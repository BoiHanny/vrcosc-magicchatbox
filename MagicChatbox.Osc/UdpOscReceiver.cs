using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace MagicChatbox.Osc;

/// <summary>The inbound half of the transport: a bound UDP port and the loop that drains it.</summary>
/// <remarks>
/// Public where <see cref="IOscSender"/> is internal, and the asymmetry is deliberate. D5 fences the
/// <i>send</i> side because sending is what can embarrass a user in front of other players. Receiving
/// has no such blast radius, and the kernel bridge in <c>Core</c> has to be able to subscribe.
/// </remarks>
public interface IOscReceiver : IDisposable
{
    /// <summary>The UDP port actually bound. Meaningful only after <see cref="Bind"/>.</summary>
    /// <remarks>This is the number we advertise over mDNS, which is why it must be readable, not assumed.</remarks>
    int Port { get; }

    /// <summary>Live decode counters, including malformed datagrams that were dropped.</summary>
    OscDecodeCounters Counters { get; }

    /// <summary>Binds the socket and returns the chosen port.</summary>
    int Bind();

    /// <summary>Runs the receive loop until cancelled. Returns normally on cancellation.</summary>
    Task RunAsync(CancellationToken cancellationToken);
}

/// <summary>Binds a UDP port, decodes everything that arrives, and refuses to die.</summary>
/// <remarks>
/// <para>
/// The requested port is normally <c>0</c>. Hard-coding 9001 is how an application becomes silently
/// deaf: the moment a second OSC application is running the port is taken, and the failure mode is
/// nothing at all — no error, no log line (§12.1). The bound port is advertised over OSCQuery instead.
/// </para>
/// <para><b>The loop survives everything a remote sender can do to it.</b> Three cases, three reasons:</para>
/// <list type="bullet">
///   <item>A malformed datagram is counted and skipped. VRCOSC's equivalent catches only
///   <see cref="OperationCanceledException"/>, so one bad packet ends reception for the session.</item>
///   <item><see cref="SocketError.ConnectionReset"/> is skipped. On Windows a UDP socket surfaces an
///   inbound ICMP port-unreachable — provoked by <i>our own</i> egress to a port VRChat has closed — as
///   a receive error. Treating that as fatal means quitting VRChat permanently deafens us.</item>
///   <item>A throwing sink is counted and skipped, in <see cref="OscPacketDecoder"/>.</item>
/// </list>
/// <para>
/// What does end the loop: cancellation, disposal, and a socket error that means the socket is gone.
/// Those are counted too, so "we stopped receiving" is never a silent state.
/// </para>
/// </remarks>
public sealed class UdpOscReceiver : IOscReceiver
{
    // The maximum UDP payload. VRChat bundles 40-60 face-tracking parameters into one datagram, so a
    // smaller buffer would truncate real traffic into malformed-bundle counts.
    private const int MaxDatagramBytes = 65_507;

    private readonly IOscMessageSink _sink;
    private readonly IPAddress _bindAddress;
    private readonly int _requestedPort;
    private readonly Socket _socket;
    private bool _disposed;

    /// <param name="sink">Receives every decoded message, inline on the receive loop.</param>
    /// <param name="bindAddress">Defaults to loopback: VRChat's OSC traffic is local.</param>
    /// <param name="port">0 asks the OS for a free port, which is the intended production value.</param>
    public UdpOscReceiver(IOscMessageSink sink, IPAddress? bindAddress = null, int port = 0)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);

        _sink = sink;
        _bindAddress = bindAddress ?? IPAddress.Loopback;
        _requestedPort = port;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    /// <inheritdoc />
    public int Port { get; private set; }

    /// <inheritdoc />
    public OscDecodeCounters Counters { get; } = new();

    /// <inheritdoc />
    /// <exception cref="SocketException">The requested port is taken. Callers that requested 0 cannot hit this.</exception>
    public int Bind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _socket.Bind(new IPEndPoint(_bindAddress, _requestedPort));
        Port = ((IPEndPoint)_socket.LocalEndPoint!).Port;
        return Port;
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Port == 0)
        {
            Bind();
        }

        var buffer = ArrayPool<byte>.Shared.Rent(MaxDatagramBytes);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int received;
                try
                {
                    received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset
                                                     or SocketError.MessageSize
                                                     or SocketError.NetworkReset)
                {
                    Counters.CountMalformedDatagram();
                    continue;
                }

                if (received <= 0)
                {
                    continue;
                }

                // Decode is total: it counts what it cannot read and returns. Nothing here can throw
                // out of the loop, which is the entire contract of this method.
                OscPacketDecoder.Decode(buffer.AsSpan(0, received), _sink, Counters);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <inheritdoc />
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
