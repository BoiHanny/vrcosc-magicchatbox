using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Core.Osc.Text;

/// <summary>
/// Tidies a line that was composed from a user-editable template, where any field can resolve empty
/// and leave the literal glue written around it behind.
/// </summary>
/// <remarks>
/// A template writes the joiner, not the field, so "{artist} - {title}" with nothing playing ships a
/// line that begins with a bare dash - and blanking a field to save room hands part of the saving
/// straight back as punctuation. Every template-driven integration has this; it lives here so the
/// next one does not rediscover it.
/// </remarks>
public static class TemplateLine
{
    /// <summary>
    /// Removes glue that lost the field it was joining: at either end of a line, or doubled up where
    /// the field between two joiners went away. Glue still sitting between two real words is kept,
    /// because from the finished line there is no way to tell it was ever stranded.
    /// </summary>
    public static string DropStrandedJoiners(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = CleanLine(lines[i]);

        // Only the outer blank lines go - a gap the user typed on purpose in the middle is theirs.
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

            // Glue is worth keeping only when there is something in front of it to join to.
            if (kept.Count == 0 || IsJoiner(kept[^1]))
                continue;

            kept.Add(word);
        }

        while (kept.Count > 0 && IsJoiner(kept[^1]))
            kept.RemoveAt(kept.Count - 1);

        return string.Join(" ", kept);
    }

    /// <summary>A word that is nothing but punctuation is glue, never content.</summary>
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
