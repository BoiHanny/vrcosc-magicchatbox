namespace vrcosc_magicchatbox.Core.Osc.Text;

/// <summary>
/// One glyph per job. Several integrations picked the same character for different purposes and
/// different characters for the same purpose; naming the roles here is what stops that recurring.
/// </summary>
/// <remarks>
/// Every glyph is a single basic-plane character and is already shipping somewhere in the chatbox,
/// so none of them is an untested guess. Nothing goes in here that has not been seen to render.
/// </remarks>
public static class OscGlyphs
{
    /// <summary>Between two integrations. Matches the separator the builder has always defaulted to.</summary>
    public const string SegmentJoin = " ┆ ";

    /// <summary>Between two fields of one integration.</summary>
    public const string FieldJoin = " · ";

    /// <summary>Marks a cut. One character - never three dots.</summary>
    public const string Ellipsis = "…";

    /// <summary>Raised percent. Shipping in the VR performance readout.</summary>
    public const string Percent = "⁒";

    /// <summary>Degree. Latin-1, and it survives a raise unchanged.</summary>
    public const string Degree = "°";
}
