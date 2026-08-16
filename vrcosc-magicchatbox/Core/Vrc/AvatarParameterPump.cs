using MagicChatbox.Vrc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record AvatarParameterPumpOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMilliseconds(50);

    public TimeSpan DefaultMinInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan KeepAlive { get; init; } = TimeSpan.FromSeconds(11);

    public int MaxSendsPerTick { get; init; } = 8;
}

public readonly record struct AvatarParameterPumpStats(
    long Published,
    long Sent,
    long Suppressed,
    long KeepAlives,
    long Deferred,
    long Failed,
    int PendingKeys);

public sealed class AvatarParameterPump : IDisposable
{
    private sealed class Slot
    {
        public readonly object Gate = new();
        public VrcParameterKind Kind;
        public double Pending;
        public double Sent;
        public bool HasPending;
        public bool HasSent;
        public long LastSentTicks;
        public TimeSpan MinInterval;
    }

    private static readonly double TicksPerMs = Stopwatch.Frequency / 1000d;

    private readonly AvatarParameterPumpOptions _options;
    private readonly ConcurrentDictionary<string, Slot> _slots = new(StringComparer.Ordinal);
    private readonly object _lifecycle = new();

    private IVrcEgress? _egress;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _cursor;
    private bool _disposed;

    private long _published;
    private long _sent;
    private long _suppressed;
    private long _keepAlives;
    private long _deferred;
    private long _failed;

    public AvatarParameterPump(AvatarParameterPumpOptions? options = null)
    {
        _options = options ?? new AvatarParameterPumpOptions();
    }

    public bool IsRunning
    {
        get { lock (_lifecycle) return _loop != null; }
    }

    public AvatarParameterPumpStats Stats => new(
        Interlocked.Read(ref _published),
        Interlocked.Read(ref _sent),
        Interlocked.Read(ref _suppressed),
        Interlocked.Read(ref _keepAlives),
        Interlocked.Read(ref _deferred),
        Interlocked.Read(ref _failed),
        _slots.Count);

    public void SetMinInterval(string name, TimeSpan minInterval)
    {
        if (string.IsNullOrEmpty(name))
            return;

        Slot slot = _slots.GetOrAdd(name, _ => NewSlot());
        lock (slot.Gate) slot.MinInterval = minInterval;
    }

    public void Publish(string name, bool value) => Publish(name, VrcParameterKind.Bool, value ? 1d : 0d);

    public void Publish(string name, int value) => Publish(name, VrcParameterKind.Int, value);

    public void Publish(string name, float value)
    {
        if (!float.IsFinite(value))
            return;

        Publish(name, VrcParameterKind.Float, value);
    }

    public void Start(IVrcEgress egress)
    {
        ArgumentNullException.ThrowIfNull(egress);

        lock (_lifecycle)
        {
            if (_disposed || _loop != null)
                return;

            _egress = egress;

            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            _cts = cts;
            _loop = Task.Run(() => RunAsync(token), CancellationToken.None);
        }
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        Task? loop;
        CancellationTokenSource? cts;

        lock (_lifecycle)
        {
            loop = _loop;
            cts = _cts;
            _loop = null;
            _cts = null;
            _egress = null;
        }

        if (loop == null)
            return;

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        bool stopped = true;

        try
        {
            await loop.WaitAsync(timeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            stopped = false;
        }
        catch (OperationCanceledException)
        {
        }

        if (stopped)
            cts?.Dispose();
    }

    public void Reset()
    {
        foreach (Slot slot in _slots.Values)
        {
            lock (slot.Gate)
            {
                slot.HasSent = false;
                slot.LastSentTicks = 0;
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        StopAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
    }

    private Slot NewSlot() => new() { MinInterval = _options.DefaultMinInterval };

    private void Publish(string name, VrcParameterKind kind, double value)
    {
        if (string.IsNullOrEmpty(name) || _disposed)
            return;

        Slot slot = _slots.GetOrAdd(name, _ => NewSlot());

        lock (slot.Gate)
        {
            slot.Kind = kind;
            slot.Pending = value;
            slot.HasPending = true;
        }

        Interlocked.Increment(ref _published);
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_options.TickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await DrainAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DrainAsync(CancellationToken token)
    {
        IVrcEgress? egress;
        lock (_lifecycle) egress = _egress;

        if (egress == null)
            return;

        List<string> names = _slots.Keys.ToList();
        if (names.Count == 0)
            return;

        names.Sort(StringComparer.Ordinal);

        long now = Stopwatch.GetTimestamp();
        int budget = Math.Max(1, _options.MaxSendsPerTick);
        int start = _cursor % names.Count;

        for (int offset = 0; offset < names.Count && budget > 0; offset++)
        {
            if (token.IsCancellationRequested)
                return;

            string name = names[(start + offset) % names.Count];

            if (!_slots.TryGetValue(name, out Slot? slot))
                continue;

            if (!TryClaim(slot, now, out VrcParameterValue value, out bool keepAlive))
                continue;

            budget--;

            bool dispatched = await SendAsync(egress, name, value, token).ConfigureAwait(false);

            lock (slot.Gate)
            {
                if (dispatched)
                {
                    slot.LastSentTicks = Stopwatch.GetTimestamp();
                    slot.HasSent = true;
                }
                else
                {
                    slot.HasPending = true;
                }
            }

            if (dispatched)
            {
                Interlocked.Increment(ref _sent);
                if (keepAlive)
                    Interlocked.Increment(ref _keepAlives);
            }
            else
            {
                Interlocked.Increment(ref _failed);
            }
        }

        _cursor = (start + 1) % names.Count;

        if (budget == 0)
            Interlocked.Increment(ref _deferred);
    }

    private bool TryClaim(Slot slot, long now, out VrcParameterValue value, out bool keepAlive)
    {
        value = default;
        keepAlive = false;

        lock (slot.Gate)
        {
            bool changed = slot.HasPending && (!slot.HasSent || slot.Pending != slot.Sent);
            bool stale = slot.HasSent && ElapsedSince(slot.LastSentTicks, now) >= _options.KeepAlive;

            if (!changed && !stale)
            {
                if (slot.HasPending)
                {
                    slot.HasPending = false;
                    Interlocked.Increment(ref _suppressed);
                }

                return false;
            }

            if (changed && slot.HasSent && ElapsedSince(slot.LastSentTicks, now) < slot.MinInterval)
                return false;

            double raw = slot.HasPending ? slot.Pending : slot.Sent;

            value = slot.Kind switch
            {
                VrcParameterKind.Bool => VrcParameterValue.Bool(raw != 0),
                VrcParameterKind.Int => VrcParameterValue.Int((int)raw),
                _ => VrcParameterValue.Float((float)raw),
            };

            keepAlive = !changed;
            slot.Sent = raw;
            slot.HasPending = false;
            return true;
        }
    }

    private async Task<bool> SendAsync(IVrcEgress egress, string name, VrcParameterValue value, CancellationToken token)
    {
        try
        {
            EgressResult result = await egress
                .SetAvatarParameterAsync(name, value, token)
                .ConfigureAwait(false);

            return result.Dispatched;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    private static TimeSpan ElapsedSince(long since, long now)
    {
        if (since == 0)
            return TimeSpan.MaxValue;

        return TimeSpan.FromMilliseconds((now - since) / TicksPerMs);
    }
}
