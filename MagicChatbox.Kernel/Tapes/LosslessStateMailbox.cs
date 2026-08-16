using System.Threading.Channels;

namespace MagicChatbox.Kernel;

/// <summary>
/// Every state change, in order, for consumers that must not miss an edge.
/// </summary>
/// <remarks>
/// Triggers, history, threshold-crossing rules. Bounded at 1,024 with drop-oldest — and every drop is
/// <b>counted</b>, which is the whole difference between this and v2's <c>CreateBounded(4, DropOldest)</c>
/// on speech transcript finals (F-084), where the losses were silent and the consumer could not tell a
/// quiet minute from a lost one.
/// <para>
/// Drop-oldest rather than back-pressure, always: a sink may never block the writer, so the only
/// remaining choice is which loss to take and whether to admit it.
/// </para>
/// </remarks>
public sealed class LosslessStateMailbox : IStateSink
{
    /// <summary>Deep enough for a 33 ms tick at the full 2,700/sec firehose to arrive intact.</summary>
    public const int DefaultCapacity = 1024;

    private readonly Channel<SignalChanged> _channel;

    private long _lostCount;

    /// <summary>Creates a mailbox with the given bound.</summary>
    public LosslessStateMailbox(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _channel = Channel.CreateBounded<SignalChanged>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            },
            _ => Interlocked.Increment(ref _lostCount));
    }

    /// <summary>The consumer's end.</summary>
    public ChannelReader<SignalChanged> Reader => _channel.Reader;

    /// <summary>How many changes were dropped. Alarm on any non-zero value.</summary>
    public long LostCount => Interlocked.Read(ref _lostCount);

    /// <inheritdoc />
    public void OnSignalChanged(in SignalChanged e) => _channel.Writer.TryWrite(e);

    /// <summary>Completes the channel so a reader loop can exit at shutdown.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
