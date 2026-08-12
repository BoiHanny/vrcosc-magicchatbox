namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

/// <summary>
/// Which players lyrics are allowed to follow. Spotify and MediaLink are independent: one switch
/// each, on their own card. The master <c>IntgrLyrics</c> flag stays as the single "is the lyrics
/// module running at all" answer that the OSC provider, the privacy hook and the module lifecycle
/// already key off, and is simply whether either source is on.
/// </summary>
public readonly record struct LyricsSourceSelection(bool Spotify, bool MediaLink)
{
    public static readonly LyricsSourceSelection None = new(false, false);
    public static readonly LyricsSourceSelection Both = new(true, true);

    public bool Any => Spotify || MediaLink;

    public LyricsSourceSelection WithSpotify(bool on) => this with { Spotify = on };
    public LyricsSourceSelection WithMediaLink(bool on) => this with { MediaLink = on };

    /// <summary>
    /// Brings a stored master flag and the two per-source flags back into agreement. Run on load,
    /// where it doubles as the migration: settings written before lyrics had per-source switches
    /// carry only the master, and the per-source flags arrive at their defaults.
    /// </summary>
    public static LyricsSourceSelection Reconcile(bool master, bool spotify, bool mediaLink)
    {
        // Master off is the wholesale answer - the privacy hook and the Lyrics row both express
        // "no lyrics" that way, and neither knows about sources.
        if (!master)
            return None;

        // Master on with neither source recorded is the pre-migration shape. Following both is what
        // that setting did before the split, so nobody's lyrics go quiet on upgrade.
        if (!spotify && !mediaLink)
            return Both;

        return new LyricsSourceSelection(spotify, mediaLink);
    }
}
