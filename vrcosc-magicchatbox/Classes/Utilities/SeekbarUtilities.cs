using System;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Utilities;

public sealed class SeekbarStyleOptions
{
    public bool DisplayTime { get; init; } = true;
    public string FilledCharacter { get; init; } = string.Empty;
    public string MiddleCharacter { get; init; } = string.Empty;
    public string NonFilledCharacter { get; init; } = string.Empty;
    public int ProgressBarLength { get; init; } = 8;
    public bool ShowTimeInSuperscript { get; init; } = true;
    public bool SpaceAgainObjects { get; init; } = true;
    public bool SpaceBetweenPreSuffixAndTime { get; init; }
    public string TimePrefix { get; init; } = string.Empty;
    public bool TimePreSuffixOnTheInside { get; init; } = true;
    public string TimeSuffix { get; init; } = string.Empty;
}

public static class SeekbarUtilities
{
    public static string CreateProgressBar(
        double percentage,
        TimeSpan current,
        TimeSpan full,
        SeekbarStyleOptions style)
    {
        if (style == null
            || string.IsNullOrEmpty(style.FilledCharacter)
            || string.IsNullOrEmpty(style.MiddleCharacter)
            || string.IsNullOrEmpty(style.NonFilledCharacter))
        {
            return string.Empty;
        }

        int totalBlocks = Math.Max(1, style.ProgressBarLength);

        string currentStr = style.DisplayTime && style.ShowTimeInSuperscript
            ? TextUtilities.TransformToSuperscript(FormatTimeSpan(current))
            : FormatTimeSpan(current);

        string fullStr = style.DisplayTime && style.ShowTimeInSuperscript
            ? TextUtilities.TransformToSuperscript(FormatTimeSpan(full))
            : FormatTimeSpan(full);

        if (style.DisplayTime && totalBlocks > 0)
        {
            int timeLen = currentStr.Length + fullStr.Length + 1;
            if (totalBlocks > timeLen)
                totalBlocks -= timeLen;
        }

        double clampedPercentage = Math.Clamp(percentage, 0, 100);
        int filled = Math.Clamp((int)(clampedPercentage / (100.0 / totalBlocks)), 0, totalBlocks);
        string filledBar = string.Concat(Enumerable.Repeat(style.FilledCharacter, filled));
        string emptyBar = string.Concat(Enumerable.Repeat(style.NonFilledCharacter, totalBlocks - filled));
        string progressBar = filledBar + style.MiddleCharacter + emptyBar;

        return FormatProgressBarWithTime(currentStr, fullStr, progressBar, style);
    }

    public static string CreateSmallNumbers(TimeSpan current, TimeSpan full)
        => $"{TextUtilities.TransformToSuperscript(FormatTimeSpan(current))} l {TextUtilities.TransformToSuperscript(FormatTimeSpan(full))}";

    public static string FormatTimeSpan(TimeSpan ts)
    {
        return ts.Hours > 0
            ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public static string FormatProgressBarWithTime(
        string current,
        string full,
        string bar,
        SeekbarStyleOptions style)
    {
        string sp = style.SpaceAgainObjects ? " " : "";
        string psp = style.SpaceBetweenPreSuffixAndTime ? " " : "";

        if (style.DisplayTime)
        {
            if (style.TimePreSuffixOnTheInside)
            {
                return string.IsNullOrWhiteSpace(style.TimePrefix) || string.IsNullOrWhiteSpace(style.TimeSuffix)
                    ? $"{current}{sp}{bar}{sp}{full}"
                    : $"{current}{psp}{style.TimePrefix}{bar}{style.TimeSuffix}{psp}{full}";
            }

            return $"{style.TimePrefix}{psp}{current}{sp}{bar}{sp}{full}{psp}{style.TimeSuffix}";
        }

        return style.TimePreSuffixOnTheInside
            ? $"{style.TimePrefix}{bar}{style.TimeSuffix}"
            : $"{style.TimePrefix}{psp}{bar}{psp}{style.TimeSuffix}";
    }
}
