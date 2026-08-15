using vrcosc_magicchatbox.Classes.Utilities;

namespace vrcosc_magicchatbox.Core.Osc.Text;

public enum OscTextRole
{
    Value,

    Label,

    Unit,
}

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

    public static OscText Value(string? text) => new(OscTextRole.Value, Clean(text));

    public static OscText Label(string? text) => new(OscTextRole.Label, Raise(text));

    public static OscText Unit(string? text) => new(OscTextRole.Unit, Raise(text));

    public static OscText Raw(string? text) => new(OscTextRole.Value, Clean(text));

    private static string Raise(string? text)
    {
        string cleaned = Clean(text);
        return cleaned.Length == 0 ? cleaned : TextUtilities.TransformToSuperscript(cleaned);
    }

    private static string Clean(string? text) => text?.Trim() ?? string.Empty;
}
