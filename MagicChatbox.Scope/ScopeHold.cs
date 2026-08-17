namespace MagicChatbox.Scope;

/// <summary>
/// How long a guard has been saying the same thing, so a decision can wait for it to settle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wearing an avatar is not one event.</b> VRChat announces the change, then the parameters arrive
/// over the following seconds, so a guard reading them flickers through Unknown and often through the
/// wrong answer before landing. Acting on the first reading means starting and stopping whatever the
/// guard gates, in front of other people, every time somebody changes clothes.
/// </para>
/// <para>
/// <b>Unknown breaks the run rather than extending it.</b> A fact dropping out is not evidence that the
/// last answer is still true — it is the absence of evidence — so the dwell restarts when the fact comes
/// back. The alternative, treating Unknown as "no change", would let a guard commit on the strength of a
/// window in which nothing could be read at all.
/// </para>
/// </remarks>
public struct ScopeHold
{
    private ScopeOutcome _outcome;
    private long _since;
    private bool _started;
    private bool _isOpeningRun;

    /// <summary>What the guard last said.</summary>
    public readonly ScopeOutcome Outcome => _outcome;

    /// <summary>False until the first observation, which is the one case that commits immediately.</summary>
    public readonly bool HasObserved => _started;

    public void Observe(ScopeOutcome outcome, long ticks)
    {
        if (!_started)
        {
            _started = true;
            _isOpeningRun = true;
            _outcome = outcome;
            _since = ticks;
            return;
        }

        if (outcome != _outcome)
        {
            _isOpeningRun = false;
            _outcome = outcome;
            _since = ticks;
        }
    }

    /// <summary>
    /// True when the current reading is <paramref name="outcome"/> and has been for the whole dwell.
    /// </summary>
    public readonly bool HasHeldFor(ScopeOutcome outcome, TimeSpan dwell, long nowTicks)
    {
        if (!_started || _outcome != outcome)
            return false;

        // The opening run has nothing to settle against: there is no previous answer it might be
        // flickering away from, and making somebody wait out a dwell for the app's starting state would
        // be a delay with no question behind it.
        if (_isOpeningRun || dwell <= TimeSpan.Zero)
            return true;

        return nowTicks - _since >= dwell.Ticks;
    }

    /// <summary>How long the current reading has stood.</summary>
    public readonly TimeSpan Elapsed(long nowTicks) =>
        _started ? TimeSpan.FromTicks(Math.Max(0, nowTicks - _since)) : TimeSpan.Zero;

    public void Reset()
    {
        _started = false;
        _isOpeningRun = false;
        _outcome = ScopeOutcome.Unknown;
        _since = 0;
    }
}
