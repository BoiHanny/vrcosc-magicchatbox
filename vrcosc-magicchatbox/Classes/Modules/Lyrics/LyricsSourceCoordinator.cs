namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public static class LyricsSourceCoordinator
{
    public static LyricsSourceSelection Read(IntegrationSettings settings)
        => new(settings.IntgrLyrics_Spotify, settings.IntgrLyrics_MediaLink);

    public static void Write(IntegrationSettings settings, LyricsSourceSelection sources)
    {
        settings.IntgrLyrics_Spotify = sources.Spotify;
        settings.IntgrLyrics_MediaLink = sources.MediaLink;
        settings.IntgrLyrics = sources.Any;
    }

    public static LyricsSourceSelection SyncWithMaster(IntegrationSettings settings)
    {
        var reconciled = LyricsSourceSelection.Reconcile(
            settings.IntgrLyrics,
            settings.IntgrLyrics_Spotify,
            settings.IntgrLyrics_MediaLink);

        settings.IntgrLyrics_Spotify = reconciled.Spotify;
        settings.IntgrLyrics_MediaLink = reconciled.MediaLink;
        return reconciled;
    }
}
