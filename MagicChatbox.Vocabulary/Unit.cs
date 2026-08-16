namespace MagicChatbox.Vocabulary;

/// <summary>
/// What a number means physically, so the composer can format it without a per-provider special case.
/// </summary>
/// <remarks>
/// Here rather than in the kernel because <c>ModulePublication</c> names it, and the assembly a module
/// author compiles against may reference only this one. See <see cref="Temperament"/> for the argument.
/// </remarks>
public enum Unit : byte
{
    /// <summary>Dimensionless.</summary>
    None,

    /// <summary>0–100.</summary>
    Percent,

    /// <summary>Bytes.</summary>
    Bytes,

    /// <summary>Megabytes.</summary>
    Megabytes,

    /// <summary>Seconds. Track position and duration both use this.</summary>
    Seconds,

    /// <summary>Beats per minute.</summary>
    Bpm,

    /// <summary>Degrees Celsius.</summary>
    Celsius,

    /// <summary>0–1, which is what nearly every avatar parameter actually is.</summary>
    Ratio01,

    /// <summary>A whole count of things.</summary>
    Count,

    /// <summary>Metres. Eye height, and the position component of a tracked pose.</summary>
    Metres,

    /// <summary>Degrees of rotation. The rotation component of a tracked pose.</summary>
    Degrees,
}
