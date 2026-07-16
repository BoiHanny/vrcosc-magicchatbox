using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Outcome of one pipe request. <see cref="Response"/> is null when no response was received;
/// <see cref="RequestDelivered"/> tells whether the request bytes were written to Soundpad —
/// callers must not blindly retry a delivered command (it may have executed).
/// </summary>
public readonly record struct SoundpadPipeReply(string? Response, bool RequestDelivered);

/// <summary>
/// Persistent client for Soundpad's remote-control named pipe (\\.\pipe\sp_remote_control).
/// The connection is reused across requests and re-established on demand after a broken pipe.
/// Exactly one request/response pair is in flight at a time: Soundpad's pipe can break when
/// requests are pipelined or sent within the same millisecond.
/// </summary>
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

    /// <summary>Connects if not already connected. Returns false when the pipe is unavailable.</summary>
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

    /// <summary>
    /// Sends a raw remote-control command (e.g. "GetPlayStatus()") and returns Soundpad's
    /// response, or a null response when Soundpad is unreachable or the request timed out.
    /// </summary>
    public async Task<SoundpadPipeReply> SendRequestAsync(string command, int timeoutMs = 2000, CancellationToken ct = default)
    {
        if (_disposed) return new SoundpadPipeReply(null, false);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var pipe = await EnsureConnectedAsync(timeoutMs, ct).ConfigureAwait(false);
            if (pipe == null)
                return new SoundpadPipeReply(null, false);

            // The official reference client spaces requests by at least 1 ms:
            // same-millisecond bursts can break the pipe.
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

    /// <summary>Drops the connection; the next request reconnects automatically.</summary>
    public void Disconnect()
    {
        // Deliberately not gated: disposing the pipe aborts any in-flight read, which
        // SendRequestAsync already handles as a broken connection.
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
                // Message read mode gives exact response framing; Soundpad's large XML
                // replies (GetSoundlist) would otherwise need a length heuristic.
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
                // Dispose() raced the connect — don't leak a live connection.
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

        // PipeStream signals a broken pipe as a 0-byte read (EOF), not an exception.
        // Soundpad never sends an empty response, so nothing-received means the pipe died.
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
            // A broken pipe can throw on close; there is nothing left to release.
        }
    }
}
