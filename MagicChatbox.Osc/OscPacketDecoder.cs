using System.Buffers.Binary;

namespace MagicChatbox.Osc;

/// <summary>Receives every decoded inbound message, synchronously, on the receive loop's thread.</summary>
/// <remarks>
/// <para>
/// The callback takes the reader by reference rather than handing over a materialized message, because
/// a materialized message means an allocation per message and this path runs at face-tracking rates
/// (§12.1). A sink that wants objects can build them; a sink that wants to intern an address from the
/// UTF-8 span — which is what the kernel bridge does — never pays for one.
/// </para>
/// <para>
/// Implementations must return quickly and must not block: they run inline on the socket loop. They
/// should not throw either, but if they do, <see cref="UdpOscReceiver"/> counts it and keeps receiving
/// rather than letting one bad handler make the application permanently deaf.
/// </para>
/// </remarks>
public interface IOscMessageSink
{
    /// <summary>Called once per decoded message. The reader is positioned at the first argument.</summary>
    void OnMessage(scoped ref OscReader message);
}

/// <summary>
/// Live counters for the receive path. Machine-readable, because "we dropped 4,000 packets" is a fact
/// the Sources screen should show rather than a line someone might find in a log file.
/// </summary>
public sealed class OscDecodeCounters
{
    private long _messages;
    private long _malformedDatagrams;
    private long _malformedBundleElements;
    private long _sinkFaults;

    /// <summary>Messages successfully handed to the sink.</summary>
    public long Messages => Volatile.Read(ref _messages);

    /// <summary>Datagrams that were not a decodable message or bundle, and were dropped whole.</summary>
    public long MalformedDatagrams => Volatile.Read(ref _malformedDatagrams);

    /// <summary>Elements inside an otherwise valid bundle that could not be decoded.</summary>
    public long MalformedBundleElements => Volatile.Read(ref _malformedBundleElements);

    /// <summary>Exceptions thrown by the sink and swallowed so the loop survives.</summary>
    public long SinkFaults => Volatile.Read(ref _sinkFaults);

    internal void CountMessage() => Interlocked.Increment(ref _messages);

    internal void CountMalformedDatagram() => Interlocked.Increment(ref _malformedDatagrams);

    internal void CountMalformedBundleElement() => Interlocked.Increment(ref _malformedBundleElements);

    internal void CountSinkFault() => Interlocked.Increment(ref _sinkFaults);
}

/// <summary>Walks a received datagram — a message, or a bundle of them — and dispatches each message.</summary>
/// <remarks>
/// Ported from v2's <c>OscReader.ProcessPacketCore</c>, including its bundle-depth cap. v2 kept its
/// malformed counter in a <c>static</c> field, which makes the number process-global and untestable;
/// here the counters are an instance the owner passes in.
/// </remarks>
public static class OscPacketDecoder
{
    /// <summary>
    /// Nested bundles are legal OSC and VRChat never sends them. The cap exists so one crafted 64 KB
    /// datagram of nested <c>#bundle</c> headers cannot recurse the receive thread's stack to death
    /// (v2 audit F-023) — a remote process terminating ours with a single packet.
    /// </summary>
    public const int MaxBundleDepth = 32;

    private const int BundleHeaderLength = 16; // "#bundle\0" (8) + OSC time tag (8)

    private static ReadOnlySpan<byte> BundleTag => "#bundle\0"u8;

    /// <summary>Decodes one datagram. Never throws for malformed input; counts it and returns.</summary>
    public static void Decode(ReadOnlySpan<byte> datagram, IOscMessageSink sink, OscDecodeCounters counters)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(counters);

        DecodeCore(datagram, sink, counters, depth: 0, isTopLevel: true);
    }

    private static void DecodeCore(
        ReadOnlySpan<byte> packet,
        IOscMessageSink sink,
        OscDecodeCounters counters,
        int depth,
        bool isTopLevel)
    {
        if (IsBundle(packet))
        {
            if (depth >= MaxBundleDepth)
            {
                Count(counters, isTopLevel);
                return;
            }

            var cursor = BundleHeaderLength;
            while (cursor < packet.Length)
            {
                if (cursor + 4 > packet.Length)
                {
                    counters.CountMalformedBundleElement();
                    return;
                }

                var size = BinaryPrimitives.ReadInt32BigEndian(packet.Slice(cursor, 4));
                cursor += 4;

                // Subtraction, not addition: a hostile size prefix of 0x7FFFFFFF makes `cursor + size`
                // overflow to a negative number, which sails through the bounds check and then throws
                // inside Slice. v2 wrote the addition; a test with a maximal size prefix found it here.
                if (size <= 0 || size > packet.Length - cursor)
                {
                    // The size prefix is how we find the next element, so a bad one costs the rest of
                    // the bundle — but only the rest of this bundle. The socket loop carries on.
                    counters.CountMalformedBundleElement();
                    return;
                }

                DecodeCore(packet.Slice(cursor, size), sink, counters, depth + 1, isTopLevel: false);
                cursor += size;
            }

            return;
        }

        if (!OscReader.TryParse(packet, out var reader))
        {
            Count(counters, isTopLevel);
            return;
        }

        counters.CountMessage();

        try
        {
            sink.OnMessage(ref reader);
        }
        catch (Exception)
        {
            // A throwing sink must not make us deaf. VRCOSC's loop catches only OperationCanceledException
            // and dies permanently on anything else; VRCNext uses a bare `catch {}` and reports nothing.
            // The third option — count it, keep receiving — is the only one that leaves evidence.
            counters.CountSinkFault();
        }
    }

    private static void Count(OscDecodeCounters counters, bool isTopLevel)
    {
        if (isTopLevel)
        {
            counters.CountMalformedDatagram();
        }
        else
        {
            counters.CountMalformedBundleElement();
        }
    }

    private static bool IsBundle(ReadOnlySpan<byte> packet) =>
        packet.Length >= BundleHeaderLength && packet[..8].SequenceEqual(BundleTag);
}
