using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Utilities;

/// <summary>
/// Pure text-transformation helpers (no side effects, no state).
/// Extracted from DataController to remove coupling.
/// </summary>
public static class TextUtilities
{
    private static readonly Dictionary<char, string> SuperscriptMapping = new()
    {
        {'/', "·"}, {':', "'"}, {'a', "ᵃ"}, {'b', "ᵇ"}, {'c', "ᶜ"}, {'d', "ᵈ"}, {'e', "ᵉ"},
        {'f', "ᶠ"}, {'g', "ᵍ"}, {'h', "ʰ"}, {'i', "ⁱ"}, {'j', "ʲ"},
        {'k', "ᵏ"}, {'l', "ˡ"}, {'m', "ᵐ"}, {'n', "ⁿ"}, {'o', "ᵒ"},
        {'p', "ᵖ"}, {'q', "ᵒ"}, {'r', "ʳ"}, {'s', "ˢ"}, {'t', "ᵗ"},
        {'u', "ᵘ"}, {'v', "ᵛ"}, {'w', "ʷ"}, {'x', "ˣ"}, {'y', "ʸ"},
        {'z', "ᶻ"}, {'0', "⁰"}, {'1', "¹"}, {'2', "²"}, {'3', "³"},
        {'4', "⁴"}, {'5', "⁵"}, {'6', "⁶"}, {'7', "⁷"}, {'8', "⁸"},
        {'9', "⁹"}, {',', "'"}, {'.', "'"}, {'%', "⁒"}
    };

    /// <summary>
    /// Converts each character of the input string to its Unicode superscript equivalent.
    /// Non-mappable characters are dropped; whitespace is preserved as a regular space.
    /// </summary>
    public static string TransformToSuperscript(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return new string(input.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '/' || c == ':' || c == ',' || c == '.' || c == '%')
            .Select(c => char.IsWhiteSpace(c) ? " " : (SuperscriptMapping.TryGetValue(c, out var mapped) ? mapped : c.ToString()))
            .SelectMany(s => s)
            .ToArray());
    }
}
