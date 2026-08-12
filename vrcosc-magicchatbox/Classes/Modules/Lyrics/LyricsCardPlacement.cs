namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsCardPlacement(bool OnMediaLinkCard, bool OnSpotifyCard)
{
    public static readonly LyricsCardPlacement Nowhere = new(false, false);

    public static LyricsCardPlacement Resolve(
        bool lyricsEnabled,
        bool hasSpotifySource,
        bool hasMediaSource,
        bool mediaLinkEnabled,
        bool spotifyEnabled)
    {
        if (!lyricsEnabled)
            return Nowhere;

        if (hasSpotifySource)
            return new LyricsCardPlacement(false, true);

        if (hasMediaSource)
            return new LyricsCardPlacement(true, false);

        if (!mediaLinkEnabled && spotifyEnabled)
            return new LyricsCardPlacement(false, true);

        return new LyricsCardPlacement(true, false);
    }
}
