using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc;

public static class OscLinePreview
{
    public static IReadOnlyList<string> SampleSegments { get; } = ["12:37", "78 bpm"];

    public static string Build(string? prefix, string? suffix, string? separator, bool separateWithEnters)
    {
        string join = separateWithEnters
            ? "\n"
            : OscOutputBuilder.NormalizeSeparator(separator);

        return OscOutputBuilder.ExpandNewlines(prefix)
             + string.Join(join, SampleSegments)
             + OscOutputBuilder.ExpandNewlines(suffix);
    }
}
