using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc.Text;

public static class TemplateLine
{
    public static string DropStrandedJoiners(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = CleanLine(lines[i]);

        return string.Join("\n", lines).Trim('\n');
    }

    private static string CleanLine(string line)
    {
        string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(words.Length);

        foreach (string word in words)
        {
            if (!IsJoiner(word))
            {
                kept.Add(word);
                continue;
            }

            if (kept.Count == 0 || IsJoiner(kept[^1]))
                continue;

            kept.Add(word);
        }

        while (kept.Count > 0 && IsJoiner(kept[^1]))
            kept.RemoveAt(kept.Count - 1);

        return string.Join(" ", kept);
    }

    private static bool IsJoiner(string word)
    {
        if (word.Length == 0)
            return false;

        foreach (char c in word)
        {
            if (c is not ('-' or '–' or '—' or '·' or '•' or '|' or '/' or ',' or ';' or ':'))
                return false;
        }

        return true;
    }
}
