using vrcosc_magicchatbox.Classes.Utilities;

namespace vrcosc_magicchatbox.Core.Osc.Text;

public enum OscTextRole
{
    /// <summary>The number or name the reader is here for. Never raised.</summary>
    Value,

    /// <summary>What the value is. Raised, and takes a space in front of it.</summary>
    Label,

    /// <summary>The unit. Raised, and glued to the value it belongs to.</summary>
    Unit,
}

/// <summary>
/// A piece of a chatbox line, tagged with what it is for.
/// </summary>
/// <remarks>
/// The app has an implicit rule - the value stays full size, the label and the unit get raised -
/// and about half the integrations broke it, most memorably by shrinking the heart rate and leaving
/// the word BPM full size. Choosing a factory here is choosing the rendering, so raising a value is
/// not something a call site can do by accident.
/// </remarks>
public readonly record struct OscText
{
    private OscText(OscTextRole role, string rendered)
    {
        Role = role;
        Rendered = rendered;
    }

    public OscTextRole Role { get; }

    public string Rendered { get; }

    public bool IsEmpty => Rendered.Length == 0;

    /// <summary>The thing being reported. Left at full size, whatever it is.</summary>
    public static OscText Value(string? text) => new(OscTextRole.Value, Clean(text));

    /// <summary>What the value means. Raised.</summary>
    public static OscText Label(string? text) => new(OscTextRole.Label, Raise(text));

    /// <summary>The unit of the value before it. Raised, and never separated from it.</summary>
    public static OscText Unit(string? text) => new(OscTextRole.Unit, Raise(text));

    /// <summary>
    /// An icon or a glyph that is already exactly what it should look like. Behaves like a value:
    /// it is placed as-is and nothing is done to it.
    /// </summary>
    public static OscText Raw(string? text) => new(OscTextRole.Value, Clean(text));

    private static string Raise(string? text)
    {
        string cleaned = Clean(text);
        return cleaned.Length == 0 ? cleaned : TextUtilities.TransformToSuperscript(cleaned);
    }

    private static string Clean(string? text) => text?.Trim() ?? string.Empty;
}
