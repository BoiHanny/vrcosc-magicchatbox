using System;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static partial class TrackQueryNormalizer
{
    [GeneratedRegex(
        @"[\(\[\{]\s*(official\s*(music\s*)?(video|audio|visualizer|lyric[s]?\s*video)?|" +
        @"lyric[s]?|audio|video|visualizer|mv|m/v|hd|hq|4k|8k|remaster(ed)?(\s*\d{4})?|" +
        @"full\s*version|full\s*album|color\s*coded|eng\s*sub|sub\s*espa[nñ]ol|" +
        @"free\s*download|out\s*now|explicit|clean|extended|radio\s*edit)\s*[^\)\]\}]*[\)\]\}]",
        RegexOptions.IgnoreCase)]
    private static partial Regex NoiseSuffix();

    [GeneratedRegex(@"\s*-\s*topic\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TopicSuffix();

    [GeneratedRegex(@"\s*[\(\[]\s*(feat|ft|featuring)\b[^\)\]]*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturingSuffix();

    [GeneratedRegex(@"[\s\-–—_|,;:/\\]+$")]
    private static partial Regex TrailingSeparators();

    [GeneratedRegex(@"^[\s\-–—_|,;:/\\]+")]
    private static partial Regex LeadingSeparators();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();

    public static LyricsQuery Normalize(string? title, string? artist, string? album, TimeSpan duration)
    {
        string cleanTitle = CleanTitle(title);
        string cleanArtist = CleanArtist(artist);

        if (cleanArtist.Length == 0 && cleanTitle.Contains(" - ", StringComparison.Ordinal))
        {
            int split = cleanTitle.IndexOf(" - ", StringComparison.Ordinal);
            cleanArtist = Tidy(cleanTitle.Substring(0, split));
            cleanTitle = Tidy(cleanTitle.Substring(split + 3));
        }

        return new LyricsQuery
        {
            Title = cleanTitle,
            Artist = cleanArtist,
            Album = Tidy(album ?? string.Empty),
            Duration = duration,
        };
    }

    public static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        string working = NormalizeWidth(title);
        working = NoiseSuffix().Replace(working, " ");
        working = FeaturingSuffix().Replace(working, " ");

        return Tidy(working);
    }

    public static string CleanArtist(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return string.Empty;

        string working = NormalizeWidth(artist);
        working = TopicSuffix().Replace(working, string.Empty);
        working = NoiseSuffix().Replace(working, " ");

        return Tidy(working);
    }

    public static string NormalizeWidth(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);

        foreach (char c in text)
        {
            builder.Append(c switch
            {
                '（' => '(',
                '）' => ')',
                '［' => '[',
                '］' => ']',
                '　' => ' ',
                '－' => '-',
                '～' => '~',
                _ => c,
            });
        }

        return builder.ToString();
    }

    private static string Tidy(string text)
    {
        string working = MultipleSpaces().Replace(text, " ");
        working = LeadingSeparators().Replace(working, string.Empty);
        working = TrailingSeparators().Replace(working, string.Empty);
        return working.Trim();
    }
}
