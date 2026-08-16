using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>One avatar parameter a peer's OSCQuery tree enumerated, with what it held when we asked.</summary>
/// <param name="Name">The parameter name with <c>/avatar/parameters/</c> stripped, e.g. <c>VRCEmote</c>.</param>
/// <param name="Kind">The kind the peer declared, translated from its OSC type tag.</param>
/// <param name="Value">
/// The value at the moment of the request, or null when the leaf carried no <c>VALUE</c> — which is why
/// presence is a separate question from declaration. Nullable rather than a sentinel because
/// <see cref="SignalKind"/> has no "none": its default is <see cref="SignalKind.Bool"/>, so a sentinel
/// would be indistinguishable from a real <c>false</c>.
/// </param>
/// <param name="Writable">
/// Whether the peer's <c>ACCESS</c> bitmask has the write bit. Carried because it is the honest
/// discriminator between a parameter that can be driven and one that only reports — the address prefix is
/// not: <c>/avatar/parameters/VRCEmote</c> is writable while <c>/avatar/parameters/ScaleFactor</c>, its
/// immediate neighbour, is not.
/// </param>
public readonly record struct VrcParameterDeclaration(
    string Name,
    SignalKind Kind,
    SignalValue? Value,
    bool Writable);

/// <summary>A reading for a key that is already declared and needs no schema work — the fixed leaves.</summary>
/// <param name="Key">The projected key, e.g. <c>avatar.eyeheight_min</c>.</param>
/// <param name="Value">The value at the moment of the request.</param>
public readonly record struct VrcFixedReading(SignalKey Key, SignalValue Value);

/// <summary>
/// Everything one OSCQuery enumeration learned, in the vocabulary both halves share.
/// </summary>
/// <param name="AvatarId">The avatar the peer's tree described, from its <c>/avatar/change</c> leaf.</param>
/// <param name="Epoch">
/// <see cref="VrcAvatarEpoch.Current"/> read <b>before</b> the fetch was issued. A consumer compares this
/// against the epoch now: an avatar that changed while the HTTP request was in flight makes the whole
/// snapshot describe an avatar nobody is wearing any more.
/// </param>
/// <param name="Parameters">The avatar's own parameters, needing both a descriptor and a value.</param>
/// <param name="Fixed">The session leaves, needing only a value.</param>
public sealed record VrcAvatarSchemaHarvest(
    string AvatarId,
    long Epoch,
    IReadOnlyList<VrcParameterDeclaration> Parameters,
    IReadOnlyList<VrcFixedReading> Fixed);

/// <summary>
/// Somewhere to hand an enumeration to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface exists for the same reason <see cref="IVrcObservationSink"/> does:</b> the snapshot
/// starts life as an <c>Osc</c> type, <c>Core</c> does not reference <c>Osc</c>, and <c>Vrc</c>'s public
/// surface is forbidden from naming one. So <c>Vrc</c> translates the tree into the shared vocabulary and
/// declares the seam; <c>Core</c> — the one assembly that may name both the kernel and the transport —
/// implements it.
/// </para>
/// <para>
/// <b>Unlike the observation sink, this is not called on the receive loop.</b> A harvest is a network
/// round trip, so it runs on its own task and an implementation may take its time. What it must not do is
/// assume it is still current: see <see cref="VrcAvatarSchemaHarvest.Epoch"/>.
/// </para>
/// </remarks>
public interface IVrcSchemaSink
{
    /// <summary>Receives one enumeration. Called off the receive loop; may block briefly.</summary>
    void OnSchemaHarvested(VrcAvatarSchemaHarvest harvest);
}

/// <summary>Discards every harvest. The default when a host wants the transport but not the schema.</summary>
public sealed class NullVrcSchemaSink : IVrcSchemaSink
{
    /// <summary>The shared instance. Stateless, so one is enough.</summary>
    public static NullVrcSchemaSink Instance { get; } = new();

    /// <inheritdoc />
    public void OnSchemaHarvested(VrcAvatarSchemaHarvest harvest)
    {
    }
}
