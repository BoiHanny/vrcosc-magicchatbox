using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Classes.Modules;

public sealed class DiscordIpcClient : IDisposable
{
    private const int HeaderSize = 8;

    private const int MaxFrameLength = 4 * 1024 * 1024;

    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private volatile bool _disposed;
    private volatile bool _intentionalDisconnect;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event Action<JObject>? MessageReceived;

    public event Action<Exception?>? Disconnected;

    public bool IsConnected => _pipe?.IsConnected == true;

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

    public async Task SendHandshakeAsync(string clientId)
    {
        var payload = new JObject
        {
            ["v"] = 1,
            ["client_id"] = clientId
        };
        await WriteFrameAsync(0, payload).ConfigureAwait(false);
    }

    public async Task SendFrameAsync(JObject payload)
    {
        await WriteFrameAsync(1, payload).ConfigureAwait(false);
    }

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

    public async Task SendAuthorizeAsync(string clientId, string[] scopes, string nonce)
    {
        var payload = new JObject
        {
            ["cmd"] = "AUTHORIZE",
            ["nonce"] = nonce,
            ["args"] = new JObject
            {
                ["client_id"] = clientId,
                ["scopes"] = new JArray(scopes)
            }
        };
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

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

    public async Task SendGetSelectedVoiceChannelAsync(string? nonce = null)
    {
        var payload = new JObject
        {
            ["cmd"] = "GET_SELECTED_VOICE_CHANNEL",
            ["nonce"] = nonce ?? Guid.NewGuid().ToString()
        };
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

    public async Task SendSetActivityAsync(JObject? activity, string? nonce = null)
    {
        var payload = new JObject
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = nonce ?? Guid.NewGuid().ToString(),
            ["args"] = new JObject
            {
                ["pid"] = Environment.ProcessId,
                ["activity"] = activity            }
        };
        await SendFrameAsync(payload).ConfigureAwait(false);
    }

    public void Disconnect()
    {
        _intentionalDisconnect = true;
        _readCts?.Cancel();
        ClosePipe();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _readCts?.Dispose();
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

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _pipe.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
            await _pipe.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await _pipe.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
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
                        case 1:                            try { MessageReceived?.Invoke(payload); }
                            catch (Exception ex) { Logging.WriteInfo($"Discord message handler error: {ex.Message}"); }
                            break;

                        case 2:                            Logging.WriteInfo($"Discord IPC received CLOSE: {payload}");
                            break;

                        case 3:                            await WriteFrameAsync(4, payload).ConfigureAwait(false);
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
            if (read == 0) return (-1, null);            totalRead += read;
        }

        int opcode = BitConverter.ToInt32(header, 0);
        int length = BitConverter.ToInt32(header, 4);

        if (length <= 0 || length > MaxFrameLength)
        {
            Logging.WriteInfo($"Discord IPC frame length {length} out of range (opcode {opcode}); closing pipe to realign.");
            return (-1, null);
        }

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
