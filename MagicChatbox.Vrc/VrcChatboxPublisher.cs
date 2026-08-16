using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>
/// The line most recently accepted by the wire, and how long ago the wire accepted it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves or neither, which is the whole reason this is a type and not two properties.</b> A
/// caller that read <see cref="VrcChatboxPublisher.LastSentText"/> and then asked separately how long ago
/// it went out would be reading two moments: a send landing between the two calls gives it the new text
/// with the old text's age, or the old text with an age of nothing. That reads on screen as a line that
/// has been up for nine seconds suddenly claiming it just arrived, which is exactly the sort of small,
/// unreproducible wrongness nobody ever files a bug about.
/// </para>
/// <para>
/// <b><see cref="Since"/> is an elapsed span, not a timestamp, and that is deliberate.</b> The reader is a
/// WebView on the other side of a loopback hop with its own clock; handing it an absolute instant would
/// make every "4s ago" on screen a subtraction between two clocks that are not the same clock. An elapsed
/// span measured entirely inside the host is already the answer, and it is the answer in the one frame of
/// reference where the send actually happened.
/// </para>
/// </remarks>
/// <param name="Text">The text on the wire. Never null — no send means no <see cref="ChatboxLastSend"/> at all.</param>
/// <param name="Since">
/// How long since the wire accepted it, from the publisher's own <see cref="TimeProvider"/>. Grows between
/// reads; a reader polling once a second is seeing a number up to a second stale, which is well inside what
/// "a moment ago" can carry.
/// </param>
public readonly record struct ChatboxLastSend(string Text, TimeSpan Since);

/// <summary>
/// Decides whether a composition needs to go on the wire at all, and makes sure an unchanged one goes
/// out anyway before it disappears.
/// </summary>
/// <remarks>
/// <para>
/// <b>P4 — the 20-second resend is NECESSARY, not nice-to-have, and the primary source says why.</b>
/// <c>wiki.vrchat.com/wiki/Chatbox</c> documents that chatbox display duration is <b>2 to 60 seconds and
/// is a PER-VIEWER CLIENT SETTING</b>. That is why no implementation anywhere has a fixed timeout
/// constant, and it is why a static composition <i>silently vanishes</i> for viewers at the short end of
/// that range while remaining visible for viewers at the long end. <b>The sender can never know the true
/// display duration for any given audience member.</b> A dedupe-only egress path therefore ships a bug
/// that its author cannot see and its users cannot report precisely: "sometimes it just stops showing".
/// One message per twenty seconds buys that away.
/// </para>
/// <para>
/// <b>Forced resend on world or avatar change</b> (§12.2 step 7, §12.7) covers the other case: the client
/// clears the box outright, so the content is gone regardless of how recently it was sent. Avatar changes
/// are picked up automatically from <see cref="VrcAvatarEpoch"/>; a world change has no representation in
/// this assembly, so the host calls <see cref="ForceResend"/>.
/// </para>
/// <para>
/// <b>Recompose rate and send rate are decoupled</b> (§12.7). The composer may call
/// <see cref="PublishAsync"/> at 2–10 Hz; identical text is suppressed here, and only what survives
/// reaches the 1500 ms cadence gate inside <see cref="IVrcEgress"/>. Recomposing at 10 Hz costs nothing
/// on the wire.
/// </para>
/// <para>
/// The clock is pulled, not pushed: there is no timer thread. The composer is already calling at 2–10 Hz,
/// so the resend decision is made on the next call after the interval elapses — which also makes every
/// case here testable without waiting twenty real seconds.
/// </para>
/// </remarks>
public sealed class VrcChatboxPublisher : IDisposable
{
    private readonly IVrcEgress _egress;
    private readonly VrcAvatarEpoch? _avatarEpoch;
    private readonly TimeSpan _unchangedResend;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    private string? _lastSentText;
    private long _lastSentTimestamp;
    private bool _forced;
    private bool _disposed;

    /// <param name="egress">The only way out. Every send still passes its full gate pipeline.</param>
    /// <param name="avatarEpoch">Optional. When supplied, an avatar swap forces the next send.</param>
    /// <param name="unchangedResend">Defaults to <see cref="ChatboxCadence.UnchangedResend"/>.</param>
    /// <param name="timeProvider">Injected so the heartbeat is testable in microseconds, not minutes.</param>
    public VrcChatboxPublisher(
        IVrcEgress egress,
        VrcAvatarEpoch? avatarEpoch = null,
        TimeSpan? unchangedResend = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(egress);

        var interval = unchangedResend ?? ChatboxCadence.UnchangedResend;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unchangedResend), interval, "The unchanged-resend interval must be positive.");
        }

        _egress = egress;
        _avatarEpoch = avatarEpoch;
        _unchangedResend = interval;
        _time = timeProvider ?? TimeProvider.System;

        if (_avatarEpoch is not null)
        {
            _avatarEpoch.Invalidated += OnAvatarInvalidated;
        }
    }

    /// <summary>The text most recently accepted by the wire, or null before the first send.</summary>
    public string? LastSentText
    {
        get
        {
            lock (_gate)
            {
                return _lastSentText;
            }
        }
    }

    /// <summary>
    /// What is on the wire and how long it has been there, or null before this session's first send.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Null is a real answer and not a missing one.</b> Nothing has gone out yet, which a screen has to
    /// be able to say — "nothing sent yet" and "sent a moment ago" are different facts about a fresh launch,
    /// and collapsing them into a zero would have a just-opened app claim it had spoken.
    /// </para>
    /// <para>
    /// <b>It answers for every writer, not just the composer.</b> A line somebody typed and a line the
    /// passive display offered both come through <see cref="PublishAsync"/>, so this reports whatever is
    /// actually above the player's head. That is the question a person watching a status readout is asking,
    /// and answering it from the composer alone would show an empty box during the several seconds a
    /// person's own message is holding the display.
    /// </para>
    /// </remarks>
    public ChatboxLastSend? LastSend
    {
        get
        {
            lock (_gate)
            {
                return _lastSentText is null
                    ? null
                    : new ChatboxLastSend(_lastSentText, _time.GetElapsedTime(_lastSentTimestamp));
            }
        }
    }

    /// <summary>
    /// How long until unchanged content is resent. <see cref="TimeSpan.Zero"/> when a send is due now.
    /// </summary>
    public TimeSpan TimeUntilResend
    {
        get
        {
            lock (_gate)
            {
                if (_forced || _lastSentText is null)
                {
                    return TimeSpan.Zero;
                }

                var elapsed = _time.GetElapsedTime(_lastSentTimestamp);
                return elapsed >= _unchangedResend ? TimeSpan.Zero : _unchangedResend - elapsed;
            }
        }
    }

    /// <summary>
    /// Sends <paramref name="message"/> if it is new, if the heartbeat is due, or if a resend was forced.
    /// </summary>
    /// <returns>
    /// The egress result when something was attempted. When the send was suppressed as unchanged, a
    /// result with <c>Dispatched = false</c> and <see cref="ReasonCode.None"/> — no gate blocked it and
    /// nothing failed; there was simply nothing to do.
    /// </returns>
    /// <remarks>
    /// <see cref="ReasonCode.None"/> is the honest answer and also a gap: the vocabulary has no
    /// <c>Unchanged</c> member, and every alternative asserts something false —
    /// <see cref="ReasonCode.Ok"/> would claim a send happened, <see cref="ReasonCode.RateLimited"/> would
    /// blame the cadence gate that was never consulted.
    /// </remarks>
    public async ValueTask<EgressResult> PublishAsync(ComposedMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var text = message.Text ?? string.Empty;

        lock (_gate)
        {
            if (!ShouldSend(text))
            {
                return new EgressResult(false, ReasonCode.None, Guid.Empty, "unchanged");
            }
        }

        var result = await _egress.SendChatboxAsync(message, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            if (result.Dispatched)
            {
                _lastSentText = text;
                _lastSentTimestamp = _time.GetTimestamp();

                // Cleared only on a successful send. A forced resend that the cadence gate rejected has
                // not happened yet, and dropping the flag here would lose the world change entirely.
                _forced = false;
            }
        }

        return result;
    }

    /// <summary>
    /// Makes the next <see cref="PublishAsync"/> send even if nothing changed.
    /// </summary>
    /// <remarks>
    /// For a world change, which this assembly cannot observe: <see cref="IWorldPolicy"/> answers only
    /// whether the current world is muted, not which world it is. The host that knows calls this.
    /// </remarks>
    public void ForceResend()
    {
        lock (_gate)
        {
            _forced = true;
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

        if (_avatarEpoch is not null)
        {
            _avatarEpoch.Invalidated -= OnAvatarInvalidated;
        }
    }

    private bool ShouldSend(string text)
    {
        if (_forced || _lastSentText is null)
        {
            return true;
        }

        if (!string.Equals(_lastSentText, text, StringComparison.Ordinal))
        {
            return true;
        }

        return _time.GetElapsedTime(_lastSentTimestamp) >= _unchangedResend;
    }

    private void OnAvatarInvalidated(VrcAvatarInvalidated change) => ForceResend();
}
