using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

public static partial class MediaTitleCleaner
{
    private const string NoiseToken =
        @"(?:official|music|lyric[s]?|video|audio|visualiser|visualizer|m/?v|hd|hq|uhd|full|[48]k|" +
        @"colou?r|coded|eng|sub|espa[nñ]ol|free|download|out|now|\d{4})";

    [GeneratedRegex(
        $@"[\(\[\{{]\s*{NoiseToken}(?:[\s\-–—/&,\.]+{NoiseToken})*\s*[\)\]\}}]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductionNoise();

    private const string FeatureWord = @"\b(?:featuring|feat|ft)\b\.?";

    [GeneratedRegex(
        $@"\s*[\(\[]\s*{FeatureWord}[^\)\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturedTail();

    [GeneratedRegex(
        $@"\s+{FeatureWord}\s+.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BareFeaturedTail();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex(@"^[\s\-–—_|,;:/\\]+|[\s\-–—_|,;:/\\]+$")]
    private static partial Regex EdgeSeparators();

    [GeneratedRegex(
        @"(?:vevo|official|topic|music|channel)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ArtistDecoration();

    private static readonly string[] TitleArtistDividers = [" - ", " – ", " — ", " | ", " • ", ": "];

    public static string Clean(string? title, string? artist)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string working = TrackQueryNormalizer.NormalizeWidth(title);
        working = ProductionNoise().Replace(working, " ");
        working = Tidy(working);

        return StripArtistEcho(working, artist);
    }

    public static string StripFeatured(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string working = FeaturedTail().Replace(title, " ");
        working = BareFeaturedTail().Replace(working, string.Empty);
        return Tidy(working);
    }

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

    private static string Fold(string value)
    {
        string letters = new(value.Where(char.IsLetterOrDigit).ToArray());
        letters = letters.ToLowerInvariant();
        return ArtistDecoration().Replace(letters, string.Empty);
    }

    private static string Tidy(string text)
    {
        string working = MultipleSpaces().Replace(text, " ");
        return EdgeSeparators().Replace(working, string.Empty).Trim();
    }
}
