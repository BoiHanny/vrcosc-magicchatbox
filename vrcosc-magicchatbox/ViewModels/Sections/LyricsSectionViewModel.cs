using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class LyricsSectionViewModel : ObservableObject
{
    private const string SampleLine = "and I'm still not sure what I'm looking for in all of these places";

    private readonly ISettingsProvider<LyricsSettings> _settingsProvider;

    public LyricsSectionViewModel(
        ISettingsProvider<LyricsSettings> settingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        LyricsDisplayState display)
    {
        _settingsProvider = settingsProvider;
        Settings = settingsProvider.Value;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        Display = display;

        Settings.PropertyChanged += (_, _) => RefreshPreview();
        IntegrationSettings.PropertyChanged += (_, e) =>
        {
            // The master can move without this section touching it (privacy, the Integrations page),
            // and the per-source flags have to follow it back.
            if (e.PropertyName == nameof(IntegrationSettings.IntgrLyrics))
                LyricsSourceCoordinator.SyncWithMaster(IntegrationSettings);

            OnPropertyChanged(nameof(LyricsFromSpotify));
            OnPropertyChanged(nameof(LyricsFromMediaLink));
            OnPropertyChanged(nameof(HostSummary));
        };
    }

    public LyricsSettings Settings { get; }
    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public LyricsDisplayState Display { get; }

    public bool LyricsFromSpotify
    {
        get => IntegrationSettings.IntgrLyrics_Spotify;
        set => ApplyLyricSources(LyricsSourceCoordinator.Read(IntegrationSettings).WithSpotify(value));
    }

    public bool LyricsFromMediaLink
    {
        get => IntegrationSettings.IntgrLyrics_MediaLink;
        set => ApplyLyricSources(LyricsSourceCoordinator.Read(IntegrationSettings).WithMediaLink(value));
    }

    private void ApplyLyricSources(LyricsSourceSelection sources)
    {
        LyricsSourceCoordinator.Write(IntegrationSettings, sources);
        OnPropertyChanged(nameof(LyricsFromSpotify));
        OnPropertyChanged(nameof(LyricsFromMediaLink));
        OnPropertyChanged(nameof(HostSummary));
    }

    /// <summary>
    /// Says what will actually happen, which means answering two separate questions: whether lyrics
    /// are switched on for a player, and whether that player is switched on in the first place.
    /// </summary>
    public string HostSummary
    {
        get
        {
            bool spotify = LyricsFromSpotify;
            bool media = LyricsFromMediaLink;

            if (!spotify && !media)
                return "Lyrics are off. Switch on a player above to start following it.";

            bool spotifyHost = IntegrationSettings.IntgrSpotify;
            bool mediaHost = IntegrationSettings.IntgrScanMediaLink;

            if (spotify && media)
            {
                if (!spotifyHost && !mediaHost)
                    return "Neither player is switched on, so there is nothing to follow yet. Turn on Spotify or Media link on the Integrations page.";
                if (!spotifyHost)
                    return "Following whatever Windows is playing. Spotify is picked first whenever it is playing, but the Spotify integration is switched off.";
                if (!mediaHost)
                    return "Following Spotify. Turn on Media link on the Integrations page to also follow browsers and other players.";

                return "Spotify is used whenever it is playing, otherwise whatever Windows is playing.";
            }

            if (spotify)
                return spotifyHost
                    ? "Following Spotify only. Anything else Windows plays is ignored."
                    : "Set to follow Spotify only, but the Spotify integration is switched off on the Integrations page.";

            return mediaHost
                ? "Following whatever Windows is playing, including Spotify's own window. Spotify's own track timing is not used."
                : "Set to follow whatever Windows is playing, but Media link is switched off on the Integrations page.";
        }
    }

    public IReadOnlyList<LyricsMediaCoexistence> CoexistenceModes { get; } =
        (LyricsMediaCoexistence[])Enum.GetValues(typeof(LyricsMediaCoexistence));

    public string RoomyPreview => Preview(90);

    public string TightPreview => Preview(40);

    public string OffsetSummary => LyricsTuning.FormatOffsetSummary(Settings.OffsetMs);

    /// <summary>
    /// Compact form for the Integrations ribbon pill; <see cref="OffsetSummary" /> stays the long
    /// form for the Options page, which has a whole card to spend on it.
    /// </summary>
    public string OffsetChip => LyricsTuning.FormatOffsetChip(Settings.OffsetMs);

    /// <summary>Non-null when the hold silently disables the ♪ break marker.</summary>
    public string? TimingWarning
        => LyricsTuning.DescribeTimingConflict(Settings.GapThresholdSeconds, Settings.LineHoldSeconds);

    public bool HasTimingWarning => TimingWarning != null;

    [RelayCommand]
    private void NudgeOffset(string amount)
    {
        if (!LyricsTuning.TryParseDelta(amount, out int delta))
            return;

        Settings.OffsetMs = LyricsTuning.NudgeOffsetMs(Settings.OffsetMs, delta);
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void ResetOffset()
    {
        Settings.OffsetMs = 0;
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void NudgeGapThreshold(string amount)
    {
        if (!LyricsTuning.TryParseDelta(amount, out int delta))
            return;

        Settings.GapThresholdSeconds = LyricsTuning.NudgeGapThresholdSeconds(Settings.GapThresholdSeconds, delta);
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void NudgeLineHold(string amount)
    {
        if (!LyricsTuning.TryParseDelta(amount, out int delta))
            return;

        Settings.LineHoldSeconds = LyricsTuning.NudgeLineHoldSeconds(Settings.LineHoldSeconds, delta);
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void BrowseLocalFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder containing .lrc files",
            Multiselect = false,
        };

        if (Directory.Exists(Settings.LocalLyricsFolder))
            dialog.InitialDirectory = Settings.LocalLyricsFolder;

        if (dialog.ShowDialog() == true)
        {
            Settings.LocalLyricsFolder = dialog.FolderName;
            _settingsProvider.Save();
        }
    }

    private void RefreshPreview()
    {
        OnPropertyChanged(nameof(RoomyPreview));
        OnPropertyChanged(nameof(TightPreview));
        OnPropertyChanged(nameof(OffsetSummary));
        OnPropertyChanged(nameof(OffsetChip));
        OnPropertyChanged(nameof(TimingWarning));
        OnPropertyChanged(nameof(HasTimingWarning));
    }

    private string Preview(int budget)
    {
        var cursor = new LyricCursor(
            LyricCursorKind.Line,
            0,
            SampleLine,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(6));

        string text = LyricSegmentFormatter.Build(cursor, TimeSpan.FromSeconds(3), budget, Settings);
        return text.Length == 0 ? "(nothing shown)" : text;
    }
}
