using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsSourceCandidate(string Title, string State);

public static class LyricsSourceStatus
{
    public const string NoHost = "Turn on MediaLink or Spotify. Lyrics follow whichever one is playing";
    public const string SpotifyIdle = "Waiting for Spotify to start playing";
    public const string NothingPlaying = "Nothing playing";

    private const int MaxListedSessions = 3;

    public static string Describe(
        bool mediaLinkEnabled,
        bool spotifyEnabled,
        IReadOnlyList<LyricsSourceCandidate>? sessions)
    {
        if (!mediaLinkEnabled && !spotifyEnabled)
            return NoHost;

        if (sessions == null || sessions.Count == 0)
        {
            if (mediaLinkEnabled)
                return NothingPlaying;

            return SpotifyIdle;
        }

        string states = string.Join(", ", sessions.Take(MaxListedSessions).Select(Label));
        return $"Waiting for playback: {states}";
    }

    private static string Label(LyricsSourceCandidate candidate)
    {
        string title = string.IsNullOrWhiteSpace(candidate.Title) ? "untitled" : candidate.Title;
        return string.IsNullOrWhiteSpace(candidate.State) ? title : $"{title} ({candidate.State})";
    }
}
