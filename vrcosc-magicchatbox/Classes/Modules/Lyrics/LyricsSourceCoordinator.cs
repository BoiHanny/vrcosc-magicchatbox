namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

/// <summary>
/// The one place the three lyrics flags are written together. Both the Integrations cards and the
/// Options section offer the same per-source switches, and they have to agree on what the master
/// means, or turning lyrics off in one place leaves the other claiming it is still on.
/// </summary>
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

    /// <summary>
    /// Pulls the per-source flags back in line after something has moved the master on its own -
    /// the privacy guard revoking Internet Access, or a plain "lyrics off" somewhere else.
    /// </summary>
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
