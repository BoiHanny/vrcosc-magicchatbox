using System.Text;

namespace vrcosc_magicchatbox.Classes.Utilities;

public static class TextUtilities
{
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
