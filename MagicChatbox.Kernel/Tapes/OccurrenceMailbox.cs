using System.Threading.Channels;

namespace MagicChatbox.Kernel;

/// <summary>
/// Every occurrence, in order, for the audit ledger and the SSE occurrence channel.
/// </summary>
/// <remarks>
/// Bounded at 256 — matching the replay ring, so a client that reconnects within the ring can recover
/// exactly what a full mailbox could have dropped — with every drop counted and surfaced.
/// <para>
/// The bound is small on purpose and it is safe because of how the tapes are split: continuous changes
/// structurally cannot reach the occurrence tape, so this channel is sized for human-rate traffic
/// rather than for the 2,700/sec avatar-parameter firehose.
/// </para>
/// </remarks>
public sealed class OccurrenceMailbox : IOccurrenceSink
{
    /// <summary>256, matching <see cref="IOccurrenceTape.Replay"/>'s ring depth.</summary>
    public const int DefaultCapacity = 256;

    private readonly Channel<Occurrence> _channel;

    private long _lostCount;

    /// <summary>Creates a mailbox with the given bound.</summary>
    public OccurrenceMailbox(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _channel = Channel.CreateBounded<Occurrence>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            },
            _ => Interlocked.Increment(ref _lostCount));
    }

    /// <summary>The consumer's end.</summary>
    public ChannelReader<Occurrence> Reader => _channel.Reader;

    /// <summary>How many occurrences were dropped. Any non-zero value is surfaced to the client.</summary>
    public long LostCount => Interlocked.Read(ref _lostCount);

    /// <inheritdoc />
    public void OnOccurrence(Occurrence occurrence) => _channel.Writer.TryWrite(occurrence);

    /// <summary>Completes the channel so a reader loop can exit at shutdown.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
