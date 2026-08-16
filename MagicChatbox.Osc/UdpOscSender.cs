using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace MagicChatbox.Osc;

/// <summary>Supplies the endpoint to send to, or null when VRChat has not been located yet.</summary>
/// <remarks>
/// Separate from the sender because the endpoint is <i>negotiated</i>, not configured. VRChat's OSC
/// ports move the moment a second OSC app is running or the user passes custom launch arguments, so
/// binding 9000/9001 and hoping is silently deaf rather than wrong-and-loud. OSCQuery discovery
/// (Phase 2) implements this; a manual override also implements it.
/// </remarks>
public interface IOscEndpointProvider
{
    /// <summary>The current VRChat OSC endpoint, or null when unknown.</summary>
    IPEndPoint? Current { get; }
}

/// <summary>Sends OSC over UDP. Internal by design — see <see cref="IOscSender"/>.</summary>
internal sealed class UdpOscSender : IOscSender, IDisposable
{
    // 4 KB holds every message this app sends, with the chatbox the only one worth sizing against.
    // VRChat's chatbox limit is 144 *characters*, not bytes, and a character there is a grapheme
    // cluster with no byte bound — an ordinary line is well under 600 bytes, a line of ZWJ family
    // emoji is around 3,600, and a pathological one built from long combining sequences could pass
    // this cap — which throws below rather than putting a truncated datagram on the wire.
    // Rented per send from the shared pool: at face-tracking rates a per-send allocation is the kind of
    // steady garbage that turns into a visible hitch in VR.
    private const int MaxMessageBytes = 4096;

    private readonly IOscEndpointProvider _endpoints;
    private readonly Socket _socket;
    private bool _disposed;

    public UdpOscSender(IOscEndpointProvider endpoints)
    {
        _endpoints = endpoints ?? throw new ArgumentNullException(nameof(endpoints));
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    public async ValueTask<bool> SendAsync(OscMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var endpoint = _endpoints.Current;
        if (endpoint is null)
        {
            return false;
        }

        var size = message.MaxEncodedSize();
        if (size > MaxMessageBytes)
        {
            throw new ArgumentException(
                $"Encoded OSC message is {size} bytes, over the {MaxMessageBytes}-byte cap.", nameof(message));
        }

        var buffer = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            var written = message.WriteTo(buffer);
            await _socket.SendToAsync(buffer.AsMemory(0, written), SocketFlags.None, endpoint, cancellationToken);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _socket.Dispose();
    }
}
