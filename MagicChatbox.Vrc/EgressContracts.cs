using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>Text the composer has already fitted to VRChat's chatbox limit.</summary>
/// <param name="Text">The rendered line. Must already fit; egress re-checks as belt and braces.</param>
/// <param name="SourceSummary">
/// Which sources contributed, for the audit ledger. Not sent on the wire.
/// </param>
public readonly record struct ComposedMessage(string Text, string? SourceSummary = null);

/// <summary>
/// A value bound for an avatar parameter, expressed in v3's vocabulary rather than the wire's.
/// </summary>
/// <remarks>
/// This type exists so that <c>Vrc</c>'s public surface names no <c>Osc</c> type. That is what lets
/// <c>Core</c> drop its reference to <c>Osc</c> entirely (D5, part 2) — a small one-time cost that buys
/// the compiler as the guard on the egress path.
/// <para>VRChat accepts only bool, int and float for avatar parameters.</para>
/// </remarks>
public readonly struct VrcParameterValue
{
    private readonly double _value;

    private VrcParameterValue(VrcParameterKind kind, double value)
    {
        Kind = kind;
        _value = value;
    }

    public VrcParameterKind Kind { get; }

    public static VrcParameterValue Bool(bool v) => new(VrcParameterKind.Bool, v ? 1 : 0);

    public static VrcParameterValue Int(int v) => new(VrcParameterKind.Int, v);

    public static VrcParameterValue Float(float v) => new(VrcParameterKind.Float, v);

    public bool AsBool() => _value != 0;

    public int AsInt() => (int)_value;

    public float AsFloat() => (float)_value;

    /// <summary>False for NaN or Infinity, which must never reach the wire.</summary>
    public bool IsFinite() => Kind != VrcParameterKind.Float || double.IsFinite(_value);
}

/// <summary>The three parameter kinds VRChat accepts.</summary>
public enum VrcParameterKind : byte
{
    Bool,
    Int,
    Float,
}

/// <summary>
/// A VRChat movement or action input. Write-only, never echoed, and deliberately never a kernel signal.
/// </summary>
/// <remarks>
/// D11 — <c>/input/*</c> is not state. VRChat never echoes it and it has no readable value, so modelling
/// it as a cell would create a key that can be written and never confirmed. It is reached only through
/// <see cref="IVrcEgress.SendInputAsync"/>, and it appears in the ledger as an occurrence.
/// <para>
/// Axes are held until reset: sending 1 and never sending 0 walks the avatar into a wall forever.
/// </para>
/// </remarks>
public enum VrcInput : byte
{
    // Axes — float, -1 to 1. Held until reset: send 1 and never 0 and the avatar walks forever.
    Vertical,
    Horizontal,
    LookHorizontal,
    LookVertical,
    MoveHoldFB,
    SpinHoldCwCcw,
    SpinHoldUD,
    SpinHoldLR,

    // Buttons — pressed and released. VRChat performs no edge detection, so the sender owns both.
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    LookLeft,
    LookRight,
    Jump,
    Run,
    ComfortLeft,
    ComfortRight,
    GrabLeft,
    GrabRight,
    UseLeft,
    UseRight,
    DropLeft,
    DropRight,
    ToggleSitStand,
    AFKToggle,
    PanicButton,
    QuickMenuToggleLeft,
    QuickMenuToggleRight,

    /// <summary>
    /// Microphone control, and the one input whose meaning depends on a setting we cannot read.
    /// </summary>
    /// <remarks>
    /// With VRChat's "Microphone Behaviour" toggle on, this is edge-triggered: false → true flips mute,
    /// and it must then be put back to false. <b>While it is held true the user's own controller and
    /// keyboard mute are blocked</b>, so a sender that latches it has locked them out of their own
    /// microphone. With the setting off it is push-to-talk instead — false is muted, true is unmuted —
    /// and holding is the correct behaviour. Nothing on the wire says which mode is active.
    /// </remarks>
    Voice,

    // The debug overlays, which VRChat's own documentation page omits entirely. The wiki lists all ten
    // and marks the first as present in the client's OSCQuery response, which is this codebase's
    // standing test for whether an address is real. They are the in-game debug panels, so a caller is
    // far more likely to want one of these than a PanicButton.
    ShowDebugInfo0,
    ShowDebugInfo1,
    ShowDebugInfo2,
    ShowDebugInfo3,
    ShowDebugInfo4,
    ShowDebugInfo5,
    ShowDebugInfo6,
    ShowDebugInfo7,
    ShowDebugInfo8,
    ShowDebugInfo9,
}

/// <summary>Whether an input is a held axis or a momentary button.</summary>
/// <remarks>
/// VRChat's documentation splits these and the difference is not cosmetic: an axis carries a float in
/// -1..1 and stays where it was put, while a button is pressed and released and <b>must be released
/// before it can fire again</b> — VRChat does no edge detection of its own. Modelling both as "a float"
/// makes <c>Jump = 0.5</c> expressible, which means nothing, and makes "press" and "hold" look like the
/// same call.
/// </remarks>
public enum VrcInputKind : byte
{
    /// <summary>A float in -1..1 that holds its value until something sets it back to 0.</summary>
    Axis,

    /// <summary>A boolean that must be released before it can be pressed again.</summary>
    Button,
}

/// <summary>
/// What has to be true before an input can do anything, beyond the datagram arriving.
/// </summary>
/// <remarks>
/// <b>Every one of these dispatches happily and does nothing when unmet.</b> <c>/input/*</c> is
/// write-only and never echoed, so a caller gets <c>Ok</c> either way and the only symptom is an avatar
/// that did not move — which is exactly how a working sender gets reported as broken. Naming the
/// precondition is what lets the layer above refuse, or at least say why, instead of dispatching into
/// nothing.
/// </remarks>
public enum VrcInputRequirement : byte
{
    /// <summary>Works in both modes, in every world. Nothing to check.</summary>
    None,

    /// <summary>VR mode only. Inert in Desktop, and detectable — <c>VRMode</c> says which you are in.</summary>
    VrOnly,

    /// <summary>
    /// The world has to allow it, and nothing on the wire reports whether it does.
    /// </summary>
    /// <remarks>
    /// Jump is the sharp case: a world's jump impulse defaults to 0, so jumping is <i>off</i> unless the
    /// world deliberately enables it. Undetectable from here — the honest move is to say so, not to
    /// refuse.
    /// </remarks>
    WorldGated,

    /// <summary>
    /// Only the world's creator can use it, unless they enabled World Debugging on the website.
    /// </summary>
    CreatorGated,
}

/// <summary>Which of VRChat's two input shapes each address is.</summary>
/// <remarks>
/// <c>UseAxisRight</c> and <c>GrabAxisRight</c> are deliberately absent from <see cref="VrcInput"/>.
/// VRChat's documentation lists them, but its client does not advertise them in its OSCQuery tree —
/// the wiki records the discrepancy in its own errata. Sending to an address the client does not
/// declare is a write into nothing, so they are left out until the client says otherwise.
/// </remarks>
public static class VrcInputs
{
    /// <summary>The shape of one input.</summary>
    public static VrcInputKind KindOf(VrcInput input) => input <= VrcInput.SpinHoldLR
        ? VrcInputKind.Axis
        : VrcInputKind.Button;

    /// <summary>
    /// What has to be true before <paramref name="input"/> can do anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named rather than derived from position, unlike <see cref="KindOf"/>: these come from three
    /// different places in VRChat's own documentation and there is no ordering that encodes them.
    /// </para>
    /// <para>
    /// <b>Sources, because two of these disagree.</b> <c>ComfortLeft</c> and <c>ComfortRight</c> are
    /// marked "(VR only)" on the VRChat wiki's own table. The six hand actions are marked "VR Only" on
    /// VRChat's docs page while the wiki's table omits the annotation — the docs are taken as
    /// authoritative because Desktop has no controller to grab with. <c>ToggleSitStand</c> is
    /// undocumented on both, and is the Quick Menu's Sit/Stand tile, which that page marks "(VR)".
    /// </para>
    /// <para>
    /// The ⁑ debug views are the ones VRChat marks as creator-only. Five of the ten carry it;
    /// the other five (build/FPS, the log, asset bundles, user stats, network graphs) are open to anyone.
    /// </para>
    /// </remarks>
    public static VrcInputRequirement RequirementOf(VrcInput input) => input switch
    {
        VrcInput.ComfortLeft or VrcInput.ComfortRight => VrcInputRequirement.VrOnly,
        VrcInput.GrabLeft or VrcInput.GrabRight => VrcInputRequirement.VrOnly,
        VrcInput.UseLeft or VrcInput.UseRight => VrcInputRequirement.VrOnly,
        VrcInput.DropLeft or VrcInput.DropRight => VrcInputRequirement.VrOnly,
        VrcInput.ToggleSitStand => VrcInputRequirement.VrOnly,

        VrcInput.Jump or VrcInput.Run => VrcInputRequirement.WorldGated,

        VrcInput.ShowDebugInfo0 or VrcInput.ShowDebugInfo6 or VrcInput.ShowDebugInfo7
            or VrcInput.ShowDebugInfo8 or VrcInput.ShowDebugInfo9 => VrcInputRequirement.CreatorGated,

        _ => VrcInputRequirement.None,
    };
}

/// <summary>Three floats in VRChat's tracking space.</summary>
/// <remarks>
/// <para>
/// Unity's convention, which VRChat states outright: left-handed, +y up, 1.0 = one real-world metre.
/// Positions are <b>world-space</b>, not local. Rotations are euler angles in <b>degrees</b>, applied Z
/// then X then Y — so a sender that hands over radians, or that composes its quaternion in a different
/// order, produces limbs that point somewhere plausible and wrong rather than an error.
/// </para>
/// <para>
/// <b>Two of the rotation signs are the opposite of the usual convention</b>, which is the single most
/// likely way a port from another tracking source goes subtly wrong: +X is pitch <i>down</i>, +Y is yaw
/// right, and +Z is roll <i>counter-clockwise</i>. Position is unsurprising — +X right, +Y up, +Z
/// forward.
/// </para>
/// </remarks>
public readonly record struct VrcVector3(float X, float Y, float Z)
{
    /// <summary>False if any component is NaN or Infinity, which must never reach the wire.</summary>
    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

/// <summary>
/// Which tracking point a frame is for.
/// </summary>
/// <remarks>
/// An enum rather than an int, so <c>tracker 9</c> and <c>tracker 0</c> are unrepresentable instead of
/// merely rejected — the same rule <see cref="VrcAction"/> follows. VRChat supports eight, documented as
/// hip, chest, two feet, two knees and two elbows, and the numbering is 1-based on the wire.
/// <para>
/// <see cref="Head"/> is not a ninth tracker. It is an <i>alignment reference</i>: VRChat shifts the
/// whole OSC tracking space so this position meets the avatar's head bone, and lerps the space's yaw
/// towards this rotation. Sending it as though it were a limb is the most common way to end up fighting
/// your own IK.
/// </para>
/// <para>
/// Two details about the head that cost a sender an afternoon each. The position is the <b>root of the
/// head bone, not the eye or HMD position</b>, so passing the HMD pose straight through leaves a
/// constant offset. And only the <b>yaw</b> of its rotation is consumed — pitch and roll are read and
/// discarded, so a head that nods in your data does not nod in VRChat.
/// </para>
/// </remarks>
public enum VrcTrackerSlot : byte
{
    /// <summary>The alignment reference, not a body part. See the type's remarks.</summary>
    Head,

    Tracker1,
    Tracker2,
    Tracker3,
    Tracker4,
    Tracker5,
    Tracker6,
    Tracker7,
    Tracker8,
}

/// <summary>
/// One frame of tracking data. High rate, no echo, never a cell.
/// </summary>
/// <remarks>
/// <b>Both components are optional and that is load-bearing.</b> VRChat documents position-only,
/// rotation-only, both and neither as distinct, meaningful choices for the head reference — position
/// without yaw alignment is a supported setup — so a frame that always carried both could not express
/// what the protocol offers. A frame carrying neither is a caller mistake and is refused rather than
/// dispatched as nothing.
/// </remarks>
/// <param name="Slot">Which tracking point.</param>
/// <param name="Position">World-space position in metres, or null to leave it alone.</param>
/// <param name="Rotation">Euler angles in degrees, or null to leave it alone.</param>
public readonly record struct VrcTrackingFrame(
    VrcTrackerSlot Slot,
    VrcVector3? Position,
    VrcVector3? Rotation);

/// <summary>
/// Which shape of eye-look data a frame carries.
/// </summary>
/// <remarks>
/// <para>
/// VRChat offers six encodings of the same fact and expects you to pick <b>one</b>: "you can send data
/// to <i>one</i> of the addresses below depending on the format you'd like to send". They are
/// alternatives, not a set — a sender that streams two of them is asking VRChat to believe two things
/// about where the eyes point.
/// </para>
/// <para>
/// The two "Vec" forms differ only in whether length matters: <see cref="CenterVec"/> is normalised so
/// VRChat raycasts for the convergence distance, while <see cref="CenterVecFull"/> encodes that distance
/// in the vector's length. They carry the same three floats, so nothing can tell them apart for you —
/// sending a normalised vector to <see cref="CenterVecFull"/> pins convergence at exactly one metre.
/// </para>
/// <para>
/// <b>Positive pitch is down and positive yaw is right</b>, and the pitch half of that is inverted
/// relative to the vector forms on the same page, where +y is up. VRChat's own worked example encodes an
/// eye target 15° <i>up</i> as <c>-15.252</c>. Mixing the two conventions inside one sender is the
/// likeliest eye-tracking bug there is.
/// </para>
/// </remarks>
public enum VrcEyeGaze : byte
{
    /// <summary>Pitch and yaw in degrees. Convergence distance comes from an in-world raycast.</summary>
    CenterPitchYaw,

    /// <summary>Pitch, yaw and an explicit convergence distance in metres.</summary>
    CenterPitchYawDist,

    /// <summary>A normalised direction local to the HMD. Distance comes from a raycast.</summary>
    CenterVec,

    /// <summary>A direction local to the HMD whose length is the convergence distance in metres.</summary>
    CenterVecFull,

    /// <summary>Left pitch, left yaw, right pitch, right yaw — all in degrees.</summary>
    LeftRightPitchYaw,

    /// <summary>Left x, y, z then right x, y, z. Normalised, HMD-local.</summary>
    LeftRightVec,
}

/// <summary>How many floats each eye-look encoding carries, and where it goes.</summary>
/// <remarks>
/// The arity is part of the protocol, not a convenience: VRChat reads a fixed count per address, so a
/// frame with the wrong number of floats is not a smaller frame, it is a different message. Refusing on
/// count is the only check available — every value is a float and none of them are bounded, so nothing
/// else about the payload can be validated.
/// </remarks>
public static class VrcEyeGazes
{
    /// <summary>The number of floats <paramref name="gaze"/> must carry, exactly.</summary>
    public static int ArityOf(VrcEyeGaze gaze) => gaze switch
    {
        VrcEyeGaze.CenterPitchYaw => 2,
        VrcEyeGaze.CenterPitchYawDist => 3,
        VrcEyeGaze.CenterVec => 3,
        VrcEyeGaze.CenterVecFull => 3,
        VrcEyeGaze.LeftRightPitchYaw => 4,
        VrcEyeGaze.LeftRightVec => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(gaze)),
    };

    /// <summary>The OSC address, which is the enum name under <c>/tracking/eye/</c>.</summary>
    /// <remarks>VRChat's documentation states these addresses are case-sensitive.</remarks>
    public static string AddressOf(VrcEyeGaze gaze) => $"/tracking/eye/{gaze}";
}

/// <summary>VRChat's hard limits, in one place so no caller invents its own.</summary>
public static class VrcChatboxLimits
{
    /// <summary>
    /// 144 <b>characters</b>, and the distinction was a real bug.
    /// </summary>
    /// <remarks>
    /// This was measured in UTF-8 bytes, citing a wiki page that does not say that. Every source says
    /// characters: the OSC docs page ("limited to 144 characters"), the wiki's chatbox page, and the
    /// wiki's OSC table ("144 character, 9 line maximum; supports UTF-8 text"). The difference is not
    /// academic — under the byte reading a single emoji cost four of the budget and a line of Japanese
    /// cost three per character, so perfectly legal messages were refused at well under half the real
    /// limit.
    /// <para>
    /// Counted in text elements rather than <see cref="string.Length"/>, because a UTF-16 length counts
    /// an emoji as two and a flag or a skin-toned emoji as four or more. What VRChat renders as one
    /// character is one grapheme cluster.
    /// </para>
    /// </remarks>
    public const int MaxCharacters = 144;

    /// <summary>
    /// 9 lines, counting word wrap.
    /// </summary>
    /// <remarks>
    /// Not enforced here, and deliberately so: the wrap point depends on the reader's own client, so the
    /// only line count this side can honestly check is explicit newlines. Stated as a constant because a
    /// composer that fits text to the chatbox needs the number.
    /// </remarks>
    public const int MaxLines = 9;

    /// <summary>
    /// How many characters <paramref name="text"/> costs against the budget.
    /// </summary>
    /// <remarks>
    /// Grapheme clusters, via <see cref="System.Globalization.StringInfo"/>. "👍" is one, "👨‍👩‍👧" is one,
    /// and "é" is one whether it arrived precomposed or as e + combining accent.
    /// </remarks>
    public static int Measure(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }
}

/// <summary>How much of the chatbox budget the last send used.</summary>
public readonly record struct VrcChatboxBudget(int UsedCharacters, int MaxCharacters)
{
    public int RemainingCharacters => Math.Max(0, MaxCharacters - UsedCharacters);

    public static VrcChatboxBudget Empty => new(0, VrcChatboxLimits.MaxCharacters);
}

/// <summary>The outcome of one egress attempt.</summary>
/// <param name="Dispatched">True only if bytes actually reached the socket.</param>
/// <param name="Reason">Why. <see cref="ReasonCode.Ok"/> on success; the blocking gate otherwise.</param>
/// <param name="OperationId">Correlates this attempt with its occurrences in the ledger.</param>
/// <param name="Detail">Optional specifics — the matched profanity term, the socket error.</param>
public readonly record struct EgressResult(
    bool Dispatched, ReasonCode Reason, Guid OperationId, string? Detail = null);
