namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsCardPlacement(bool OnMediaLinkCard, bool OnSpotifyCard)
{
    public static readonly LyricsCardPlacement Nowhere = new(false, false);

    public static LyricsCardPlacement Resolve(
        bool lyricsEnabled,
        bool hasSpotifySource,
        bool hasMediaSource,
        bool mediaLinkEnabled,
        bool spotifyEnabled,
        bool lyricsOnMediaLink = true,
        bool lyricsOnSpotify = true)
    {
        if (!lyricsEnabled)
            return Nowhere;

        if (hasSpotifySource)
            return new LyricsCardPlacement(false, true);

        if (hasMediaSource)
            return new LyricsCardPlacement(true, false);

        if (lyricsOnMediaLink && mediaLinkEnabled)
            return new LyricsCardPlacement(true, false);

        if (lyricsOnSpotify && spotifyEnabled)
            return new LyricsCardPlacement(false, true);

        if (lyricsOnMediaLink)
            return new LyricsCardPlacement(true, false);

        if (lyricsOnSpotify)
            return new LyricsCardPlacement(false, true);

        return Nowhere;
    }
}
