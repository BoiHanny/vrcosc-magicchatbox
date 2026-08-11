using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public readonly record struct SoundpadPipeReply(string? Response, bool RequestDelivered);

public sealed class SoundpadPipeClient : IDisposable
{
    public const string DefaultPipeName = "sp_remote_control";

    private static readonly TimeSpan MinRequestSpacing = TimeSpan.FromMilliseconds(2);

    private readonly string _pipeName;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private byte[]? _readBuffer;
    private bool _messageMode;
    private long _lastRequestTimestamp;
    private volatile bool _disposed;

    public SoundpadPipeClient(string pipeName = DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public bool IsConnected => _pipe?.IsConnected == true;

    public async Task<bool> TryConnectAsync(int timeoutMs = 1000, CancellationToken ct = default)
    {
        if (_disposed) return false;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await EnsureConnectedAsync(timeoutMs, ct).ConfigureAwait(false) != null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SoundpadPipeReply> SendRequestAsync(string command, int timeoutMs = 2000, CancellationToken ct = default)
    {
        if (_disposed) return new SoundpadPipeReply(null, false);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var pipe = await EnsureConnectedAsync(timeoutMs, ct).ConfigureAwait(false);
            if (pipe == null)
                return new SoundpadPipeReply(null, false);

            if (_lastRequestTimestamp != 0)
            {
                TimeSpan sinceLast = Stopwatch.GetElapsedTime(_lastRequestTimestamp);
                if (sinceLast < MinRequestSpacing)
                    await Task.Delay(MinRequestSpacing - sinceLast, ct).ConfigureAwait(false);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            bool delivered = false;
            try
            {
                byte[] request = Encoding.UTF8.GetBytes(command);
                await pipe.WriteAsync(request.AsMemory(), timeout.Token).ConfigureAwait(false);
                await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
                delivered = true;
                _lastRequestTimestamp = Stopwatch.GetTimestamp();
                string response = await ReadResponseAsync(pipe, timeout.Token).ConfigureAwait(false);
                return new SoundpadPipeReply(response, true);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException ||
                                       (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                DisconnectCore();
                return new SoundpadPipeReply(null, delivered);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Disconnect()
    {
        DisconnectCore();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisconnectCore();
    }

    private async Task<NamedPipeClientStream?> EnsureConnectedAsync(int timeoutMs, CancellationToken ct)
    {
        var existing = _pipe;
        if (existing?.IsConnected == true)
            return existing;

        DisconnectCore();
        if (_disposed)
            return null;

        var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeoutMs, ct).ConfigureAwait(false);
            try
            {
                pipe.ReadMode = PipeTransmissionMode.Message;
                _messageMode = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _messageMode = false;
            }
            _pipe = pipe;
            if (_disposed)
            {
                DisconnectCore();
                return null;
            }
            return pipe;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException ||
                                   (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            pipe.Dispose();
            return null;
        }
    }

    private async Task<string> ReadResponseAsync(NamedPipeClientStream pipe, CancellationToken ct)
    {
        byte[] buffer = _readBuffer ??= new byte[64 * 1024];
        using var ms = new MemoryStream();
        while (true)
        {
            int read = await pipe.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read <= 0)
                break;
            ms.Write(buffer, 0, read);
            if (_messageMode ? pipe.IsMessageComplete : read < buffer.Length)
                break;
        }

        if (ms.Length == 0)
            throw new IOException("The Soundpad pipe was closed before a response was received.");

        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length).TrimEnd('\0');
    }

    private void DisconnectCore()
    {
        var pipe = Interlocked.Exchange(ref _pipe, null);
        try
        {
            pipe?.Dispose();
        }
        catch (IOException)
        {
        }
    }
}
