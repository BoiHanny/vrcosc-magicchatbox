using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vrcosc_magicchatbox.Core.Osc.Text;

/// <summary>
/// Builds one integration's segment: joins its fields, keeps the whitespace clean, and reports what
/// it costs of the line.
/// </summary>
/// <remarks>
/// Nine integrations grew their own version of this and each got a different part of it wrong -
/// double spaces, a gap between a number and its unit, a truncation that could split a surrogate
/// pair. Doing it once means those are not defects to fix but shapes that cannot be written.
/// </remarks>
public sealed class SegmentWriter
{
    private readonly List<string> _fields = [];

    /// <summary>Adds one field. Parts are placed by role: a unit glues on, anything else takes a space.</summary>
    public SegmentWriter Field(params OscText[] parts)
    {
        if (parts is null || parts.Length == 0)
            return this;

        var field = new StringBuilder();

        foreach (OscText part in parts)
        {
            if (part.IsEmpty)
                continue;

            // A unit belongs to the number in front of it. Everything else is its own word.
            if (field.Length > 0 && part.Role != OscTextRole.Unit)
                field.Append(' ');

            field.Append(part.Rendered);
        }

        if (field.Length > 0)
            _fields.Add(field.ToString());

        return this;
    }

    /// <summary>Adds a field only when the condition holds, so call sites stay flat.</summary>
    public SegmentWriter FieldIf(bool condition, params OscText[] parts)
        => condition ? Field(parts) : this;

    public string Text => Tidy(string.Join(OscGlyphs.FieldJoin, _fields));

    /// <summary>What this segment spends of the line, before any separator joining it to the next.</summary>
    public int Cost => Text.Length;

    public bool IsEmpty => Text.Length == 0;

    public override string ToString() => Text;

    /// <summary>
    /// The longest rendering that still fits, or the shortest one cut to size if none of them do.
    /// Replaces the bespoke fitting each integration invented, and gives the ones that never had a
    /// graceful degrade something better than vanishing.
    /// </summary>
    public static string Fit(int budget, params string[] rungs)
    {
        if (rungs is null || rungs.Length == 0)
            return string.Empty;

        var tidied = rungs.Select(Tidy).Where(r => r.Length > 0).ToList();
        if (tidied.Count == 0)
            return string.Empty;

        foreach (string rung in tidied)
        {
            if (rung.Length <= budget)
                return rung;
        }

        return Truncate(tidied[^1], budget);
    }

    /// <summary>
    /// Cuts to fit, marked so it does not read as the whole value, and never through the middle of
    /// a surrogate pair. Prefers a word boundary when one is close enough to be worth it.
    /// </summary>
    public static string Truncate(string? text, int budget)
    {
        string value = text ?? string.Empty;
        if (budget <= 0)
            return string.Empty;
        if (value.Length <= budget)
            return value;

        int keep = budget - OscGlyphs.Ellipsis.Length;
        if (keep <= 0)
            return OscGlyphs.Ellipsis.Length <= budget ? OscGlyphs.Ellipsis : string.Empty;

        int space = value.LastIndexOf(' ', Math.Min(keep, value.Length - 1));
        if (space > keep / 2)
            keep = space;
        else if (char.IsHighSurrogate(value[keep - 1]))
            keep--;

        return value[..keep].TrimEnd() + OscGlyphs.Ellipsis;
    }

    /// <summary>Collapses runs of whitespace and trims the ends.</summary>
    public static string Tidy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;

        foreach (char c in text)
        {
            // Newlines are meaningful in a chatbox line, so they survive; runs of them do not.
            bool isSpace = c == ' ' || c == '\t';
            if (isSpace && lastWasSpace)
                continue;

            builder.Append(isSpace ? ' ' : c);
            lastWasSpace = isSpace;
        }

        return builder.ToString().Trim();
    }
}
