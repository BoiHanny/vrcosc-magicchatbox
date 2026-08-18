using System;
using System.Collections.Generic;
using System.Text;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static class LyricSegmentFormatter
{
    public const string Ellipsis = "…";
    public const string GapMark = "♪";

    public static string Build(
        LyricCursor cursor,
        TimeSpan position,
        int budget,
        LyricsSettings settings)
    {
        if (settings == null || budget <= 0)
            return string.Empty;

        switch (cursor.Kind)
        {
            case LyricCursorKind.InstrumentalGap:
            case LyricCursorKind.BeforeFirstLine:
                return BuildInstrumentalMarker(settings, position, budget);

            case LyricCursorKind.Line:
                if (budget < settings.MinimumCharacters)
                    return string.Empty;

                string text = PrepareLine(cursor.Text, settings);
                if (text.Length == 0)
                    return string.Empty;

                string prefix = settings.ShowNoteIcon ? GapMark + " " : string.Empty;
                int textBudget = budget - prefix.Length;

                if (textBudget < 4)
                    return string.Empty;

                return prefix + Fit(text, textBudget, cursor, position);

            default:
                return string.Empty;
        }
    }

    public static string BuildInstrumentalMarker(LyricsSettings settings, TimeSpan position, int budget)
    {
        if (settings == null || !settings.ShowGapMarker)
            return string.Empty;

        var style = settings.InstrumentalMarker;

        if (InstrumentalMarker.MaxWidth(style) > budget)
            style = LyricsInstrumentalMarker.Note;

        string marker = InstrumentalMarker.Render(style, position);
        return marker.Length <= budget ? marker : string.Empty;
    }

    public static string PrepareLine(string? raw, LyricsSettings? settings)
    {
        string text = Sanitize(raw);
        if (text.Length == 0 || settings?.SuperscriptAsides != true)
            return text;

        return Sanitize(LyricAsides.Apply(text));
    }

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r' || c == '\t')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        return CollapseSpaces(builder.ToString()).Trim();
    }

    public static string Fit(string text, int budget, LyricCursor cursor, TimeSpan position)
    {
        if (budget <= 0)
            return string.Empty;

        if (text.Length <= budget)
            return text;

        var chunks = Chunk(text, Math.Max(4, budget - Ellipsis.Length * 2));
        if (chunks.Count == 0)
            return string.Empty;

        int index = SelectChunk(chunks.Count, cursor, position);
        string chunk = chunks[index];

        string lead = index > 0 ? Ellipsis : string.Empty;
        string tail = index < chunks.Count - 1 ? Ellipsis : string.Empty;
        string result = lead + chunk + tail;

        return result.Length <= budget ? result : Hard(result, budget);
    }

    public static int SelectChunk(int chunkCount, LyricCursor cursor, TimeSpan position)
    {
        if (chunkCount <= 1)
            return 0;

        TimeSpan duration = cursor.LineDuration;
        if (duration <= TimeSpan.Zero || cursor.LineEnd == TimeSpan.MaxValue)
            return 0;

        double progressed = (position - cursor.LineStart).TotalMilliseconds / duration.TotalMilliseconds;
        int index = (int)Math.Floor(progressed * chunkCount);

        return Math.Clamp(index, 0, chunkCount - 1);
    }

    public static IReadOnlyList<string> Chunk(string text, int size)
    {
        var chunks = new List<string>();
        if (size <= 0 || text.Length == 0)
            return chunks;

        var current = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;

            if (candidate.Length <= size)
            {
                current.Clear().Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (word.Length <= size)
            {
                current.Append(word);
                continue;
            }

            for (int i = 0; i < word.Length; i += size)
                chunks.Add(word.Substring(i, Math.Min(size, word.Length - i)));
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks;
    }

    private static string Hard(string text, int budget)
        => text.Length <= budget ? text : text.Substring(0, Math.Max(0, budget));

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;

        foreach (char c in text)
        {
            bool isSpace = c == ' ';
            if (isSpace && lastWasSpace)
                continue;

            builder.Append(c);
            lastWasSpace = isSpace;
        }

        return builder.ToString();
    }
}
