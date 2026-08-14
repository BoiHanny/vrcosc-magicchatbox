using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Builds the sample chatbox line shown beside the prefix, suffix and separator settings.
/// </summary>
/// <remarks>
/// These four settings shape every line the app ever sends, and App options used to present them as
/// four bare text boxes. Nothing on screen said that the separator is ignored while ENTER mode is
/// on, that a prefix costs the same characters an integration would have used, or that "\n" is an
/// escape rather than two literal characters. Assembling stand-in segments the same way
/// <see cref="OscOutputBuilder"/> assembles real ones answers all three by being looked at.
/// </remarks>
public static class OscLinePreview
{
    /// <summary>
    /// Two short stand-ins for real integrations. Fixed rather than live, the way the music preview
    /// already fakes a track, so the preview says something even with every integration switched off.
    /// </summary>
    public static IReadOnlyList<string> SampleSegments { get; } = ["12:37", "78 bpm"];

    /// <summary>
    /// Assembles the sample line exactly as the real builder would, escapes and all.
    /// </summary>
    public static string Build(string? prefix, string? suffix, string? separator, bool separateWithEnters)
    {
        // ENTER mode wins over the separator box in the builder, so it has to win here too -
        // otherwise the preview would keep showing a separator the user is no longer getting.
        string join = separateWithEnters
            ? "\n"
            : OscOutputBuilder.NormalizeSeparator(separator);

        return OscOutputBuilder.ExpandNewlines(prefix)
             + string.Join(join, SampleSegments)
             + OscOutputBuilder.ExpandNewlines(suffix);
    }
}
