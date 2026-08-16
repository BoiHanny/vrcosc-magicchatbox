using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// The kinds of thing that can appear on the occurrence tape.
/// </summary>
/// <remarks>
/// All but one of these carry no cell at all — no value, no version, sometimes no key. Forcing them onto
/// the state tape's shape would produce a record with a nullable key, a nullable value, a nullable
/// version and a discriminator, which is precisely v2's <c>KernelEventEnvelope</c>, the type this
/// design exists to delete.
/// <para>
/// <see cref="DiscreteSignalChanged"/> is the one that <i>is</i> a state change, and it rides here as
/// well as on the state tape. That redundancy is conceded, with its cost stated: a discrete change
/// costs two publishes. It buys the ledger and the trigger engine exactly one input instead of two,
/// and discrete keys change at human rates.
/// </para>
/// <para>
/// <b>Members are appended and never renumbered.</b> The name crosses the channel boundary — the
/// Diagnostics log resolves it through a table keyed by both the name and the ordinal — so reordering
/// would relabel every historical row a client had already drawn.
/// </para>
/// </remarks>
public enum OccurrenceKind
{
    /// <summary>A change on a <see cref="Temperament.Discrete"/> key. Continuous changes never reach this tape.</summary>
    DiscreteSignalChanged,

    /// <summary>One all-or-nothing batch, carrying every transition it applied.</summary>
    BatchApplied,

    /// <summary>A cell was evicted — a module unloaded, an avatar swapped.</summary>
    SignalRemoved,

    /// <summary>Policy said no. Nothing was written.</summary>
    WriteRejected,

    /// <summary>The source could not produce a value. The cell keeps its last good reading.</summary>
    WriteFailed,

    /// <summary>A descriptor declaration was illegal and was not stored.</summary>
    DescriptorRejected,

    /// <summary>Something was asked of the outside world. Recorded by Vrc and Core, not by the store.</summary>
    CommandDispatched,

    /// <summary>The outside world confirmed it.</summary>
    CommandCompleted,

    /// <summary>The outside world refused it or never answered.</summary>
    CommandFailed,

    /// <summary>
    /// A sink threw three times running and was removed from the fan-out. D12: this is a user-visible
    /// failure whether or not it is reported, so it is reported.
    /// </summary>
    SinkEjected,

    /// <summary>
    /// A rule's trigger matched, everything held, and its actions were queued.
    /// </summary>
    /// <remarks>
    /// Safe to record unconditionally only because the engine's firing budget already bounds it: thirty
    /// fires in a rolling minute quarantines the rule, so one rule cannot spend more than thirty of this
    /// tape's 256 entries a minute however badly it is written. Everything else a rule concludes goes to
    /// its own decision ring instead — see <see cref="RuleSkipped"/>.
    /// </remarks>
    RuleFired,

    /// <summary>
    /// A rule's trigger matched and a gate said no — its guard, its cooldown, or the world outside.
    /// </summary>
    /// <remarks>
    /// <b>Recorded only for a rule the user is watching, and even then only on a doubling ladder.</b>
    /// This tape is a 256-entry ring sized on the argument that discrete keys change at human rates, and
    /// a rule skipping against a busy signal is not bounded by that — one of them would erase every other
    /// record in the application inside seconds, including the evidence of whatever it was skipping over.
    /// The engine's per-rule decision ring is the unconditional record; this is the door onto the shared
    /// tape, and <c>rules.watch</c> is what opens it.
    /// </remarks>
    RuleSkipped,

    /// <summary>
    /// One step of a rule's action list was refused or threw. The rest of the list still ran.
    /// </summary>
    /// <remarks>
    /// Unconditional, unlike <see cref="RuleSkipped"/>, because a refused effect is bounded by the fires
    /// that asked for it and because it is the one rule outcome a person cannot diagnose from anywhere
    /// else: the rule looks like it worked, and something above the avatar's head did not happen.
    /// </remarks>
    RuleActionFailed,

    /// <summary>
    /// A rule exceeded its firing budget and the engine turned it off.
    /// </summary>
    /// <remarks>
    /// Rare by construction — at most once per rule per minute — and the event a user most needs to find
    /// later, because "why did my rule stop" has no answer at all if the only record is a ring that has to
    /// be on screen to be read.
    /// </remarks>
    RuleQuarantined,

    /// <summary>
    /// A rule had something to say to the person running it, and this is the saying of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The only kind on this tape whose <c>Detail</c> is not a diagnosis the application produced.</b>
    /// Every other member records something that happened and leaves the tail to whatever explains it; here
    /// the tail <i>is</i> the payload — a sentence the user wrote, with its tokens resolved. Nothing parses
    /// it, which is the tape's existing rule for <c>Detail</c> and is exactly what this needs.
    /// </para>
    /// <para>
    /// <b>The tape is the delivery mechanism, not a record of a delivery that happened elsewhere.</b> A
    /// notification queue beside this one would be a second event pipeline for the one question this tape
    /// already answers — "what happened" — and the surfaces that read it (a toast, a bell, a rule's own
    /// history) would then disagree about what the application had said and when. Bounded by the engine's
    /// firing budget on the way in, exactly as <see cref="RuleFired"/> is, and coalesced by whatever draws
    /// it rather than by a second gate here.
    /// </para>
    /// </remarks>
    RuleNotified,

    /// <summary>
    /// A module's five-phase activation completed and it is now publishing.
    /// </summary>
    /// <remarks>
    /// <b>Recorded because deactivation evicts, which makes the store's silence ambiguous.</b> A module
    /// that is switched off leaves no cells and no descriptors behind — that is what stops a stale
    /// "now playing" sitting on the dashboard an hour later — so afterwards there is nothing anywhere
    /// except this line to say the module ever ran. Bounded by the host's dwell, which is two seconds by
    /// default, so a guard that flapped cannot spend this tape on a module going up and down.
    /// </remarks>
    ModuleActivated,

    /// <summary>
    /// A module stopped and everything it contributed was given back.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="ModuleActivated"/>, and the pair is what makes the eviction
    /// attributable: a <see cref="SignalRemoved"/> under <c>module.&lt;id&gt;.*</c> says the cells went,
    /// and this says which lifecycle decision took them. Recorded only for a module that genuinely
    /// started — an activation that rolled back before its own hook returned never ran, and reporting a
    /// stop for it would be the same double-counting v2's rollback produced when it called the second
    /// half of a pair whose first half had failed.
    /// </remarks>
    ModuleDeactivated,

    /// <summary>
    /// A module threw, in its tick or in its lifecycle, or was quarantined for doing it repeatedly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Distinct from <see cref="WriteFailed"/>, which is what this used to be filed under and is a
    /// claim about one cell.</b> A module whose foreground-window read throws has not failed to write
    /// anything — it never got as far as a value — and a notification surface reading the tape would
    /// have shown "Write failed" for a module that stopped, which is a sentence about the wrong subject.
    /// The reason carries which kind of stop it was: <see cref="ReasonCode.SourceFaulted"/> for a throw,
    /// <see cref="ReasonCode.SourceDisabled"/> for the quarantine that follows three of them.
    /// </para>
    /// <para>
    /// Safe to record unconditionally for the same reason <see cref="RuleFired"/> is: the host's fault
    /// budget already bounds it. Three faults inside a minute quarantine a module, so one module can put
    /// at most four lines on this 256-entry ring before it stops trying.
    /// </para>
    /// </remarks>
    ModuleFaulted,

    /// <summary>
    /// A rule's trigger matched on an avatar parameter while the avatar was still settling, and the
    /// engine held it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Distinct from <see cref="RuleSkipped"/>, and the difference is who to blame.</b> A skip is the
    /// rule's own sentence saying no — a guard, a cooldown — and the answer to "why didn't it fire" is
    /// something the author wrote. This is the host overruling a trigger that genuinely matched, for a
    /// reason nobody authored and nobody can see: for a few seconds after an avatar change VRChat streams
    /// hundreds of parameters at their defaults, every one of them an edge. Filing that under
    /// <see cref="RuleSkipped"/> would tell a person to go and look at a guard that was never consulted.
    /// </para>
    /// <para>
    /// <b>Recorded unconditionally, unlike <see cref="RuleSkipped"/>, and affordable because it is bounded
    /// by the swap rather than by the key.</b> The rule's decision ring folds every suppression inside one
    /// settling window onto a single line, and only the first one — the line whose <c>Repeats</c> is still
    /// zero — reaches this tape. So one rule spends one entry per avatar change however many hundreds of
    /// parameters arrived, and an avatar change is a human-rate event. That is what makes it safe to
    /// record without a watch, which matters: the entire failure this suppression exists to fix looks, to
    /// the user, exactly like the app being broken, and it must be findable afterwards rather than only
    /// while somebody happens to have the panel open.
    /// </para>
    /// </remarks>
    RuleSuppressed,

    /// <summary>
    /// A module was asked to do one of the verbs its manifest declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Recorded at the ask rather than at the outcome, because a command has no outcome to record.</b>
    /// A module answers a press by publishing, so what a person needs on the tape is the line that lets
    /// them join "I pressed Look now" to the cells that moved just after it — and if the module threw
    /// instead, <see cref="ModuleFaulted"/> lands immediately behind this one, which reads correctly as
    /// the two separate facts they are.
    /// </para>
    /// <para>
    /// <b>Safe to record unconditionally, and bounded by the press rather than by the module.</b>
    /// <c>ModuleHost.CommandBudget</c> caps one module at five a second before anything reaches the queue,
    /// and the only producer is a person's finger on a button. That is a human-rate event in the same
    /// sense an avatar change is, which is the test this 256-entry ring applies to every kind on it.
    /// </para>
    /// </remarks>
    ModuleCommanded,
}

/// <summary>
/// One thing that happened, in order, never coalesced.
/// </summary>
/// <param name="Seq">Global sequence number. Occurrences are ordered by this and nothing else.</param>
/// <param name="Kind">What sort of thing happened.</param>
/// <param name="Actor">Who caused it.</param>
/// <param name="Correlation">The operation, transaction and cause it belongs to.</param>
/// <param name="Timestamp">
/// Monotonic ticks, on the <c>TimeProvider</c> the tape was built with.
/// <para>
/// <b>A tick count and not a clock, and the difference is a whole epoch.</b> The origin is whatever the
/// platform's high-resolution counter happened to be at — process start on some, boot on others — so
/// dividing this by the frequency yields a duration since nothing in particular. Only
/// <see cref="IOccurrenceTape.ProjectUtc"/> turns it into an instant, because only the tape holds the
/// <c>(UtcNow, Timestamp)</c> pair it has to be measured against.
/// </para>
/// </param>
/// <param name="Transitions">
/// Every key this occurrence touched, with before and after. Empty for the kinds that touch no cell.
/// </param>
/// <param name="Reason">The machine-readable why.</param>
/// <param name="Detail">
/// The human-readable tail — an exception message, an offending value. Free text on purpose, and the
/// only thing the reason enum cannot carry. Never parsed.
/// </param>
public sealed record Occurrence(
    long Seq,
    OccurrenceKind Kind,
    KernelActor Actor,
    Correlation Correlation,
    long Timestamp,
    ImmutableArray<SignalTransition> Transitions,
    ReasonCode Reason,
    string? Detail)
{
    /// <summary>The first key this occurrence touched, or the default when it touched none.</summary>
    public SignalKey PrimaryKey => Transitions.IsDefaultOrEmpty ? default : Transitions[0].Key;
}

/// <summary>
/// Which occurrences a subscriber wants.
/// </summary>
/// <param name="Kinds">Empty means every kind.</param>
/// <param name="KeyPatterns">
/// Empty means every key, <i>including occurrences that name no key at all</i>. A non-empty list also
/// filters out keyless occurrences, because a subscriber that asked for specific keys did not ask for
/// whole-source failures.
/// </param>
public readonly record struct OccurrenceFilter(
    ImmutableArray<OccurrenceKind> Kinds,
    ImmutableArray<string> KeyPatterns)
{
    /// <summary>Everything.</summary>
    public static readonly OccurrenceFilter All = new(
        ImmutableArray<OccurrenceKind>.Empty, ImmutableArray<string>.Empty);

    /// <summary>Only the given kinds, over every key.</summary>
    public static OccurrenceFilter OfKinds(params OccurrenceKind[] kinds) =>
        new([.. kinds], ImmutableArray<string>.Empty);

    /// <summary>True when this filter admits <paramref name="occurrence"/>.</summary>
    public bool Admits(Occurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        if (!Kinds.IsDefaultOrEmpty && !Kinds.Contains(occurrence.Kind))
        {
            return false;
        }

        if (KeyPatterns.IsDefaultOrEmpty)
        {
            return true;
        }

        if (occurrence.Transitions.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var transition in occurrence.Transitions)
        {
            foreach (var pattern in KeyPatterns)
            {
                if (KeyPattern.Matches(pattern, transition.Key))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>Receives occurrences. Implemented only by kernel-owned mailboxes; must not block.</summary>
public interface IOccurrenceSink
{
    /// <summary>Called on the writer's thread, outside every stripe lock. Must not block.</summary>
    void OnOccurrence(Occurrence occurrence);
}

/// <summary>The occurrence tape: everything that happened, in order, with replay for reconnects.</summary>
public interface IOccurrenceTape
{
    /// <summary>Subscribes, filtered by grant and by an explicit filter.</summary>
    IDisposable Subscribe(IOccurrenceSink sink, GrantSet grants, OccurrenceFilter filter);

    /// <summary>
    /// Turns an <see cref="Occurrence.Timestamp"/> into the wall-clock instant it happened at.
    /// </summary>
    /// <remarks>
    /// <b>On the tape rather than anywhere downstream, because the tape is the only thing that knows what
    /// the ticks are measured from.</b> <see cref="Occurrence.Timestamp"/> is a monotonic counter whose
    /// origin is the platform's, so a caller holding only the number and the frequency can compute a
    /// duration and never an instant — divide it by the frequency and you get a time near 1970, or near
    /// process start, depending on which machine you are on. The pair that fixes the origin is captured
    /// once when the tape is built, off the same <c>TimeProvider</c> that stamps every occurrence, and
    /// every later tick is projected against it.
    /// <para>
    /// One anchor for the life of the tape, and it is deliberately not re-taken: re-anchoring would make
    /// two rows recorded a second apart project to instants that disagree by however much the wall clock
    /// was corrected in between, which is exactly the non-monotonic ordering the tick count exists to
    /// avoid. The cost is that a long-running process drifts against the wall clock by whatever the two
    /// clocks disagree by, which is seconds a day and is not what a log row is read for.
    /// </para>
    /// </remarks>
    DateTimeOffset ProjectUtc(long timestamp);

    /// <summary>
    /// Occurrences after <paramref name="afterSeq"/>, for an SSE <c>Last-Event-ID</c> reconnect.
    /// </summary>
    /// <remarks>
    /// The ring holds 256, matching the per-subscriber channel bound. A client offline for more than a
    /// few seconds under load will find a gap in <c>Seq</c> and must reload from the durable ledger; a
    /// deeper ring would hide that need without removing it. The state tape needs no replay at all —
    /// the cell value <i>is</i> the coalesced truth, so a reconnect takes a snapshot.
    /// </remarks>
    IReadOnlyList<Occurrence> Replay(long afterSeq, int max = 256);
}

/// <summary>
/// Puts an occurrence on the tape without owning any of the machinery that produced it.
/// </summary>
/// <remarks>
/// This is how command lifecycle — dispatched, completed, failed — reaches the Audit screen while
/// dispatch itself stays in <c>Vrc</c> and <c>Core</c>, where the socket is. It is roughly twenty
/// lines, and it is all the ledger ever actually needed from v2's 481-line command outbox.
/// </remarks>
public interface IOccurrenceRecorder
{
    /// <summary>Records one occurrence. Stamps <c>Seq</c> and the timestamp itself.</summary>
    void Record(
        OccurrenceKind kind,
        in KernelActor actor,
        in Correlation correlation,
        ReasonCode reason,
        string? detail,
        ImmutableArray<SignalTransition> transitions = default);
}
