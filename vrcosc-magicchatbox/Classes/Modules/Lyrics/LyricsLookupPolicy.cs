using System;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static class LyricsLookupPolicy
{
    public static readonly TimeSpan MaxTrackLength = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinTrackLength = TimeSpan.FromSeconds(25);

    private static readonly string[] NotMusicMarkers =
    {
        "podcast",
        "audiobook",
        "audio book",
        "livestream",
        "live stream",
        "full album",
        "full movie",
        "full episode",
        "full match",
        "gameplay",
        "walkthrough",
        "tutorial",
        "interview",
        "documentary",
    };

    public static bool ShouldLookUp(LyricsQuery query, out string reason)
    {
        reason = string.Empty;

        if (query == null || !query.IsUsable)
        {
            reason = "Not enough track information to search";
            return false;
        }

        if (query.Duration > TimeSpan.Zero)
        {
            if (query.Duration > MaxTrackLength)
            {
                reason = $"Too long to be a song ({query.Duration.TotalMinutes:F0} min), not searching";
                return false;
            }

            if (query.Duration < MinTrackLength)
            {
                reason = "Too short to be a song, not searching";
                return false;
            }
        }

        string haystack = $"{query.Title} {query.Album}".ToLowerInvariant();

        foreach (string marker in NotMusicMarkers)
        {
            if (haystack.Contains(marker, StringComparison.Ordinal))
            {
                reason = "This looks like spoken word rather than music, so not searching";
                return false;
            }
        }

        return true;
    }
}
