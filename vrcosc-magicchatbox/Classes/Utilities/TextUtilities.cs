using System.Text;

namespace vrcosc_magicchatbox.Classes.Utilities;

public static class TextUtilities
{
    /// <summary>
    /// Raises what can be raised and leaves the rest at full size.
    /// </summary>
    /// <remarks>
    /// Characters without a raised form pass through instead of being deleted, which is what the
    /// previous table did to degree signs, hyphens and brackets. The table itself is
    /// <see cref="SuperscriptText"/>, limited to glyphs known to draw in the chatbox.
    /// </remarks>
    public static string TransformToSuperscript(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var builder = new StringBuilder(input.Length);

        foreach (char c in input)
        {
            if (char.IsWhiteSpace(c))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(SuperscriptText.TryMap(char.ToLowerInvariant(c), out char raised) ? raised : c);
        }

        return builder.ToString();
    }
}
