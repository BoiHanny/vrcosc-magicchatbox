using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Channels;

namespace MagicChatbox.Osc;

/// <summary>
/// Materializes decoded messages into a bounded channel, for consumers that want a stream rather than
/// an inline callback.
/// </summary>
/// <remarks>
/// <para>
/// §12.1 sketches the ingress path as <c>IOscReceiver.Messages : IAsyncEnumerable&lt;OscMessage&gt;</c>,
/// which cannot be reconciled with the same section's requirement that the parse path allocate nothing
/// per message — an <see cref="OscMessage"/> is a class with an array. The defensible reading is that
/// both surfaces exist and only one is the hot path: <see cref="IOscMessageSink"/> is what the kernel
/// bridge implements, and this adapter is what tests, diagnostics and any future non-kernel consumer
/// use. It allocates one message and one argument array per message, and says so here rather than
/// pretending otherwise.
/// </para>
/// <para>
/// The channel is bounded and drops the oldest item when full. At face-tracking rates an unbounded
/// channel in front of a slow reader is an out-of-memory crash with a long fuse; dropping is honest and
/// counted.
/// </para>
/// </remarks>
public sealed class BufferedOscMessageSink : IOscMessageSink
{
    private readonly Channel<OscMessage> _channel;
    private long _dropped;

    /// <param name="capacity">Messages buffered before the oldest starts being dropped.</param>
    public BufferedOscMessageSink(int capacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _channel = Channel.CreateBounded<OscMessage>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = true,
                SingleReader = false,
            },
            itemDropped: _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>The decoded messages, oldest first.</summary>
    public IAsyncEnumerable<OscMessage> Messages => _channel.Reader.ReadAllAsync();

    /// <summary>Messages discarded because the reader fell behind.</summary>
    public long Dropped => Volatile.Read(ref _dropped);

    /// <summary>Tries to take one buffered message without waiting.</summary>
    public bool TryRead([NotNullWhen(true)] out OscMessage? message)
    {
        if (_channel.Reader.TryRead(out var item))
        {
            message = item;
            return true;
        }

        message = null;
        return false;
    }

    /// <summary>Completes the stream, so an enumerating consumer finishes rather than hanging.</summary>
    public void Complete() => _channel.Writer.TryComplete();

    /// <inheritdoc />
    public void OnMessage(scoped ref OscReader message)
    {
        var address = Encoding.UTF8.GetString(message.Address);

        var args = new OscArg[message.ArgumentCount];
        var count = 0;

        while (message.TryReadNext(out var value))
        {
            // Tags we can size but not represent are dropped rather than faked into a neighbouring type.
            if (value.IsSupported)
            {
                args[count++] = value.ToArg();
            }
        }

        if (count != args.Length)
        {
            args = args[..count];
        }

        _channel.Writer.TryWrite(OscMessage.Create(address, args));
    }
}
