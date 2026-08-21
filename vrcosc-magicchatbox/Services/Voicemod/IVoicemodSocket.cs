using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services.Voicemod;

public interface IVoicemodSocket : IAsyncDisposable
{
    WebSocketState State { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    Task SendTextAsync(string message, CancellationToken cancellationToken);

    Task<string?> ReceiveTextAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);
}

public interface IVoicemodSocketFactory
{
    IVoicemodSocket Create();
}
