using System.Diagnostics;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>
/// Whether the world the user is currently in should receive chatbox output at all.
/// </summary>
/// <remarks>
/// Some worlds are private, some are performances, some have their own chat systems that a bot-looking
/// chatbox spams over. The user maintains the list; this gate is consulted before every chatbox send and
/// there is no path that skips it.
/// <para>
/// Defaults to <b>not</b> muted so a discovery failure cannot silently mute the product — but a source
/// that cannot determine the current world should say so through <see cref="ReasonCode"/>, not by
/// guessing.
/// </para>
/// </remarks>
public interface IWorldPolicy
{
    /// <summary>True when the current world is on the user's mute list.</summary>
    bool IsCurrentWorldMuted { get; }
}

/// <summary>Blocks text the user has asked never to be sent on their behalf.</summary>
/// <remarks>
/// This runs on composed output, not on user input. The composer assembles text from sources the user
/// does not control — a track title, a world name, a transcript — so the filter's job is stopping
/// MagicChatbox from putting something in the user's mouth, not censoring the user.
/// </remarks>
public interface IProfanityPolicy
{
    /// <summary>True when <paramref name="text"/> must not be sent; <paramref name="term"/> is the match.</summary>
    bool Blocks(string text, out string? term);
}

/// <summary>Permits at most one chatbox send per cadence window.</summary>
public interface IChatboxCadence
{
    /// <summary>True if a send may proceed now. Consumes the slot when it returns true.</summary>
    bool TryAcquire();

    /// <summary>How long until the next slot. <see cref="TimeSpan.Zero"/> when one is available.</summary>
    TimeSpan TimeUntilNext { get; }
}

/// <summary>Default cadence: one send per interval, monotonic clock.</summary>
/// <remarks>
/// <para><b>1500 ms, and the number has a source.</b></para>
/// <para>
/// This is VRCOSC's shipped default (<c>VRCOSC.App/Settings/SettingsManager.cs:63</c>) — the only
/// field-tested figure available. v2 used 0.7 s and its own comment called it folklore; an earlier v3
/// draft said ~1.4 s with no source at all.
/// </para>
/// <para>
/// <b>This is a courtesy cadence, NOT VRChat's enforced limit.</b> Those are different things and
/// conflating them is how the folklore got created. VRChat 2026.2.1 replaced the flat timeout with a
/// leaky bucket — "You are now allowed to send 5 messages within 5 seconds before having to wait to
/// send your next message" — and states that "Auto sent messages will no longer contribute to rate
/// limiting, only manually sending a message will have limits"
/// (<c>docs.vrchat.com/docs/vrchat-202621</c>) — so OSC-driven sends are likely exempt entirely. We go
/// slower anyway, because the constraint that actually matters is not spamming the in-game chat log
/// and the bubble other players read.
/// </para>
/// <para>
/// <b>Re-checked:</b> that text was first read off the open-beta page and now appears verbatim on the
/// shipped 2026.2.1 release page, so the wording is no longer provisional. What remains unverified is
/// whether the bucket engages against <i>this</i> app's sends at all — that is a live-session
/// observation, not a documentation question, and nothing here has been measured against a running
/// VRChat. This interval stays tunable rather than load-bearing, and it is conservative either way:
/// one send per 1500 ms puts at most three messages into any five-second window against an allowance
/// of five, and cannot burst at all, because this is a flat interval and not a bucket.
/// </para>
/// <para>
/// <b>Tunable downwards only as far as <see cref="MotionSafety.MinimumIntervalMs"/>, and that one
/// direction is load-bearing.</b> Everything this clock drives is rendered on other people's screens, so
/// a fast enough cadence is a strobe whatever the text says. The constructor refuses anything under the
/// floor rather than clamping to it: a caller who asked for 100 ms and silently got 400 would go looking
/// for the bug in their own code. See <see cref="MotionSafety"/> for the arithmetic.
/// </para>
/// </remarks>
public sealed class ChatboxCadence : IChatboxCadence
{
    /// <summary>
    /// VRCOSC's field-tested default in milliseconds, as a compile-time constant.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="DefaultInterval"/> rather than written twice because
    /// <c>ChatboxPaceSettings.SendDefaultMilliseconds</c> is a <c>const int</c> — a settings default has to
    /// be usable in an attribute-free field initialiser and in the frontend's mirrored constant — and a
    /// second literal 1500 in a second file is exactly the drift §5.1.1 of the revision plan measured.
    /// </remarks>
    public const int DefaultIntervalMilliseconds = 1500;

    /// <summary>VRCOSC's field-tested default. See the remarks — courtesy, not enforcement.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(DefaultIntervalMilliseconds);

    /// <summary>
    /// How long unchanged content may sit before it is sent again. 20 seconds.
    /// </summary>
    /// <remarks>
    /// P4 — chatbox display duration is 2–60 seconds and is a <b>per-viewer client setting</b>
    /// (<c>wiki.vrchat.com/wiki/Chatbox</c>). A composition that stops being resent therefore vanishes
    /// for viewers at the short end of that range, and the sender has no way to observe it happening.
    /// Twenty seconds sits under the documented floor's ten-fold margin at a cost of one message per
    /// twenty seconds. <see cref="VrcChatboxPublisher"/> enforces it.
    /// </remarks>
    public static readonly TimeSpan UnchangedResend = TimeSpan.FromSeconds(20);

    /// <summary>
    /// After 60 seconds with nothing resending it, a line is off every screen in the room.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The top of the same 2–60 second range <see cref="UnchangedResend"/> is derived from, and the same
    /// primary source: <c>wiki.vrchat.com/wiki/Chatbox</c> documents chatbox display duration as a
    /// <b>per-viewer client setting</b> between two and sixty seconds. Inside the window this side cannot
    /// know who is still reading; past it, nobody is.
    /// </para>
    /// <para>
    /// <b>The ceiling is what makes "clear the box" answerable.</b> Wiping a box that VRChat emptied
    /// minutes ago is not a no-op — <c>V2-V3-REVISION-PLAN.md</c> D2 records that nobody in either
    /// codebase has ever observed whether an empty <c>/chatbox/input</c> clears the bubble or leaves an
    /// empty one, so a clear sent into silence may well <i>create</i> the thing it was meant to remove.
    /// <c>PanicSwitch</c> is the first caller and reads it for exactly that.
    /// </para>
    /// <para>
    /// <b>The frontend keeps its own copy</b> (<c>OutputStrip.tsx</c>'s <c>CLEARED_AFTER_SECONDS</c>) and
    /// that is not drift to fold away here: it is a rendering decision about when a status card stops
    /// claiming a line is up, made on the far side of a loopback hop, and it cites this same wiki
    /// sentence itself. Two readers of one documented fact, not two guesses.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan DisplayCeiling = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _interval;
    private readonly object _gate = new();
    private long _lastSendTicks;
    private bool _everSent;

    /// <summary>Builds a cadence, defaulting to <see cref="DefaultInterval"/>.</summary>
    /// <param name="interval">
    /// How long between sends. Must be at least <see cref="MotionSafety.MinimumIntervalMs"/>; see this
    /// type's remarks for why that bound is not negotiable and why it throws rather than clamps.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is under the motion-safety floor.</exception>
    public ChatboxCadence(TimeSpan? interval = null)
    {
        var value = interval ?? DefaultInterval;
        if (value < MotionSafety.MinimumInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                value,
                $"A chatbox cadence under {MotionSafety.MinimumIntervalMs} ms would put a flashing image on "
                + "other people's screens. See MotionSafety.");
        }

        _interval = value;
    }

    public TimeSpan TimeUntilNext
    {
        get
        {
            lock (_gate)
            {
                if (!_everSent)
                {
                    return TimeSpan.Zero;
                }

                var elapsed = Stopwatch.GetElapsedTime(_lastSendTicks);
                return elapsed >= _interval ? TimeSpan.Zero : _interval - elapsed;
            }
        }
    }

    public bool TryAcquire()
    {
        lock (_gate)
        {
            if (_everSent && Stopwatch.GetElapsedTime(_lastSendTicks) < _interval)
            {
                return false;
            }

            _lastSendTicks = Stopwatch.GetTimestamp();
            _everSent = true;
            return true;
        }
    }
}

/// <summary>Records what egress did, for the Audit ledger.</summary>
/// <remarks>
/// Kernel-shaped occurrences are not available in Phase 1 (the kernel is built last, by design), so
/// egress records through this seam and the kernel implementation is substituted later without egress
/// changing. Phase 1 ships a no-op and an in-memory recorder for tests.
/// </remarks>
public interface IEgressJournal
{
    void Dispatched(Correlation correlation, string surface, string? detail);

    void Blocked(Correlation correlation, string surface, ReasonCode reason, string? detail);
}

/// <summary>Discards everything. The default until the kernel's occurrence tape exists.</summary>
public sealed class NullEgressJournal : IEgressJournal
{
    public static readonly NullEgressJournal Instance = new();

    private NullEgressJournal() { }

    public void Dispatched(Correlation correlation, string surface, string? detail) { }

    public void Blocked(Correlation correlation, string surface, ReasonCode reason, string? detail) { }
}
