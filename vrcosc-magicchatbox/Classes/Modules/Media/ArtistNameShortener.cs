using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Media;

public static partial class ArtistNameShortener
{
    [GeneratedRegex(
        @"\s*-\s*Topic\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TopicSuffix();

    [GeneratedRegex(
        @"\s*[\(\[]?\s*\b(?:feat\.?|ft\.?|featuring)\s+.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturedTail();

    [GeneratedRegex(@"\s+&\s+", RegexOptions.CultureInvariant)]
    private static partial Regex AmpersandJoin();

    private static readonly char[] CreditSeparators = [',', ';'];

    public static string Clean(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return string.Empty;

        return TopicSuffix().Replace(artist.Trim(), string.Empty).Trim();
    }

    public static IReadOnlyList<string> SplitCredits(string? artist)
    {
        string cleaned = Clean(artist);
        if (cleaned.Length == 0)
            return [];

        var parts = cleaned
            .Split(CreditSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (parts.Count > 1)
        {
            string[] tail = AmpersandJoin().Split(parts[^1]);
            if (tail.Length > 1)
            {
                parts.RemoveAt(parts.Count - 1);
                parts.AddRange(tail.Select(t => t.Trim()).Where(t => t.Length > 0));
            }
        }

        return parts;
    }

    public static IReadOnlyList<string> Ladder(string? artist)
    {
        string cleaned = Clean(artist);
        if (cleaned.Length == 0)
            return [];

        var rungs = new List<string>();

        void Add(string value)
        {
            if (value.Length > 0 && !rungs.Contains(value))
                rungs.Add(value);
        }

        Add(cleaned);

        string withoutFeat = FeaturedTail().Replace(cleaned, string.Empty)
            .Trim()
            .TrimEnd(',', ';', '(', '[')
            .Trim();
        Add(withoutFeat);

        var credits = SplitCredits(withoutFeat.Length > 0 ? withoutFeat : cleaned);
        for (int keep = credits.Count - 1; keep >= 1; keep--)
            Add($"{string.Join(", ", credits.Take(keep))} +{credits.Count - keep}");

        if (credits.Count > 1)
            Add(credits[0]);

        return rungs;
    }
}
