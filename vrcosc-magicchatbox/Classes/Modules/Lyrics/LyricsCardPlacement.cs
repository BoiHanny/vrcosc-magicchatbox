namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsCardPlacement(bool OnMediaLinkCard, bool OnSpotifyCard)
{
    public static readonly LyricsCardPlacement Nowhere = new(false, false);

    /// <summary>
    /// Where the lyrics ribbon should sit.
    ///
    /// Two separate questions decide it, and conflating them is a mistake worth naming: whether
    /// lyrics are switched on for a player, and whether that player's integration is running at all.
    /// The ribbon may only ever land on a card whose own lyrics switch is on - otherwise switching
    /// lyrics off on Media link left a panel sitting on the Media link tile reporting "Paused".
    /// But with lyrics on and no integration running it still has to land somewhere, because that is
    /// the only place it can explain that there is nothing to follow yet.
    /// </summary>
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

        // Nothing is playing, so this is only deciding where the ribbon parks. Prefer a card whose
        // integration is actually running, then fall back to one that merely has lyrics switched on
        // so the "turn something on" message has a home.
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
