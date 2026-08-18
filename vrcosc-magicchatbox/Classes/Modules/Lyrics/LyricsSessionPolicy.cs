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
