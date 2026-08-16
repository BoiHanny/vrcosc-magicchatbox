namespace MagicChatbox.Vocabulary;

/// <summary>
/// Whether a key's changes may be coalesced.
/// </summary>
/// <remarks>
/// <b><see cref="Continuous"/> is legal only on <c>Float</c> and <c>Int</c>.</b> Bool and Text are
/// always <see cref="Discrete"/>, enforced as a registration error rather than a convention, because a
/// contact receiver toggling true and back inside one 33 ms drain would otherwise be coalesced away
/// entirely and the Triggers screen could never fire on it.
/// <para>
/// The default is <see cref="Discrete"/>, which fails toward never-dropping. A key misdeclared as
/// discrete costs occurrence-tape traffic; a key misdeclared as continuous loses edges silently, and
/// this project's audit history is full of the second kind.
/// </para>
/// <para>
/// <b>It lives here rather than beside the descriptor it facets</b> because it is on
/// <c>ModulePublication</c>'s signature, and a module author compiles against
/// <c>MagicChatbox.Modules.Abstractions</c>, which may not name the kernel. <c>WriteSafety</c> and
/// <c>DescriptorSource</c> stayed behind for the converse reason: no module signature names either.
/// </para>
/// </remarks>
public enum Temperament : byte
{
    /// <summary>Every edge matters. Reaches the occurrence tape as well as the state tape.</summary>
    Discrete = 0,

    /// <summary>A sampled quantity. Latest-wins is lossless by definition; never reaches the occurrence tape.</summary>
    Continuous = 1,
}
