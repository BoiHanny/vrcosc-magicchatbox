using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vrcosc_magicchatbox.Core.Osc.Text;

public sealed class SegmentWriter
{
    private readonly List<string> _fields = [];

    public SegmentWriter Field(params OscText[] parts)
    {
        if (parts is null || parts.Length == 0)
            return this;

        var field = new StringBuilder();

        foreach (OscText part in parts)
        {
            if (part.IsEmpty)
                continue;

            if (field.Length > 0 && part.Role != OscTextRole.Unit)
                field.Append(' ');

            field.Append(part.Rendered);
        }

        if (field.Length > 0)
            _fields.Add(field.ToString());

        return this;
    }

    public SegmentWriter FieldIf(bool condition, params OscText[] parts)
        => condition ? Field(parts) : this;

    public string Text => Tidy(string.Join(OscGlyphs.FieldJoin, _fields));

    public int Cost => Text.Length;

    public bool IsEmpty => Text.Length == 0;

    public override string ToString() => Text;

    public static string Fit(int budget, params string[] rungs)
    {
        if (rungs is null || rungs.Length == 0)
            return string.Empty;

        string fallback = string.Empty;

        foreach (string rung in rungs)
        {
            string tidied = Tidy(rung);
            if (tidied.Length == 0)
                continue;

            if (tidied.Length <= budget)
                return tidied;

            fallback = tidied;
        }

        return fallback.Length == 0 ? string.Empty : Truncate(fallback, budget);
    }

    public static string Fit(int budget, params Func<string>[] rungs)
    {
        if (rungs is null || rungs.Length == 0)
            return string.Empty;

        string fallback = string.Empty;

        foreach (Func<string> rung in rungs)
        {
            if (rung is null)
                continue;

            string tidied = Tidy(rung());
            if (tidied.Length == 0)
                continue;

            if (tidied.Length <= budget)
                return tidied;

            fallback = tidied;
        }

        return fallback.Length == 0 ? string.Empty : Truncate(fallback, budget);
    }

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

    public static string Tidy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;

        foreach (char c in text)
        {
            bool isSpace = c == ' ' || c == '\t';
            if (isSpace && lastWasSpace)
                continue;

            builder.Append(isSpace ? ' ' : c);
            lastWasSpace = isSpace;
        }

        return builder.ToString().Trim();
    }
}
