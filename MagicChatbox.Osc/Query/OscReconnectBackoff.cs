namespace MagicChatbox.Osc.Query;

/// <summary>
/// The one backoff formula for the whole transport: discovery retries, <c>?HOST_INFO</c> refetches, and
/// receive-socket rebinds alike.
/// </summary>
/// <remarks>
/// Ported verbatim in behaviour from v2's <c>TransportReconnectBackoff</c>. It is a pure function with
/// the jitter sample passed in rather than drawn from <see cref="Random"/> inside, which is what makes
/// the schedule assertable in a test instead of merely plausible.
/// </remarks>
public static class OscReconnectBackoff
{
    /// <summary>Delay before the first retry.</summary>
    public static readonly TimeSpan DefaultBase = TimeSpan.FromSeconds(1);

    /// <summary>Ceiling. A user who alt-tabs out of VRChat for ten minutes should not wait ten minutes to reconnect.</summary>
    public static readonly TimeSpan DefaultMax = TimeSpan.FromSeconds(30);

    /// <summary>Plus or minus 25%, so a machine running several OSC apps does not retry in lockstep.</summary>
    public const double DefaultJitterFactor = 0.25;

    /// <summary>Computes the delay before the next attempt.</summary>
    /// <param name="failureCount">1-based consecutive failure count. Values below 1 are treated as 1.</param>
    /// <param name="jitterSample">A uniform [0,1) sample. Fixed in tests; <c>Random.Shared.NextDouble()</c> in production.</param>
    /// <param name="baseDelay">Delay for the first failure, before jitter.</param>
    /// <param name="maxDelay">Cap, applied before and after jitter.</param>
    /// <param name="jitterFactor">Fractional jitter, e.g. 0.25 for ±25%.</param>
    public static TimeSpan CalculateDelay(
        int failureCount,
        double jitterSample,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double jitterFactor = DefaultJitterFactor)
    {
        var baseSeconds = (baseDelay ?? DefaultBase).TotalSeconds;
        var maxSeconds = (maxDelay ?? DefaultMax).TotalSeconds;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSeconds, baseSeconds);

        var bounded = Math.Max(1, failureCount);

        // The exponent is capped independently of the delay: 2^(n-1) overflows to infinity long before a
        // long-running session's failure count does, and Math.Min on an infinity is not a number anyone
        // wants to schedule a timer from.
        var exponential = baseSeconds * Math.Pow(2, Math.Min(bounded - 1, 10));
        var capped = Math.Min(exponential, maxSeconds);

        var sample = Math.Clamp(jitterSample, 0d, 1d);
        var scale = 1 + (((sample * 2) - 1) * jitterFactor);

        return TimeSpan.FromSeconds(Math.Clamp(capped * scale, baseSeconds, maxSeconds));
    }
}
