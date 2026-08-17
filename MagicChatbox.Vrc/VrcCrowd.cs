namespace MagicChatbox.Vrc;

/// <summary>How busy an instance is, in three buckets.</summary>
public enum VrcCrowd
{
    Unknown = 0,
    Quiet = 1,
    Busy = 2,
    Packed = 3,
}

/// <summary>
/// Turns a headcount into a bucket that does not change its mind every few seconds.
/// </summary>
/// <remarks>
/// <b>The thresholds are asymmetric on purpose.</b> A single boundary means one person standing in a
/// doorway flips the bucket back and forth as they come and go, and anything watching it reacts every
/// time. Entering Busy takes six people; leaving it takes dropping to four. The gap is the hysteresis,
/// and it is why this takes the previous bucket as an argument rather than being a pure function of the
/// count.
/// <para>
/// The numbers are a starting point rather than a measurement. They come from what "a few people" and "a
/// full room" mean in an ordinary VRChat world, and they are worth revisiting against a real session
/// before anybody treats them as settled.
/// </para>
/// </remarks>
public static class VrcCrowdBuckets
{
    public const int BusyEnters = 6;
    public const int BusyLeaves = 4;
    public const int PackedEnters = 16;
    public const int PackedLeaves = 12;

    public static VrcCrowd Classify(VrcCrowd previous, int headcount)
    {
        if (headcount < 0)
            return VrcCrowd.Unknown;

        return previous switch
        {
            VrcCrowd.Packed => headcount < PackedLeaves ? StepDownFromPacked(headcount) : VrcCrowd.Packed,
            VrcCrowd.Busy => headcount >= PackedEnters
                ? VrcCrowd.Packed
                : headcount < BusyLeaves ? VrcCrowd.Quiet : VrcCrowd.Busy,
            _ => Fresh(headcount),
        };
    }

    private static VrcCrowd StepDownFromPacked(int headcount) =>
        headcount < BusyLeaves ? VrcCrowd.Quiet : VrcCrowd.Busy;

    private static VrcCrowd Fresh(int headcount)
    {
        if (headcount >= PackedEnters)
            return VrcCrowd.Packed;

        return headcount >= BusyEnters ? VrcCrowd.Busy : VrcCrowd.Quiet;
    }

    public static string NameOf(VrcCrowd crowd) => crowd switch
    {
        VrcCrowd.Quiet => "Quiet",
        VrcCrowd.Busy => "Busy",
        VrcCrowd.Packed => "Packed",
        _ => "Unknown",
    };
}
