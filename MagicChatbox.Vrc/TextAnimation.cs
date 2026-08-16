namespace MagicChatbox.Vrc;

/// <summary>
/// How the composer is allowed to move text that VRChat will render on other people's screens.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Blink</c> member and there is never going to be one, and that absence is the whole
/// point of this type existing before the composer does.</b> v2 shipped six text animations — Marquee,
/// Typewriter, Blink, Fade, EasedMarquee, CountUp — and the v3 ledger recommends carrying them forward
/// "as maths, not as WPF". Five of those six are carried here. Blink is not, because everything this
/// application animates is rendered above the user's head on screens belonging to people who never
/// installed it, never agreed to anything, and cannot set <c>prefers-reduced-motion</c> at us.
/// <c>frontend/src/design/tokens/base.css</c> honours that preference beautifully for our own user and
/// cannot reach anybody else.
/// </para>
/// <para>
/// <b>An absent member rather than a setting defaulted off.</b> A shipped dead end somebody can walk into
/// is worse than an absent feature, and this particular dead end has a medical consequence: photosensitive
/// epilepsy, vestibular disorders, migraine. A switch marked "unsafe" is still a switch, and the person it
/// would hurt is not the person holding it. There is deliberately no warning dialog either — either an
/// animation is safe to put on a stranger's screen or it is not in this enum.
/// </para>
/// <para>
/// <b>Landed now, while the composer is unbuilt, because the cost curve is the argument.</b> Today this is
/// an enum with five members and a guard. Once an animation set ships and somebody has built a look around
/// it, the same decision is a deprecation with angry users attached. <c>MotionSafetyGuards</c> in
/// <c>MagicChatbox.Architecture.Tests</c> fails the build if a sixth member appears here or if any enum
/// anywhere in <c>src/</c> declares a member called <c>Blink</c> — the pinned-members shape
/// <c>speech-ai-and-money.md</c> uses to hold its fault-tell strings closed, for the same stated reason:
/// without it, one unreviewed addition removes the rule entirely.
/// </para>
/// <para>
/// <b>The composer consumes this now.</b> <c>Core/Composition/TextAnimator.cs</c> is the pure function
/// that turns a kind, a line and a frame counter into the text for that frame, and
/// <c>Layer.Animation</c> and the timeline's <c>State.Animation</c> are where an author picks one. It is
/// still declared here, beside <see cref="ChatboxCadence"/>, because this assembly is the one that owns
/// what leaves for VRChat — the egress fence, the world gate, the profanity gate, the cadence — and motion
/// safety is a constraint of exactly that family rather than a rendering preference.
/// <para>
/// <b>Two of the five are declared and deliberately not rendered, which is worth knowing before adding a
/// sixth.</b> <see cref="Fade"/> asks for opacity and the chatbox is plain text with no alpha, so there is
/// nothing to fade; <see cref="CountUp"/> needs the previous value and the animator is pure by design,
/// with no channel for what the line said last frame. Both fall through to the plain, budget-capped line
/// rather than being quietly mapped onto some other motion — a substitute effect would be this
/// application putting something on a stranger's screen that nobody asked for, which is the same argument
/// that keeps <c>Blink</c> out. <c>TextAnimator</c>'s own remarks carry the detail.
/// </para>
/// </para>
/// </remarks>
public enum TextAnimationKind : byte
{
    /// <summary>The text does not move. The default, and the only kind that is safe at any rate.</summary>
    None = 0,

    /// <summary>Linear horizontal scroll for a line wider than the chatbox.</summary>
    Marquee = 1,

    /// <summary>A marquee that eases at each end instead of wrapping abruptly.</summary>
    EasedMarquee = 2,

    /// <summary>The line reveals a character at a time.</summary>
    Typewriter = 3,

    /// <summary>The line arrives and departs by opacity rather than by movement.</summary>
    Fade = 4,

    /// <summary>A number walks from its previous value to its current one.</summary>
    CountUp = 5,
}

/// <summary>
/// The floor under everything that moves, expressed as the one number VRChat actually lets us control.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a rate floor is the second half of <see cref="TextAnimationKind"/> and not a separate concern.</b>
/// Removing the blink member removes the animation *named* after flashing; it does not by itself make a
/// strobe unrepresentable. A marquee re-sent every 80 ms with a two-character step is a flashing image, and
/// nobody would have to intend it. The chatbox has exactly one clock — <see cref="ChatboxCadence"/> — so
/// putting a floor on that clock is what turns "we do not offer a blink" into "this application cannot emit
/// a strobe".
/// </para>
/// <para>
/// <b>400 ms, which is 2.5 Hz, against a seizure threshold that begins around 3 Hz and is most dangerous
/// between 15 and 25 Hz.</b> The shipped cadence is 1500 ms — 0.67 Hz, nearly four times slower again —
/// so this floor is not a constraint on any behaviour that exists today. It is a constraint on a future
/// tuning control: <see cref="ChatboxCadence"/>'s own remarks call the interval "tunable rather than
/// load-bearing", and this is the one direction in which that sentence is false. VRChat 2026.2.1's leaky
/// bucket might well permit faster; permission is not the question here.
/// </para>
/// <para>
/// <b>It is a floor on the cadence itself, not only on animated compositions, and that is stricter than
/// the accessibility design asked for.</b> Deliberately: there is one cadence and every composition rides
/// it, so a floor that lifted whenever the current line happened to be static would be a floor that moves
/// under a person who is watching. A rule that only holds sometimes is not the kind of rule this is.
/// </para>
/// </remarks>
public static class MotionSafety
{
    /// <summary>
    /// The fastest the chatbox may be driven, in milliseconds. See the type remarks for the arithmetic.
    /// </summary>
    public const int MinimumIntervalMs = 400;

    /// <summary>The fastest the chatbox may be driven, as a <see cref="TimeSpan"/>.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(MinimumIntervalMs);
}
