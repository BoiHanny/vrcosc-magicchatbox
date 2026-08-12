using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public enum LyricsSessionPlayback
{
    Playing,
    Paused,
    Stopped,
}

public readonly record struct LyricsSessionCandidate(
    int Index,
    LyricsSessionPlayback Playback,
    bool IsActive,
    bool HasTitle);

public readonly record struct LyricsSessionChoice(int Index, bool IsPlaying)
{
    public static readonly LyricsSessionChoice None = new(-1, false);
    public bool HasSession => Index >= 0;
}

/// <summary>
/// Picks which media session lyrics should follow, and - the part that matters - reports whether it
/// is actually playing.
///
/// A paused session still has a track and a position, so it stays the chosen source: dropping it
/// would throw away the loaded lyrics and re-look them up the moment playback resumed. What it must
/// not do is keep showing a line. Position is frozen while paused, so the scheduler resolves the
/// same lyric for as long as the pause lasts, and because a visible lyric also hides the song title,
/// pausing used to leave a stale line on screen with no way to see that anything was paused at all.
/// </summary>
public static class LyricsSessionPolicy
{
    public static LyricsSessionChoice Choose(IReadOnlyList<LyricsSessionCandidate> sessions)
    {
        if (sessions == null || sessions.Count == 0)
            return LyricsSessionChoice.None;

        foreach (var session in sessions)
            if (session.Playback == LyricsSessionPlayback.Playing && session.HasTitle)
                return new LyricsSessionChoice(session.Index, true);

        foreach (var session in sessions)
            if (session.IsActive && session.Playback == LyricsSessionPlayback.Paused && session.HasTitle)
                return new LyricsSessionChoice(session.Index, false);

        foreach (var session in sessions)
            if (session.IsActive && session.HasTitle)
                return new LyricsSessionChoice(session.Index, false);

        return LyricsSessionChoice.None;
    }
}
