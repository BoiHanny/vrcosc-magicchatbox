using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace vrcosc_magicchatbox.Classes.Modules.Afk;

public enum AfkTextStyle
{
    [Description("Plain")]
    Plain = 0,

    [Description("ˢᵘᵖᵉʳˢᶜʳⁱᵖᵗ")]
    Superscript = 1,

    [Description("ꜱᴍᴀʟʟ ᴄᴀᴘꜱ")]
    SmallCaps = 2,

    [Description("𝗯𝗼𝗹𝗱")]
    Bold = 3,

    [Description("𝘪𝘵𝘢𝘭𝘪𝘤")]
    Italic = 4,

    [Description("𝚖𝚘𝚗𝚘")]
    Monospace = 5,

    [Description("ｗｉｄｅ")]
    Wide = 6,
}

/// <summary>
/// Turns ordinary typing into the decorated text people actually want in a chatbox, so nobody has to
/// go hunting for characters to build their own "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ".
///
/// Anything without a real equivalent is left exactly as it was. That is not a shortcut: half of the
/// Unicode blocks these draw from have holes in them, and a question mark or a dropped letter in the
/// middle of someone's status is worse than a letter that stayed plain.
/// </summary>
public static class UnicodeTextStyler
{
    // Superscript deliberately leaves capitals alone. The block is missing C, F, Q, S, X, Y and Z, so
    // styling capitals would render "AFK" as "ᴬFᴷ". Leaving them upright is also exactly how the
    // long-standing default "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ" reads, so typing it out reproduces it character for
    // character.
    private static readonly Dictionary<char, string> Superscript = new()
    {
        ['a'] = "ᵃ", ['b'] = "ᵇ", ['c'] = "ᶜ", ['d'] = "ᵈ", ['e'] = "ᵉ", ['f'] = "ᶠ", ['g'] = "ᵍ",
        ['h'] = "ʰ", ['i'] = "ⁱ", ['j'] = "ʲ", ['k'] = "ᵏ", ['l'] = "ˡ", ['m'] = "ᵐ", ['n'] = "ⁿ",
        ['o'] = "ᵒ", ['p'] = "ᵖ", ['r'] = "ʳ", ['s'] = "ˢ", ['t'] = "ᵗ", ['u'] = "ᵘ", ['v'] = "ᵛ",
        ['w'] = "ʷ", ['x'] = "ˣ", ['y'] = "ʸ", ['z'] = "ᶻ",
        ['0'] = "⁰", ['1'] = "¹", ['2'] = "²", ['3'] = "³", ['4'] = "⁴",
        ['5'] = "⁵", ['6'] = "⁶", ['7'] = "⁷", ['8'] = "⁸", ['9'] = "⁹",
        ['+'] = "⁺", ['-'] = "⁻", ['='] = "⁼", ['('] = "⁽", [')'] = "⁾",
        // 'q' has no superscript form anywhere in Unicode.
    };

    private static readonly Dictionary<char, string> SmallCaps = new()
    {
        ['a'] = "ᴀ", ['b'] = "ʙ", ['c'] = "ᴄ", ['d'] = "ᴅ", ['e'] = "ᴇ", ['f'] = "ꜰ", ['g'] = "ɢ",
        ['h'] = "ʜ", ['i'] = "ɪ", ['j'] = "ᴊ", ['k'] = "ᴋ", ['l'] = "ʟ", ['m'] = "ᴍ", ['n'] = "ɴ",
        ['o'] = "ᴏ", ['p'] = "ᴘ", ['r'] = "ʀ", ['s'] = "ꜱ", ['t'] = "ᴛ", ['u'] = "ᴜ", ['v'] = "ᴠ",
        ['w'] = "ᴡ", ['y'] = "ʏ", ['z'] = "ᴢ",
        // 'q' and 'x' have no small capital forms.
    };

    public static string Apply(string? text, AfkTextStyle style)
    {
        if (string.IsNullOrEmpty(text) || style == AfkTextStyle.Plain)
            return text ?? string.Empty;

        var builder = new StringBuilder(text.Length * 2);

        foreach (char c in text)
        {
            switch (style)
            {
                case AfkTextStyle.Superscript:
                    builder.Append(Lookup(Superscript, c));
                    break;

                case AfkTextStyle.SmallCaps:
                    // Capitals are already the shape small caps imitate, so they pass through.
                    builder.Append(Lookup(SmallCaps, char.ToLowerInvariant(c), c));
                    break;

                case AfkTextStyle.Bold:
                    builder.Append(MathAlphabet(c, upper: 0x1D5D4, lower: 0x1D5EE, digit: 0x1D7EC));
                    break;

                case AfkTextStyle.Italic:
                    // The sans-serif italic block has no digits, so they stay upright.
                    builder.Append(MathAlphabet(c, upper: 0x1D608, lower: 0x1D622, digit: null));
                    break;

                case AfkTextStyle.Monospace:
                    builder.Append(MathAlphabet(c, upper: 0x1D670, lower: 0x1D68A, digit: 0x1D7F6));
                    break;

                case AfkTextStyle.Wide:
                    builder.Append(Fullwidth(c));
                    break;

                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// What this text costs against the 144 character line. Bold, italic and monospace live outside
    /// the basic plane, so every letter spends two of the budget rather than one - which is the sort
    /// of thing you want to find out before your integrations start getting trimmed, not after.
    /// </summary>
    public static int CostInChatbox(string? text, AfkTextStyle style)
        => Apply(text, style).Length;

    private static string Lookup(Dictionary<char, string> map, char key, char? fallback = null)
        => map.TryGetValue(key, out var mapped) ? mapped : (fallback ?? key).ToString();

    private static string MathAlphabet(char c, int upper, int lower, int? digit)
    {
        if (c is >= 'A' and <= 'Z')
            return char.ConvertFromUtf32(upper + (c - 'A'));

        if (c is >= 'a' and <= 'z')
            return char.ConvertFromUtf32(lower + (c - 'a'));

        if (digit.HasValue && c is >= '0' and <= '9')
            return char.ConvertFromUtf32(digit.Value + (c - '0'));

        return c.ToString();
    }

    private static string Fullwidth(char c)
    {
        if (c == ' ')
            return "　";

        // The fullwidth block mirrors printable ASCII from '!' to '~' in order.
        if (c is >= '!' and <= '~')
            return char.ConvertFromUtf32(0xFF01 + (c - '!'));

        return c.ToString();
    }

    public static string Describe(AfkTextStyle style)
    {
        var field = typeof(AfkTextStyle).GetField(style.ToString());
        var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false);

        return attribute is { Length: > 0 } && attribute[0] is DescriptionAttribute description
            ? description.Description
            : style.ToString();
    }

    public static IReadOnlyList<AfkTextStyle> All { get; } =
        (AfkTextStyle[])Enum.GetValues(typeof(AfkTextStyle));
}
