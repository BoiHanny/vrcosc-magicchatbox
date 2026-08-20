using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services.Voicemod;

public sealed class ClientWebSocketFactory : IVoicemodSocketFactory
{
    public IVoicemodSocket Create() => new ClientWebSocketAdapter();

    private sealed class ClientWebSocketAdapter : IVoicemodSocket
    {
        private const int MaximumMessageBytes = 16 * 1024 * 1024;
        private readonly ClientWebSocket _socket = new();

        public ClientWebSocketAdapter()
        {
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        }

        public WebSocketState State => _socket.State;

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
            => _socket.ConnectAsync(uri, cancellationToken);

        public Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            return _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                if (result.Count > 0)
                {
                    stream.Write(buffer, 0, result.Count);
                    if (stream.Length > MaximumMessageBytes)
                        throw new InvalidDataException("A Voicemod message exceeded the 16 MB safety limit.");
                }
            }
            while (!result.EndOfMessage);

            return result.MessageType == WebSocketMessageType.Text
                ? Encoding.UTF8.GetString(stream.ToArray())
                : string.Empty;
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "MagicChatbox disconnecting",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public ValueTask DisposeAsync()
        {
            _socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
