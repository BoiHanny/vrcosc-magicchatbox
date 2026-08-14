using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

/// <summary>
/// Tidies a track title for display. A browser reports the raw video title, so it arrives carrying
/// production credits and often the channel name the artist field already holds - together about a
/// third of the 144 character line.
/// </summary>
/// <remarks>
/// More cautious than <see cref="TrackQueryNormalizer"/>, which cleans titles for lyrics matching:
/// that one drops "Extended" and "Radio Edit" because they hurt a search, but they change what you
/// are hearing, so they stay here.
/// </remarks>
public static class MediaTitleCleaner
{
    // Words that only ever describe the upload, never the music.
    private const string NoiseToken =
        @"(?:official|music|lyric[s]?|video|audio|visualiser|visualizer|m/?v|hd|hq|uhd|full|[48]k|" +
        @"colou?r|coded|eng|sub|espa[nñ]ol|free|download|out|now|\d{4})";

    // The whole bracket has to be noise. Matching a keyword then swallowing the rest would eat
    // "(Video Game Soundtrack)".
    private static readonly Regex ProductionNoise = new(
        $@"[\(\[\{{]\s*{NoiseToken}(?:[\s\-–—/&,\.]+{NoiseToken})*\s*[\)\]\}}]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // The \b sits before the optional dot, not after it: "feat." ends on a full stop, and there is
    // no word boundary between "." and the space that follows.
    private const string FeatureWord = @"\b(?:featuring|feat|ft)\b\.?";

    private static readonly Regex FeaturedTail = new(
        $@"\s*[\(\[]\s*{FeatureWord}[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BareFeaturedTail = new(
        $@"\s+{FeatureWord}\s+.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MultipleSpaces = new(@"\s{2,}", RegexOptions.Compiled);

    private static readonly Regex EdgeSeparators = new(
        @"^[\s\-–—_|,;:/\\]+|[\s\-–—_|,;:/\\]+$",
        RegexOptions.Compiled);

    // Channel-name decoration that stops a title prefix matching the artist it repeats.
    private static readonly Regex ArtistDecoration = new(
        @"(?:vevo|official|topic|music|channel)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] TitleArtistDividers = [" - ", " – ", " — ", " | ", " • ", ": "];

    /// <summary>Strips upload noise, then the artist name the title repeats.</summary>
    public static string Clean(string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string working = TrackQueryNormalizer.NormalizeWidth(title);
        working = ProductionNoise.Replace(working, " ");
        working = Tidy(working);

        return StripArtistEcho(working, artist);
    }

    /// <summary>
    /// Drops a "(feat. …)" credit. Separate from <see cref="Clean"/> because a guest artist is real
    /// information - it goes only when the line would otherwise not fit.
    /// </summary>
    public static string StripFeatured(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string working = FeaturedTail.Replace(title, " ");
        working = BareFeaturedTail.Replace(working, string.Empty);
        return Tidy(working);
    }

    /// <summary>
    /// Removes the artist from the front or back of the title. The line already renders as
    /// "title ᵇʸ artist", so a repeat is waste.
    /// </summary>
    public static string StripArtistEcho(string title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            return title;

        var names = new List<string> { artist };
        names.AddRange(ArtistNameShortener.SplitCredits(artist));

        foreach (string divider in TitleArtistDividers)
        {
            int head = title.IndexOf(divider, StringComparison.Ordinal);
            if (head > 0 && IsSameName(title[..head], names))
            {
                string tail = Tidy(title[(head + divider.Length)..]);
                if (tail.Length > 0)
                    return tail;
            }

            int tailStart = title.LastIndexOf(divider, StringComparison.Ordinal);
            if (tailStart > 0 && IsSameName(title[(tailStart + divider.Length)..], names))
            {
                string head2 = Tidy(title[..tailStart]);
                if (head2.Length > 0)
                    return head2;
            }
        }

        return title;
    }

    private static bool IsSameName(string candidate, IEnumerable<string> names)
    {
        string folded = Fold(candidate);
        return folded.Length > 0 && names.Any(n => Fold(n) == folded);
    }

    /// <summary>Reduces a name to comparable letters, so a decorated channel name meets a plain one.</summary>
    private static string Fold(string value)
    {
        string letters = new(value.Where(char.IsLetterOrDigit).ToArray());
        letters = letters.ToLowerInvariant();
        return ArtistDecoration.Replace(letters, string.Empty);
    }

    private static string Tidy(string text)
    {
        string working = MultipleSpaces.Replace(text, " ");
        return EdgeSeparators.Replace(working, string.Empty).Trim();
    }
}
