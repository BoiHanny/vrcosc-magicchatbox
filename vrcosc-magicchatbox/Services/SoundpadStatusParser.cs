using System;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Services;

public enum SoundpadPlayStatus
{
    Unknown,
    Stopped,
    Playing,
    Paused,
    Seeking
}

public static partial class SoundpadStatusParser
{
    [GeneratedRegex(@"^R-\d")]
    private static partial Regex ErrorCodeRegex();

    [GeneratedRegex(@"\s*\[.*?\]\s*")]
    private static partial Regex BracketedSegmentRegex();

    [GeneratedRegex(@"^(?:II\s+)?Soundpad(\s*-\s*|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePrefixRegex();

    [GeneratedRegex(@"^\s*II\s+Soundpad(\s|-|$)", RegexOptions.IgnoreCase)]
    private static partial Regex PausedTitleRegex();

    public static bool IsSuccessResponse(string? response)
        => response != null && response.StartsWith("R-200", StringComparison.Ordinal);

    public static bool IsErrorResponse(string? response)
        => response == null || ErrorCodeRegex().IsMatch(response);

    public static SoundpadPlayStatus ParsePlayStatus(string? response)
    {
        if (IsErrorResponse(response))
            return SoundpadPlayStatus.Unknown;

        return response!.Trim().ToUpperInvariant() switch
        {
            "PLAYING" => SoundpadPlayStatus.Playing,
            "PAUSED" => SoundpadPlayStatus.Paused,
            "SEEKING" => SoundpadPlayStatus.Seeking,
            "STOPPED" => SoundpadPlayStatus.Stopped,
            _ => SoundpadPlayStatus.Stopped,
        };
    }

    public static bool IsPausedTitle(string? titleText)
        => titleText != null && PausedTitleRegex().IsMatch(titleText);

    public static string ParseNowPlayingTitle(string? titleText)
    {
        if (string.IsNullOrWhiteSpace(titleText) || IsErrorResponse(titleText))
            return string.Empty;

        string title = BracketedSegmentRegex().Replace(titleText, " ").Trim();

        var prefix = TitlePrefixRegex().Match(title);
        if (prefix.Success)
            title = title[prefix.Length..].Trim();

        return title;
    }
}
