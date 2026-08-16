namespace MagicChatbox.Vrc;

/// <summary>
/// <b>The only way out of MagicChatbox to VRChat.</b>
/// </summary>
/// <remarks>
/// <para>
/// Every method runs the same fixed pipeline in the same order, with no way to skip a stage:
/// <b>SAFETY → CADENCE → DISPATCH → JOURNAL</b>.
/// </para>
/// <para>
/// <b>There is no raw send, and there is no address parameter anywhere on this interface.</b> That is
/// the entire design. v2 exposed an address-taking send in three places, guarded two of them, and
/// shipped three holes of one class — one of which (<c>ModuleRuntime.cs:604</c>) is still open today and
/// lets a module with ordinary OSC permission write straight to <c>/chatbox/input</c>, past the world
/// blacklist, the profanity filter and the cadence gate. Attaching safety to each caller has now failed
/// twice in this codebase. Removing the ability to be a caller has not.
/// </para>
/// <para>
/// Adding a method here is a protocol change and is reviewed as one. Adding an address parameter to an
/// existing method re-opens the hole and must be rejected in review.
/// </para>
/// </remarks>
public interface IVrcEgress
{
    /// <summary>
    /// <c>/chatbox/input</c> — <c>",sTT"</c>: text, sendImmediately, playNotificationSfx. 144 characters
    /// max, measured by <see cref="VrcChatboxLimits.Measure"/>; the wiki's own table says
    /// "144 character, 9 line maximum; supports UTF-8 text".
    /// </summary>
    ValueTask<EgressResult> SendChatboxAsync(ComposedMessage message, CancellationToken cancellationToken);

    /// <summary><c>/chatbox/typing</c> — one bool.</summary>
    ValueTask<EgressResult> SetTypingAsync(bool typing, CancellationToken cancellationToken);

    /// <summary><c>/avatar/parameters/{name}</c> — bool, int or float only.</summary>
    ValueTask<EgressResult> SetAvatarParameterAsync(
        string name, VrcParameterValue value, CancellationToken cancellationToken);

    /// <summary>
    /// <c>/avatar/eyeheight</c> — metres.
    /// </summary>
    /// <remarks>
    /// P8: VRChat echoes the <i>requested</i> value first and then a second event carrying the
    /// Udon-<i>enforced</i> value. Read-back-to-verify is provably wrong for this address — the first
    /// echo will agree with you and the second will not.
    /// </remarks>
    ValueTask<EgressResult> SetEyeHeightAsync(float metres, CancellationToken cancellationToken);

    /// <summary>
    /// <c>/input/{name}</c> — write-only, never echoed, never a kernel signal (D11).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Axes are held until reset. Send 1 and never 0 and the avatar walks forever. Buttons must be
    /// released before they can fire again — VRChat performs no edge detection, so two presses with no
    /// release between them are one press. <see cref="VrcInputs.KindOf"/> says which an address is, and
    /// the wire type tag follows from it.
    /// </para>
    /// <para>
    /// <b>Nothing releases these on your behalf.</b> Neither an avatar change nor a transport drop
    /// clears a held input, because the state lives in VRChat rather than in any cell here. A caller
    /// that holds an axis owns putting it back.
    /// </para>
    /// </remarks>
    ValueTask<EgressResult> SendInputAsync(VrcInput input, float value, CancellationToken cancellationToken);

    /// <summary>
    /// <c>/tracking/trackers/{slot}/position</c> and <c>/rotation</c> — high rate, no echo, no cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One frame is <b>up to two datagrams</b>, because VRChat splits position and rotation across two
    /// addresses and lets you send either alone. The result is true only if every datagram the frame
    /// asked for left; there is no partial success to report, since both go to the same socket.
    /// </para>
    /// <para>
    /// VRChat's own guidance is that fewer trackers is often better — feet and hips alone let its IK
    /// compensate for drift that eight points would fight. Sending all eight is a choice to make once
    /// the sender's absolute accuracy has earned it.
    /// </para>
    /// </remarks>
    ValueTask<EgressResult> SendTrackingAsync(VrcTrackingFrame frame, CancellationToken cancellationToken);

    /// <summary>
    /// <c>/tracking/eye/EyesClosedAmount</c> — 0 open, 1 shut. Both eyes together.
    /// </summary>
    /// <remarks>
    /// Per-eye winking is not expressible: VRChat currently drives both eyelids from this single value
    /// and documents separate control as a future addition. Eye-look and eyelids time out
    /// <i>independently</i> after ten seconds without data, each reverting to VRChat's own auto-blink or
    /// auto-look — so a sender that stops sending has not frozen the eyes, it has handed them back.
    /// </remarks>
    ValueTask<EgressResult> SetEyesClosedAsync(float amount, CancellationToken cancellationToken);

    /// <summary>
    /// One of <c>/tracking/eye/*</c>'s six eye-look encodings.
    /// </summary>
    /// <remarks>
    /// <b>Pick one encoding and stay on it.</b> VRChat treats these as alternatives rather than as a set;
    /// streaming two is asking it to believe two things about where the eyes point. Nothing here can
    /// enforce that across calls — it is a property of a session, not of a message — so it is stated
    /// where a caller will read it.
    /// </remarks>
    /// <param name="gaze">Which encoding, which also fixes how many floats are required.</param>
    /// <param name="values">
    /// Exactly <see cref="VrcEyeGazes.ArityOf"/> floats. A different count is refused rather than padded
    /// or truncated: VRChat reads a fixed count per address, so the wrong number is a different message.
    /// </param>
    ValueTask<EgressResult> SendEyeGazeAsync(
        VrcEyeGaze gaze, ReadOnlyMemory<float> values, CancellationToken cancellationToken);

    /// <summary>
    /// One of VRChat's camera or dolly addresses, by the key it reports on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Takes a <b>kernel key</b> rather than an address, which is the same rule the rest of this
    /// interface follows: you name the thing you want changed, not where to send bytes. The key is
    /// resolved through <see cref="VrcSubsystems"/>, so an address VRChat does not advertise cannot be
    /// reached from here at all.
    /// </para>
    /// <para>
    /// Unlike an avatar parameter this is <b>not</b> correlated for an echo. VRChat does report these
    /// back, but it also reports them when the user moves a slider in-game, so an echo confirms only
    /// that the value is now that — never that it was this write that made it so. The cell tells you
    /// where the camera ended up; the result tells you whether the datagram left.
    /// </para>
    /// </remarks>
    /// <param name="key">A key from <see cref="VrcSubsystems"/>, e.g. <c>vrc.camera.zoom</c>.</param>
    /// <param name="value">Coerced to the address's declared kind, and refused if it will not convert.</param>
    ValueTask<EgressResult> SetSubsystemAsync(string key, VrcParameterValue value, CancellationToken cancellationToken);

    /// <summary>
    /// A write-only camera or dolly action: capture, close, import a path.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SetSubsystemAsync"/> because these have no key and no readable state —
    /// there is nothing to observe afterwards, which is exactly why they are not cells.
    /// </remarks>
    /// <param name="action">Which action. An enum, so no address can be composed by a caller.</param>
    /// <param name="argument">The payload, or null for a bare trigger.</param>
    ValueTask<EgressResult> SendSubsystemActionAsync(
        VrcAction action, VrcParameterValue? argument, CancellationToken cancellationToken);

    /// <summary>Budget consumed by the most recent chatbox send.</summary>
    VrcChatboxBudget Budget { get; }
}
