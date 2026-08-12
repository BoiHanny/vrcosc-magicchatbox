using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public sealed record LyricLine(TimeSpan Start, string Text);

public sealed record LyricTrack
{
    public IReadOnlyList<LyricLine> Lines { get; init; } = Array.Empty<LyricLine>();
    public TimeSpan EmbeddedOffset { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public bool IsSynced => Lines.Count > 0;

    public static readonly LyricTrack Empty = new();
}

public sealed record LyricsQuery
{
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }

    public bool IsUsable => Title.Length > 0 && Artist.Length > 0;

    public string CacheKey =>
        $"{Title.ToLowerInvariant()}|{Artist.ToLowerInvariant()}|{(int)Duration.TotalSeconds}";
}
