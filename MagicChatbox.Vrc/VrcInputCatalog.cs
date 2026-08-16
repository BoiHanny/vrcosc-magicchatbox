using System.Collections.Immutable;

namespace MagicChatbox.Vrc;

/// <summary>
/// How a control behaves under a finger, which is not the same question as what shape the address is.
/// </summary>
/// <remarks>
/// <see cref="VrcInputs.KindOf"/> answers the protocol's question — axis or button — and that is all the
/// wire knows. This answers the product's: of the buttons, which ones mean something <i>while held</i>
/// (walking, push-to-talk), which fire an event (jumping, opening a menu), and which one latches because
/// it is a modifier nobody can hold and press something else with at the same time. Flattening the three
/// is how this surface goes wrong: a momentary <c>Run</c> is a button that genuinely cannot do anything,
/// and a <c>Jump</c> pressed and never released fires once and then looks broken, because VRChat performs
/// no edge detection of its own.
/// </remarks>
public enum VrcInputShape : byte
{
    /// <summary>Down while the pointer or key is, and the release is the half that matters.</summary>
    Hold,

    /// <summary>One press and one release, both sent by the host so the release cannot be lost.</summary>
    Tap,

    /// <summary>A float in −1..1 that VRChat holds where it was put, so the control springs back.</summary>
    Axis,

    /// <summary>Latches on until it is turned off. Only a modifier wants this — see <c>Run</c>.</summary>
    Sticky,
}

/// <summary>
/// One VRChat input as something a person reads, rather than as an address.
/// </summary>
/// <param name="Input">The address, typed. The only field the wire itself knows about.</param>
/// <param name="Group">What you are trying to do. See <see cref="VrcInputCatalog"/> for the group names.</param>
/// <param name="Label">What to call it. Never the enum name where a real name exists.</param>
/// <param name="Icon">A Material Symbols ligature, which is the icon vocabulary the frontend already reads.</param>
/// <param name="Shape">How it behaves under a finger.</param>
/// <param name="VrOnly">
/// True when VRChat performs it only in VR. Carried as a bool beside <see cref="VrcInputs.RequirementOf"/>
/// rather than instead of it, because a renderer needs a badge and a gate needs the reason.
/// </param>
/// <param name="Modifier">
/// Why this does nothing on its own, or null. Present for exactly one input and that is the point of the
/// field: <c>Run</c> is VRChat's shift key, and a control that changes what <i>another</i> control does is
/// indistinguishable from a broken one unless something says so.
/// </param>
/// <param name="Hint">What a person needs told before pressing it, or null.</param>
public sealed record VrcInputDescriptor(
    VrcInput Input,
    string Group,
    string Label,
    string Icon,
    VrcInputShape Shape,
    bool VrOnly,
    string? Modifier,
    string? Hint);

/// <summary>
/// Everything human-facing about VRChat's inputs, in one table.
/// </summary>
/// <remarks>
/// <para>
/// <b>This table existed three times before it existed once.</b> The grouping, the labels, the icons, the
/// shapes and the hard-won hints lived only in <c>ControlsPage.tsx</c>'s <c>GROUPS</c> const — a React
/// component — while the action catalog had grown its own copy of the labels and the warnings, and the
/// roadmap had a third port planned for the Rules screen's locomotion list. The wire knows none of it:
/// <see cref="VrcInputs.KindOf"/> and <see cref="VrcInputs.RequirementOf"/> answer shape and precondition
/// and nothing else, because that is all VRChat documents. So the knowledge moves here, one layer below
/// everything that needs it, and the three consumers render it rather than restating it. Getting a hint
/// wrong now propagates to three screens instead of three tables quietly disagreeing.
/// </para>
/// <para>
/// <b>Why this is in <c>Vrc</c> and not in <c>Api</c>.</b> A rule firing "press Jump" needs the same label
/// and the same warning a person clicking Jump needs, and the rule engine lives in <c>Core</c>, which may
/// name <c>Vrc</c> and may not name <c>Api</c>. Putting the table beside <see cref="VrcInput"/> itself is
/// what lets the action catalog read it without an interface existing solely to cross a boundary.
/// </para>
/// <para>
/// <b>Thirty inputs, thirty-one rows, and the duplicate is deliberate.</b> <see cref="VrcInput.Voice"/>
/// appears twice — once as <i>Toggle mute</i> and once as <i>Hold to talk</i> — because VRChat reads that
/// one address two ways depending on its Microphone Behaviour setting and <b>nothing on the wire says
/// which</b>. Offering both and saying so beats guessing, since guessing wrong leaves someone muted or
/// broadcasting. So <see cref="All"/> is a list rather than a map, and a lookup by input takes the first.
/// </para>
/// <para>
/// <b>The ten debug overlays are absent from <see cref="All"/> on purpose, not by omission</b> (VRC-10).
/// Nothing in the research says anyone wants <c>/input/ShowDebugInfo3</c> as a manual button, and this list
/// is what a screen renders. They remain reachable as authored actions — <see cref="LabelFor"/> and
/// <see cref="NoteFor"/> answer for all forty members, which is the seam that puts them back in one line
/// if a support request ever asks for them.
/// </para>
/// </remarks>
public static class VrcInputCatalog
{
    /// <summary>Walking, running, jumping — the group a stuck player reaches for first.</summary>
    public const string MoveGroup = "Move";

    /// <summary>Turning and pitching. Holds the residual surface's one surviving axis.</summary>
    public const string LookGroup = "Look";

    /// <summary>The safety and social reflex. Nobody should have to author a rule to mute themselves.</summary>
    public const string MicrophoneGroup = "Microphone";

    /// <summary>Low frequency, high urgency. Safe mode is an emergency control.</summary>
    public const string MenusGroup = "Menus and safety";

    /// <summary>The raw locomotion axes. Duplicates <see cref="MoveGroup"/>'s buttons by design.</summary>
    public const string AnalogueGroup = "Analogue";

    /// <summary>Grab, use and drop. All six are VR only.</summary>
    public const string HandsGroup = "Hands";

    /// <summary>Manipulating whatever is already in your hand.</summary>
    public const string HeldObjectsGroup = "Held objects";

    /// <summary>
    /// The groups the residual Controls page renders, in order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the cut, and it is drawn by frequency and by reachability rather than arbitrarily.</b>
    /// Move, Look, Microphone and Menus-and-safety are the things a person wants to do <i>right now</i>
    /// from a desk: fumbling out of geometry, muting, opening the quick menu, hitting Safe Mode. What is
    /// left out is left out for a stated reason — Analogue's Walk, Strafe and Turn are the same motions as
    /// Move and Look's buttons expressed a second way on one page, and Hands and Held objects are VR-only
    /// manipulations nobody reaches for a mouse to perform. Those become authored actions, where an axis is
    /// exactly the right shape ("nudge forward 0.3 for 200 ms when X") and a manual page is not.
    /// </para>
    /// <para>
    /// <b><see cref="VrcInput.LookVertical"/> stays, and refusal #8 does not apply to it.</b> Pitch is the
    /// one axis with no discrete equivalent anywhere: Turn, Walk and Strafe each have a button pair in
    /// <see cref="MoveGroup"/> or <see cref="LookGroup"/>, and there is no <c>LookUp</c> or <c>LookDown</c>
    /// member of <see cref="VrcInput"/> because VRChat does not offer one. Cutting it on the duplicate-front-door
    /// logic that justifies cutting the other three would remove the only way to look up manually at all.
    /// </para>
    /// <para>
    /// The page filters on these names, so a rename here empties a group there with nothing failing.
    /// <c>VrcInputCatalogTests</c> pins the spellings for that reason, and names the file that reads them.
    /// </para>
    /// </remarks>
    public static readonly ImmutableArray<string> ResidualGroups =
        [MoveGroup, LookGroup, MicrophoneGroup, MenusGroup];

    /// <summary>Every input a screen can offer as a button, in the order the groups read.</summary>
    public static readonly ImmutableArray<VrcInputDescriptor> All = Build();

    private static readonly Dictionary<VrcInput, VrcInputDescriptor> First = BuildIndex();

    /// <summary>The first row describing <paramref name="input"/>, or false for one that has none.</summary>
    /// <remarks>
    /// False for the ten debug overlays, which are excluded from <see cref="All"/>. A caller wanting a name
    /// for any of the forty wants <see cref="LabelFor"/>, which answers for all of them.
    /// </remarks>
    public static bool TryGet(VrcInput input, out VrcInputDescriptor descriptor) =>
        First.TryGetValue(input, out descriptor!);

    /// <summary>
    /// What to call one input where a person will read it — all forty of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The thirty names the Controls page has always used, so the same control is never called two things
    /// in one application, plus the ten the page never exposed.
    /// </para>
    /// <para>
    /// <b><see cref="VrcInput.Voice"/> is "Microphone" here and is neither of its two row labels.</b> A row
    /// on a manual page is one <i>reading</i> of that address — a mute toggle, or push-to-talk — and which
    /// is right depends on a VRChat setting nothing reports. An action names the address, so it takes the
    /// address's name and the ambiguity travels in <see cref="NoteFor"/> where a warning belongs.
    /// </para>
    /// <para>
    /// The overlays are numbered rather than named: VRChat's wiki says five of the ten are the build and
    /// FPS panel, the log, asset bundles, user stats and the network graphs, and does not say which index
    /// is which — so a guessed label would be a confident lie where a number is merely terse. The fallback
    /// is the enum's own name, so a member added to <see cref="VrcInput"/> shows up as itself rather than
    /// as a wrong guess.
    /// </para>
    /// </remarks>
    public static string LabelFor(VrcInput input)
    {
        if (input == VrcInput.Voice)
        {
            return "Microphone";
        }

        if (First.TryGetValue(input, out var descriptor))
        {
            return descriptor.Label;
        }

        var name = input.ToString();
        return name.StartsWith(DebugPrefix, StringComparison.Ordinal)
            ? $"Debug overlay {name[DebugPrefix.Length..]}"
            : name;
    }

    /// <summary>
    /// What a person needs told before they <i>author</i> this input, or empty when there is nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The requirement comes from <see cref="VrcInputs.RequirementOf"/> rather than from a column here, so
    /// the picker's annotation is the same answer the egress layer would give rather than a copy of it.
    /// Every one of these dispatches happily and does nothing when its precondition is unmet — that is what
    /// makes an unannotated picker actively harmful.
    /// </para>
    /// <para>
    /// <b>Two inputs carry a second sentence, and it is deliberately not
    /// <see cref="VrcInputDescriptor.Modifier"/> or <see cref="VrcInputDescriptor.Hint"/>.</b> Those two
    /// fields instruct somebody pressing a button now — "turn it on, then hold Forward" — and this warns
    /// somebody writing a sentence that will run later, unattended, possibly while they are in a headset.
    /// The facts behind them are the same two facts, learned the same expensive way: <c>Run</c> does
    /// nothing whatsoever on its own, and <c>Voice</c> means two different things depending on a setting
    /// nothing on the wire reports, one of which locks the user out of their own microphone while it is
    /// held. Same file, two registers, no third table.
    /// </para>
    /// </remarks>
    public static string NoteFor(VrcInput input)
    {
        var requirement = VrcInputs.RequirementOf(input) switch
        {
            VrcInputRequirement.VrOnly => "VR only — in Desktop it is sent and nothing happens.",
            VrcInputRequirement.WorldGated =>
                "The world has to allow this, and nothing on the wire says whether it does.",
            VrcInputRequirement.CreatorGated =>
                "Only the world's creator can use this, unless they enabled World Debugging.",
            _ => "",
        };

        var extra = input switch
        {
            VrcInput.Run => "Scales walking speed while a movement control is also held, and does nothing alone.",
            VrcInput.Voice =>
                "Means two different things depending on VRChat's Microphone Behaviour setting: a mute "
                + "toggle, or push-to-talk. Holding it in toggle mode blocks your own mute controls.",
            _ => "",
        };

        return (requirement, extra) switch
        {
            ("", var e) => e,
            (var r, "") => r,
            var (r, e) => $"{r} {e}",
        };
    }

    private const string DebugPrefix = "ShowDebugInfo";

    private static Dictionary<VrcInput, VrcInputDescriptor> BuildIndex()
    {
        var index = new Dictionary<VrcInput, VrcInputDescriptor>();

        foreach (var descriptor in All)
        {
            // First wins, which only matters for Voice: its two rows are two readings of one address, and
            // a caller asking "what is this input" wants the address rather than either reading.
            index.TryAdd(descriptor.Input, descriptor);
        }

        return index;
    }

    private static ImmutableArray<VrcInputDescriptor> Build() =>
    [
        new(VrcInput.MoveForward, MoveGroup, "Forward", "keyboard_arrow_up", VrcInputShape.Hold, false, null, null),
        new(VrcInput.MoveBackward, MoveGroup, "Back", "keyboard_arrow_down", VrcInputShape.Hold, false, null, null),
        new(VrcInput.MoveLeft, MoveGroup, "Left", "keyboard_arrow_left", VrcInputShape.Hold, false, null, null),
        new(VrcInput.MoveRight, MoveGroup, "Right", "keyboard_arrow_right", VrcInputShape.Hold, false, null, null),

        // Latching, not momentary. Run is VRChat's shift key: it scales locomotion speed while a movement
        // input is active and does nothing at all on its own — and you cannot hold it and press Forward
        // with the same pointer. Turn it on, then move.
        new(
            VrcInput.Run, MoveGroup, "Run", "directions_run", VrcInputShape.Sticky, false,
            Modifier:
                "Turn it on, then hold Forward or Back. Run scales walking speed and does nothing alone — "
                + "and VRChat documents strafing as unaffected by it, so testing with Left or Right cannot work.",
            Hint: null),

        // Not a hedge. A world's jump impulse defaults to 0, so jumping is off unless the world deliberately
        // enables it — the standard VRCWorld prefab sets it, and a world built without one never has a jump
        // for this to trigger.
        new(
            VrcInput.Jump, MoveGroup, "Jump", "arrow_upward", VrcInputShape.Tap, false, null,
            Hint: "Off by default in VRChat — a world has to enable jumping. Press Space in-game: if that "
                + "does not jump either, the world has none."),

        // Not a chair. This is the Quick Menu's VR play-mode tile: it switches between seated and standing
        // tracking and shifts your viewpoint height. Naming it after the world's sit action is why pressing
        // it in Desktop reads as broken rather than as inapplicable.
        new(
            VrcInput.ToggleSitStand, MoveGroup, "Sit / stand", "height", VrcInputShape.Tap, VrOnly: true, null,
            Hint: "Switches VR play-mode between seated and standing, moving your viewpoint. Not the world "
                + "sit action."),

        new(VrcInput.LookLeft, LookGroup, "Turn left", "undo", VrcInputShape.Hold, false, null, null),
        new(VrcInput.LookRight, LookGroup, "Turn right", "redo", VrcInputShape.Hold, false, null, null),
        new(VrcInput.ComfortLeft, LookGroup, "Snap left", "rotate_left", VrcInputShape.Tap, VrOnly: true, null, null),
        new(VrcInput.ComfortRight, LookGroup, "Snap right", "rotate_right", VrcInputShape.Tap, VrOnly: true, null, null),

        // The one axis on a page that otherwise has none, and the reason ResidualGroups keeps Look. There is
        // no discrete vertical look anywhere in VRChat's protocol, so this is not a second way to do
        // something the buttons already do — it is the only way to do it by hand.
        new(
            VrcInput.LookVertical, LookGroup, "Pitch", "height", VrcInputShape.Axis, false, null,
            Hint: "1 up, −1 down. Springs back to 0, because VRChat holds an axis where you put it."),

        new(
            VrcInput.Voice, MicrophoneGroup, "Toggle mute", "mic", VrcInputShape.Tap, false, null,
            Hint: "Flips your mute, if VRChat's Microphone Behaviour setting is on."),
        new(
            VrcInput.Voice, MicrophoneGroup, "Hold to talk", "mic_none", VrcInputShape.Hold, false, null,
            Hint: "Push-to-talk, if VRChat's Microphone Behaviour setting is off."),

        new(VrcInput.QuickMenuToggleLeft, MenusGroup, "Quick menu (left)", "menu", VrcInputShape.Tap, false, null, null),
        new(VrcInput.QuickMenuToggleRight, MenusGroup, "Quick menu (right)", "menu_open", VrcInputShape.Tap, false, null, null),
        new(VrcInput.AFKToggle, MenusGroup, "AFK", "bedtime", VrcInputShape.Tap, false, null, null),
        new(
            VrcInput.PanicButton, MenusGroup, "Safe mode", "shield", VrcInputShape.Tap, false, null,
            Hint: "Turns on Safe Mode in VRChat."),

        new(
            VrcInput.Vertical, AnalogueGroup, "Walk", "swap_vert", VrcInputShape.Axis, false, null,
            Hint: "1 forward, −1 back."),
        new(
            VrcInput.Horizontal, AnalogueGroup, "Strafe", "swap_horiz", VrcInputShape.Axis, false, null,
            Hint: "1 right, −1 left."),
        new(
            VrcInput.LookHorizontal, AnalogueGroup, "Turn", "sync", VrcInputShape.Axis, false, null,
            Hint: "1 right, −1 left."),

        new(VrcInput.GrabLeft, HandsGroup, "Grab left", "back_hand", VrcInputShape.Hold, VrOnly: true, null, null),
        new(VrcInput.UseLeft, HandsGroup, "Use left", "touch_app", VrcInputShape.Tap, VrOnly: true, null, null),
        new(VrcInput.DropLeft, HandsGroup, "Drop left", "do_not_touch", VrcInputShape.Tap, VrOnly: true, null, null),
        new(VrcInput.GrabRight, HandsGroup, "Grab right", "back_hand", VrcInputShape.Hold, VrOnly: true, null, null),
        new(VrcInput.UseRight, HandsGroup, "Use right", "touch_app", VrcInputShape.Tap, VrOnly: true, null, null),
        new(VrcInput.DropRight, HandsGroup, "Drop right", "do_not_touch", VrcInputShape.Tap, VrOnly: true, null, null),

        // Named in full rather than as the page's "Spin" and "Tilt". A label under a group heading can
        // borrow the heading's context; a row in an action picker, sorted beside "Jump" and "Safe mode",
        // has nothing above it to say what is being spun.
        new(VrcInput.MoveHoldFB, HeldObjectsGroup, "Push / pull held object", "open_in_full", VrcInputShape.Axis, false, null, null),
        new(VrcInput.SpinHoldCwCcw, HeldObjectsGroup, "Spin held object", "refresh", VrcInputShape.Axis, false, null, null),
        new(VrcInput.SpinHoldUD, HeldObjectsGroup, "Tilt held object", "unfold_more", VrcInputShape.Axis, false, null, null),
        new(VrcInput.SpinHoldLR, HeldObjectsGroup, "Yaw held object", "unfold_more_double", VrcInputShape.Axis, false, null, null),
    ];
}
