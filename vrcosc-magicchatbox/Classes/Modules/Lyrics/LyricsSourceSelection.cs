namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public readonly record struct LyricsSourceSelection(bool Spotify, bool MediaLink)
{
    public static readonly LyricsSourceSelection None = new(false, false);
    public static readonly LyricsSourceSelection Both = new(true, true);

    public bool Any => Spotify || MediaLink;

    public LyricsSourceSelection WithSpotify(bool on) => this with { Spotify = on };
    public LyricsSourceSelection WithMediaLink(bool on) => this with { MediaLink = on };

    public static LyricsSourceSelection Reconcile(bool master, bool spotify, bool mediaLink)
    {
        if (!master)
            return None;

        if (!spotify && !mediaLink)
            return Both;

        return new LyricsSourceSelection(spotify, mediaLink);
    }
}
