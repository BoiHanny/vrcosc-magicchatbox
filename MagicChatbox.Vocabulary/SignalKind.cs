namespace MagicChatbox.Vocabulary;

/// <summary>
/// The four cases of <see cref="SignalValue"/>, as a discriminator.
/// </summary>
/// <remarks>
/// Three of them — <see cref="Bool"/>, <see cref="Int"/>, <see cref="Float"/> — are exactly VRChat's
/// avatar-parameter wire types (<c>T</c>/<c>F</c>, <c>i</c>, <c>f</c>). <see cref="Text"/> exists for
/// facts that only ever travel inward and out to the chatbox, never onto the parameter path, which is
/// why it converts to nothing (§5.4).
/// <para>Numeric values are stable: they reach saved documents and logs.</para>
/// </remarks>
public enum SignalKind : byte
{
    /// <summary>A two-state fact. The default kind, so <c>default(SignalValue)</c> is <c>false</c>.</summary>
    Bool = 0,

    /// <summary>A whole number, widened to 64 bits so an OSC <c>i</c> can never overflow on the way in.</summary>
    Int = 1,

    /// <summary>A real number, widened to 64 bits. NaN and Infinity are representable and rejected at the store (D4).</summary>
    Float = 2,

    /// <summary>Discrete text, ordinal and case-sensitive, capped at 256 UTF-8 bytes.</summary>
    Text = 3,
}
