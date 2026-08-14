using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Utilities;

/// <summary>
/// Raised forms for the characters that actually have one.
/// </summary>
/// <remarks>
/// Everything here is a single BMP character, so raised text costs the same as plain text against
/// the 144 character line.
///
/// The table is limited to three Unicode blocks on purpose. Superscripts and Subscripts, Latin-1 and
/// Spacing Modifier Letters are old and universally drawn; Phonetic Extensions covers the rest of
/// the alphabet. Characters from newer blocks are listed at the bottom as rejected rather than
/// quietly included, because a glyph the font lacks renders as an empty box and reads as corruption.
/// </remarks>
public static class SuperscriptText
{
    private static readonly Dictionary<char, char> Map = new()
    {
        // Phonetic Extensions and Spacing Modifier Letters. No "q" exists in either.
        ['a'] = 'ᵃ', ['b'] = 'ᵇ', ['c'] = 'ᶜ', ['d'] = 'ᵈ', ['e'] = 'ᵉ', ['f'] = 'ᶠ',
        ['g'] = 'ᵍ', ['h'] = 'ʰ', ['i'] = 'ⁱ', ['j'] = 'ʲ', ['k'] = 'ᵏ', ['l'] = 'ˡ',
        ['m'] = 'ᵐ', ['n'] = 'ⁿ', ['o'] = 'ᵒ', ['p'] = 'ᵖ', ['r'] = 'ʳ', ['s'] = 'ˢ',
        ['t'] = 'ᵗ', ['u'] = 'ᵘ', ['v'] = 'ᵛ', ['w'] = 'ʷ', ['x'] = 'ˣ', ['y'] = 'ʸ',
        ['z'] = 'ᶻ',

        // Superscripts and Subscripts, plus the three that live in Latin-1.
        ['0'] = '⁰', ['1'] = '¹', ['2'] = '²', ['3'] = '³', ['4'] = '⁴',
        ['5'] = '⁵', ['6'] = '⁶', ['7'] = '⁷', ['8'] = '⁸', ['9'] = '⁹',
        ['+'] = '⁺', ['-'] = '⁻', ['='] = '⁼', ['('] = '⁽', [')'] = '⁾',

        // Spacing Modifier Letters again - the same block as the raised letters above, so these are
        // as safe as the alphabet is.
        ['?'] = 'ˀ', ['<'] = '˂', ['>'] = '˃', ['~'] = '˜', ['^'] = 'ˆ',
        [':'] = '˸', ['*'] = '˟', ['|'] = 'ˈ', ['"'] = 'ˮ',
        ['\''] = 'ʼ', ['’'] = 'ʼ', ['`'] = 'ˋ',

        // Latin Extended-D. The only entry outside the proven blocks, and the only raised
        // exclamation mark Unicode offers.
        ['!'] = 'ꜝ',

        // Not true raised forms - no such thing exists for either - but small lookalikes that have
        // been in the readouts long enough to prove they draw. Dropping them would put a full-size
        // glyph in the middle of raised text.
        ['%'] = '⁒',
        ['/'] = '·',
    };

    /// <summary>The raised form of a character, if one exists that can be relied on to draw.</summary>
    public static bool TryMap(char value, out char raised) => Map.TryGetValue(value, out raised);

    public static bool CanRaise(char value) => Map.ContainsKey(value);

    // Deliberately absent, and why:
    //
    //   q       U+107A5 is outside the basic plane, so it would cost two characters, and it was
    //           added in Unicode 14 - almost nothing draws it yet.
    //   C F Q   the raised capitals U+A7F2..U+A7F4 are Unicode 14 as well. Raising text lowercases
    //           it first, which sidesteps the gap and keeps every letter the same height anyway.
    //   . ,     U+2E33 and U+2E34 are Supplemental Punctuation. A full stop and a comma already sit
    //           low and small, so they pass beside raised letters without the risk.
    //   @ # $   no raised form exists in any block.
    //   % & _
}
