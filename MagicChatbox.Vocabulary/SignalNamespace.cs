namespace MagicChatbox.Vocabulary;

/// <summary>
/// The first segment of every signal key. A small closed set, extended only by a deliberate decision.
/// </summary>
/// <remarks>
/// The rejected alternative was letting each source own a top-level prefix (<c>music.title</c>). A
/// source id is user-installable, so that prefix set is unbounded — and an unbounded namespace turns
/// every <c>switch</c> over this enum into a partial function that silently falls through instead of
/// failing to compile. Confining every third-party fact to <c>module.&lt;sourceId&gt;.&lt;path&gt;</c>
/// is what buys the compiler as the guard.
/// <para>
/// <c>Input</c> is local input hardware — MIDI, gamepads, controllers whose state we genuinely
/// observe. It is <b>not</b> VRChat's <c>/input/*</c> address space, which is write-only, never echoed,
/// and therefore has no current value a cell could honestly assert (D11).
/// </para>
/// <para>
/// <b>This said "exactly five members, closed forever" and now has six.</b> The sixth was added when
/// VRChat's camera and dolly turned out to be read/write subsystems with real state and no honest home:
/// not the avatar, not local hardware, not the OS, not a third-party module, and not this application.
/// Recording the change rather than editing the claim away, because "closed forever" was load-bearing
/// and someone should be able to see what overturned it.
/// </para>
/// <para>
/// The bound that actually matters is unchanged: the set is <i>finite and ours</i>, so every switch over
/// it stays total. Adding a member is a compile-time event with a known cost — <see cref="SignalKey"/>'s
/// reserved-word match and <c>SignalStoreOptions.DefaultCellCaps</c> both have to gain an arm, and a
/// test asserts the second so a missing cap cannot mean "unbounded" by omission.
/// </para>
/// </remarks>
public enum SignalNamespace : byte
{
    /// <summary>Avatar parameters and avatar-level VRChat facts: <c>avatar.param.hue</c>, <c>avatar.id</c>.</summary>
    Avatar = 0,

    /// <summary>Local input devices we read: <c>input.midi.cc.14</c>. Never VRChat's <c>/input/*</c>.</summary>
    Input = 1,

    /// <summary>OS and machine vitals: <c>system.window.title</c>, <c>system.cpu.load</c>.</summary>
    System = 2,

    /// <summary>Everything a module or integration produces: <c>module.music.title</c>.</summary>
    Module = 3,

    /// <summary>First-party app subsystems: <c>app.speech.last</c>, <c>app.assistant.narration</c>.</summary>
    App = 4,

    /// <summary>
    /// VRChat subsystems that are not the avatar: <c>vrc.camera.zoom</c>, <c>vrc.dolly.playing</c>.
    /// </summary>
    /// <remarks>
    /// Everything VRChat reports that outlives an avatar change and is not local hardware. The camera
    /// is the case that forced it: 36 addresses, most of them read/write with documented ranges, which
    /// makes them state rather than commands — and state needs somewhere to live. <c>avatar.</c> would
    /// have meant the swap eviction wiped the camera settings, which is plainly wrong.
    /// </remarks>
    Vrc = 5,
}
