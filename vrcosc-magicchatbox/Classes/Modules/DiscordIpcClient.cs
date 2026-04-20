using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Low-level Discord IPC client using native Named Pipes.
/// Implements the Discord RPC wire protocol: 8-byte header (opcode LE32 + length LE32) + UTF-8 JSON payload.
/// </summary>
public sealed class DiscordIpcClient : IDisposable
{
    private const int HeaderSize = 8;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _readCts;
    private CancellationTokenSource? _reconnectCts;
    private Task? _readTask;
    private Task? _reconnectTask;
    private volatile bool _disposed;
    private volatile bool _intentionalDisconnect;
    private int _reconnectAttempts;

    /// <summary>Raised on the read-loop thread when a JSON message arrives.</summary>
    public event Action<JObject>? MessageReceived;

    /// <summary>Raised when the pipe disconnects unexpectedly.</summary>
    public event Action<Exception?>? Disconnected;

    /// <summary>True when the pipe is connected.</summary>
    public bool IsConnected => _pipe?.IsConnected == true;

    /// <summary>
    /// Attempts to connect to Discord's IPC pipe (tries discord-ipc-0 through discord-ipc-9).
    /// Returns true on success.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        for (int i = 0; i <= Core.Constants.DiscordIpcMaxPipeIndex; i++)
        {
            if (ct.IsCancellationRequested) return false;

            string pipeName = $"{Core.Constants.DiscordIpcPipePrefix}{i}";
            try
            {
                var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(500, ct).ConfigureAwait(false);

                if (pipe.IsConnected)
                {
                    _pipe = pipe;
                    _reconnectAttempts = 0;
                    StartReadLoop();
                    Logging.WriteInfo($"Discord IPC connected on pipe: {pipeName}");
                    return true;
                }

                pipe.Dispose();
            }
            catch (TimeoutException) { }
            catch (IOException) { }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Discord IPC pipe {pipeName} failed: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Sends the initial handshake frame (opcode 0).
    /// </summary>
    public async Task SendHandshakeAsync(string clientId)
    {
        var payload = new JObject
        {
            ["v"] = 1,
            ["client_id"] = clientId
        };
        await WriteFrameAsync(0, payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a standard RPC frame (opcode 1).
    /// </summary>
    public async Task SendFrameAsync(JObject payload)
    {
        await WriteFrameAsync(1, payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends an AUTHENTICATE command with the given access token.
    /// </summary>
    public async Task SendAuthenticateAsync(string accessToken, string nonce)
    {
        var payload = new JObject
        {
            ["cmd"] = "AUTHENTICATE",
            ["nonce"] = nonce,
            ["args"] = new JObject
            {
                ["access_token"] = accessToken
            }
        };
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a SUBSCRIBE command for the given event.
    /// </summary>
    public async Task SubscribeAsync(string evt, JObject? args = null, string? nonce = null)
    {
        var payload = new JObject
        {
            ["cmd"] = "SUBSCRIBE",
            ["evt"] = evt,
            ["nonce"] = nonce ?? Guid.NewGuid().ToString()
        };
        if (args != null)
            payload["args"] = args;
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a GET_SELECTED_VOICE_CHANNEL command to fetch current channel state.
    /// </summary>
    public async Task SendGetSelectedVoiceChannelAsync(string? nonce = null)
    {
        var payload = new JObject
        {
            ["cmd"] = "GET_SELECTED_VOICE_CHANNEL",
            ["nonce"] = nonce ?? Guid.NewGuid().ToString()
        };
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts auto-reconnect loop with exponential backoff.
    /// </summary>
    public void StartAutoReconnect(Func<Task> onReconnected)
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;

        _reconnectTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                _reconnectAttempts++;
                var delay = TimeSpan.FromSeconds(
                    Math.Min(
                        Core.Constants.DiscordReconnectMinDelay.TotalSeconds * Math.Pow(2, _reconnectAttempts - 1),
                        Core.Constants.DiscordReconnectMaxDelay.TotalSeconds));

                Logging.WriteInfo($"Discord IPC reconnect attempt {_reconnectAttempts} in {delay.TotalSeconds:0.#}s");

                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested) break;

                    if (await ConnectAsync(ct).ConfigureAwait(false))
                    {
                        Logging.WriteInfo("Discord IPC reconnected successfully.");
                        await onReconnected().ConfigureAwait(false);
                        return;
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Discord IPC reconnect failed: {ex.Message}");
                }
            }
        }, ct);
    }

    /// <summary>
    /// Disconnects gracefully.
    /// </summary>
    public void Disconnect()
    {
        _intentionalDisconnect = true;
        _reconnectCts?.Cancel();
        _readCts?.Cancel();
        ClosePipe();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _readCts?.Dispose();
        _reconnectCts?.Dispose();
    }

    private async Task WriteFrameAsync(int opcode, JObject payload)
    {
        if (_pipe == null || !_pipe.IsConnected)
            throw new InvalidOperationException("Discord IPC pipe is not connected.");

        var json = payload.ToString(Newtonsoft.Json.Formatting.None);
        var data = Encoding.UTF8.GetBytes(json);
        var header = new byte[HeaderSize];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);

        await _pipe.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
        await _pipe.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        await _pipe.FlushAsync().ConfigureAwait(false);
    }

    private void StartReadLoop()
    {
        _readCts?.Cancel();
        _readCts = new CancellationTokenSource();
        var ct = _readCts.Token;

        _readTask = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested && _pipe?.IsConnected == true)
                {
                    var (opcode, payload) = await ReadFrameAsync(ct).ConfigureAwait(false);
                    if (payload == null) break;

                    switch (opcode)
                    {
                        case 1: // FRAME
                            try { MessageReceived?.Invoke(payload); }
                            catch (Exception ex) { Logging.WriteInfo($"Discord message handler error: {ex.Message}"); }
                            break;

                        case 2: // CLOSE
                            Logging.WriteInfo($"Discord IPC received CLOSE: {payload}");
                            break;

                        case 3: // PING → respond with PONG
                            await WriteFrameAsync(4, payload).ConfigureAwait(false);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Logging.WriteInfo($"Discord IPC read loop error: {ex.Message}");
            }
            finally
            {
                if (!_intentionalDisconnect && !_disposed)
                {
                    ClosePipe();
                    Disconnected?.Invoke(null);
                }
            }
        }, ct);
    }

    private async Task<(int opcode, JObject? payload)> ReadFrameAsync(CancellationToken ct)
    {
        var header = new byte[HeaderSize];
        int totalRead = 0;

        while (totalRead < HeaderSize)
        {
            ct.ThrowIfCancellationRequested();
            int read = await _pipe!.ReadAsync(header, totalRead, HeaderSize - totalRead, ct).ConfigureAwait(false);
            if (read == 0) return (-1, null); // pipe closed
            totalRead += read;
        }

        int opcode = BitConverter.ToInt32(header, 0);
        int length = BitConverter.ToInt32(header, 4);

        if (length <= 0 || length > 1024 * 64) // sanity: max 64KB
            return (opcode, new JObject());

        var data = new byte[length];
        totalRead = 0;
        while (totalRead < length)
        {
            ct.ThrowIfCancellationRequested();
            int read = await _pipe!.ReadAsync(data, totalRead, length - totalRead, ct).ConfigureAwait(false);
            if (read == 0) return (-1, null);
            totalRead += read;
        }

        var json = Encoding.UTF8.GetString(data);
        try
        {
            return (opcode, JObject.Parse(json));
        }
        catch
        {
            Logging.WriteInfo($"Discord IPC failed to parse JSON: {json[..Math.Min(200, json.Length)]}");
            return (opcode, new JObject());
        }
    }

    private void ClosePipe()
    {
        try
        {
            _pipe?.Dispose();
        }
        catch { }
        finally
        {
            _pipe = null;
        }
    }
}
