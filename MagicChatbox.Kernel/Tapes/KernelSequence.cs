namespace MagicChatbox.Kernel;

/// <summary>
/// The one monotonic counter behind every <c>Seq</c> in the kernel.
/// </summary>
/// <remarks>
/// State changes and occurrences draw from the same counter, so <c>Seq</c> is a total order over all
/// publishes and a consumer can interleave the two tapes by it.
/// <para>
/// <b>It is not a real-time order across keys.</b> Two writes to different keys are stamped under
/// different stripe locks, so their relative <c>Seq</c> reflects which thread got there first and
/// nothing more. Within one key it <i>is</i> write order, because version and sequence are stamped
/// inside the same lock — which is the fix for v2's publish-order inversion, where the version was
/// stamped under the stripe lock and the sequence under a different gate afterwards.
/// </para>
/// </remarks>
public sealed class KernelSequence
{
    private long _seq;

    /// <summary>The last number handed out. Zero on a fresh kernel; the first <see cref="Next"/> returns 1.</summary>
    public long Current => Interlocked.Read(ref _seq);

    /// <summary>Takes the next number.</summary>
    public long Next() => Interlocked.Increment(ref _seq);
}
