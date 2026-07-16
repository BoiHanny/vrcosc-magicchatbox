using System;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Services;

/// <summary>Play state reported by Soundpad's GetPlayStatus() remote-control query.</summary>
public enum SoundpadPlayStatus
{
    /// <summary>Status could not be determined (no response or an R-xxx error).</summary>
    Unknown,
    Stopped,
    Playing,
    Paused,
    Seeking
}

/// <summary>
/// Parses responses from Soundpad's remote-control pipe. Action commands (Do*) answer with
/// "R-&lt;code&gt;" status strings; queries (Get*/Is*) answer with a bare payload on success
/// and an "R-&lt;code&gt;" string on failure.
/// </summary>
public static partial class SoundpadStatusParser
{
    [GeneratedRegex(@"^R-\d")]
    private static partial Regex ErrorCodeRegex();

    [GeneratedRegex(@"\s*\[.*?\]\s*")]
    private static partial Regex BracketedSegmentRegex();

    // Live-verified format (Soundpad 4.0.30): idle "Soundpad", playing "Soundpad - <name>",
    // paused " II  Soundpad - <name>" — the pause marker precedes "Soundpad".
    [GeneratedRegex(@"^(?:II\s+)?Soundpad(\s*-\s*|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TitlePrefixRegex();

    [GeneratedRegex(@"^\s*II\s+Soundpad(\s|-|$)", RegexOptions.IgnoreCase)]
    private static partial Regex PausedTitleRegex();

    /// <summary>True when an action command was accepted ("R-200", possibly with trailing text).</summary>
    public static bool IsSuccessResponse(string? response)
        => response != null && response.StartsWith("R-200", StringComparison.Ordinal);

    /// <summary>True when a query answered with an error code instead of a payload.</summary>
    public static bool IsErrorResponse(string? response)
        => response == null || ErrorCodeRegex().IsMatch(response);

    /// <summary>
    /// Maps a GetPlayStatus() response to a play state. Unparseable-but-present responses map
    /// to Stopped (matching the official reference client); missing responses and error codes
    /// map to Unknown so callers can fall back to other status sources.
    /// </summary>
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

    /// <summary>
    /// True when a GetTitleText() response (or scraped window title) carries the leading
    /// pause marker, e.g. " II  Soundpad - airhorn.mp3".
    /// </summary>
    public static bool IsPausedTitle(string? titleText)
        => titleText != null && PausedTitleRegex().IsMatch(titleText);

    /// <summary>
    /// Extracts the sound name from a GetTitleText() response (or a scraped window title),
    /// e.g. "Soundpad - airhorn.mp3" → "airhorn.mp3". Returns an empty string when nothing is
    /// playing ("Soundpad" alone) or the response is unusable. Only the leading frame-title
    /// decorations are stripped, so sound names that legitimately contain "II" or "-" survive.
    /// </summary>
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
