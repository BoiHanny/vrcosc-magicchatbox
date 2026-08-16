using System.Collections.Immutable;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Kernel;

/// <summary>
/// Everything the kernel knows about a key that is not its current value.
/// </summary>
/// <param name="Key">The key described.</param>
/// <param name="Kind">The kind values on this key are coerced to. A mismatch that will not convert is rejected.</param>
/// <param name="Safety">Who may write it, and by what path.</param>
/// <param name="Temperament">Whether changes may be coalesced. Continuous is legal only on Float and Int.</param>
/// <param name="HasExternalSideEffect">
/// True when changing this fact means asking the outside world, not writing a cell. Every
/// <c>avatar.param.*</c> key is one: the cell records what VRChat reported, and a local write would
/// make the store assert a value the game never confirmed. <c>Mutate</c> is rejected on these.
/// </param>
/// <param name="Owner">The source id that owns this key. Used by the Sources screen and by eviction.</param>
/// <param name="Group">A display grouping — "Now playing", "Vitals". Presentation only.</param>
/// <param name="Description">One human sentence. Reaches the assistant's tool schema and the UI.</param>
/// <param name="Unit">What the number means physically.</param>
/// <param name="Min">Lower bound for UI affordances, when one is known.</param>
/// <param name="Max">Upper bound for UI affordances, when one is known.</param>
/// <param name="Precision">Decimal places when rendering. Zero for integers and bools.</param>
/// <param name="Importance">Drop order when a composition will not fit.</param>
/// <param name="ExpectedIntervalMs">How often a healthy source refreshes this. Diagnostic, not enforced.</param>
/// <param name="StaleAfterMs">
/// How long a reading stays believable. Null means never stale — which is right for a menu toggle and
/// wrong for a heart rate. Only keys that declare it are scanned by the staleness sweep.
/// </param>
/// <param name="DependsOn">
/// Keys whose unavailability makes this one meaningless. The composer suppresses a segment when a
/// dependency is not live, which is how "artist" disappears with "title" instead of hanging alone.
/// </param>
/// <param name="NativeName">
/// The source's own spelling of this signal, when the key is a lossy projection of it. Null when the key
/// already is the name.
/// <para>
/// A key is trimmed and lower-cased; the system on the other side may not be. VRChat matches parameter
/// addresses case-sensitively, so <c>avatar.param.go/jsrf/readytogrind</c> cannot be turned back into
/// <c>Go/JSRF/ReadyToGrind</c> by any rule — the original has to be carried or it is gone. It is also
/// what a UI should print: a key is an identifier, not a label.
/// </para>
/// </param>
/// <param name="RemoteWritable">
/// Whether the upstream system accepts writes to this signal. Null when unknown.
/// <para>
/// Distinct from <paramref name="Safety"/>, and the two must not be conflated. <c>Safety</c> governs who
/// may write the kernel <i>cell</i>; this governs whether the outside world will honour a write at all.
/// Every <c>avatar.param.*</c> key is <c>ObservedOnly</c> regardless, because the cell is written by
/// observation — but roughly nine in ten of them are things VRChat will happily let you set.
/// </para>
/// </param>
/// <param name="Label">
/// A display name the declaring source chose, when neither the key nor <paramref name="NativeName"/> can
/// be made into one. Null for almost everything, because almost everything can.
/// <para>
/// <c>SignalLabel</c> builds a name out of the key's last segment where a source says nothing, and for a
/// key whose last segment is a name — <c>module.music.title</c>, <c>vrc.camera.zoom</c> — that is the
/// right answer and this field is noise. It exists for the keys where the last segment is a coordinate
/// rather than a name: VRChat's tracking poses are eighteen keys ending in <c>x</c>, <c>y</c> and
/// <c>z</c> under three devices, and no rule applied to <c>input.vr.leftwrist.position.x</c> recovers
/// "Left wrist position X" — the word that distinguishes it is in the middle of the key, and only the
/// code that declared it knows which middle segment that is.
/// </para>
/// <para>
/// <b>Not <paramref name="NativeName"/>, and the difference is load-bearing rather than editorial.</b>
/// A native name is a claim that the upstream system spells the signal that way, and two things act on
/// that claim: <c>WardrobeService</c> will only record a parameter it can name, and <c>CarryOverRunner</c>
/// compares native names to decide that an arriving avatar declares the same parameter a saved one did.
/// A display name written into that field would be a name the wire has never heard of, offered to code
/// whose whole job is to send it back.
/// </para>
/// </param>
public readonly record struct SignalDescriptor(
    SignalKey Key,
    SignalKind Kind,
    WriteSafety Safety,
    Temperament Temperament,
    bool HasExternalSideEffect,
    string Owner,
    string Group,
    string Description,
    Unit Unit,
    double? Min,
    double? Max,
    byte Precision,
    Importance Importance,
    int? ExpectedIntervalMs,
    int? StaleAfterMs,
    ImmutableArray<SignalKey> DependsOn,
    string? NativeName = null,
    bool? RemoteWritable = null,
    string? Label = null)
{
    /// <summary>
    /// The minimum honest descriptor: a key, a kind, and an owner. Everything else takes its default,
    /// and every default fails safe — <c>Discrete</c>, <c>Writable</c>, no staleness, no side effect.
    /// </summary>
    public static SignalDescriptor Create(
        SignalKey key,
        SignalKind kind,
        string owner,
        WriteSafety safety = WriteSafety.Writable,
        Temperament temperament = Temperament.Discrete,
        bool hasExternalSideEffect = false,
        Unit unit = Unit.None,
        Importance importance = Importance.Foreground,
        int? staleAfterMs = null,
        int? expectedIntervalMs = null,
        byte precision = 0,
        string group = "",
        string description = "",
        string? nativeName = null,
        bool? remoteWritable = null,
        string? label = null) =>
        new(key, kind, safety, temperament, hasExternalSideEffect, owner, group, description, unit,
            Min: null, Max: null, precision, importance, expectedIntervalMs, staleAfterMs,
            ImmutableArray<SignalKey>.Empty, nativeName, remoteWritable, label);
}

/// <summary>
/// What happened to a descriptor upsert.
/// </summary>
/// <param name="Applied">True when this upsert changed the effective descriptor for the key.</param>
/// <param name="Winner">Which layer currently owns the effective descriptor.</param>
/// <param name="Reason">
/// <c>Ok</c> when applied, <c>NoContent</c> when the layer was accepted but outranked, and a rejection
/// reason when the descriptor is illegal.
/// </param>
public readonly record struct UpsertResult(bool Applied, DescriptorSource Winner, ReasonCode Reason);
