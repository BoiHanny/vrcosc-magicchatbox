using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex TimestampPattern();

    [GeneratedRegex(@"^\s*\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetPattern();

    [GeneratedRegex(@"^\s*\[[a-zA-Z#]+:[^\]]*\]\s*$")]
    private static partial Regex MetadataPattern();

    [GeneratedRegex(@"^\s*[\[\(](music|applause|laughter|instrumental|intro|outro|chorus|verse|bridge|hook|silence)[^\]\)]*[\]\)]\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex StageDirectionPattern();

    public const double MaxStageDirectionShare = 0.30;

    public static LyricTrack Parse(string? content, string providerName = "")
    {
        if (string.IsNullOrWhiteSpace(content))
            return LyricTrack.Empty;

        var parsed = new List<LyricLine>();
        TimeSpan embeddedOffset = TimeSpan.Zero;
        int stageDirections = 0;

        foreach (string raw in content.Replace("\r\n", "\n").Split('\n'))
        {
            var offsetMatch = OffsetPattern().Match(raw);
            if (offsetMatch.Success &&
                int.TryParse(offsetMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ms))
            {
                embeddedOffset = TimeSpan.FromMilliseconds(ms);
                continue;
            }

            var stamps = TimestampPattern().Matches(raw);
            if (stamps.Count == 0)
                continue;

            string text = TimestampPattern().Replace(raw, string.Empty).Trim();

            if (MetadataPattern().IsMatch(text) || IsCreditBlock(text))
                continue;

            if (StageDirectionPattern().IsMatch(text))
                stageDirections++;

            foreach (Match stamp in stamps)
            {
                if (TryReadTimestamp(stamp, out TimeSpan at))
                    parsed.Add(new LyricLine(at, text));
            }
        }

        if (parsed.Count == 0)
            return LyricTrack.Empty;

        if (stageDirections > parsed.Count * MaxStageDirectionShare)
            return LyricTrack.Empty;

        var ordered = KeepFirstBlock(parsed);

        return new LyricTrack
        {
            Lines = ordered,
            EmbeddedOffset = embeddedOffset,
            ProviderName = providerName,
        };
    }

    public static IReadOnlyList<LyricLine> KeepFirstBlock(IReadOnlyList<LyricLine> lines)
    {
        if (lines.Count == 0)
            return lines;

        var kept = new List<LyricLine>(lines.Count) { lines[0] };

        for (int i = 1; i < lines.Count; i++)
        {
            if (lines[i].Start < lines[i - 1].Start)
                break;

            kept.Add(lines[i]);
        }

        return kept;
    }

    private static bool IsCreditBlock(string text)
        => text.StartsWith("{\"t\":", StringComparison.Ordinal);

    private static bool TryReadTimestamp(Match stamp, out TimeSpan at)
    {
        at = TimeSpan.Zero;

        if (!int.TryParse(stamp.Groups[1].Value, out int minutes) ||
            !int.TryParse(stamp.Groups[2].Value, out int seconds))
        {
            return false;
        }

        int fraction = 0;
        string fractionText = stamp.Groups[3].Value;
        if (fractionText.Length > 0 && int.TryParse(fractionText, out fraction))
        {
            fraction = fractionText.Length switch
            {
                1 => fraction * 100,
                2 => fraction * 10,
                _ => fraction,
            };
        }

        at = new TimeSpan(0, 0, minutes, seconds, fraction);
        return true;
    }
}
