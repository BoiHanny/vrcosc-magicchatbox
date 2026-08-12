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
        IntegrationSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HostSummary));
    }

    public LyricsSettings Settings { get; }
    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public LyricsDisplayState Display { get; }

    public string HostSummary
    {
        get
        {
            bool media = IntegrationSettings.IntgrScanMediaLink;
            bool spotify = IntegrationSettings.IntgrSpotify;

            if (media && spotify)
                return "Spotify is used when it is playing, otherwise whatever Windows is playing.";

            if (spotify)
                return "Following Spotify. Turn on MediaLink to also follow browsers and other players.";

            if (media)
                return "Following whatever Windows is playing. Turn on Spotify for its own track timing.";

            return "Lyrics have no source yet. Turn on MediaLink or Spotify on the Integrations page.";
        }
    }

    public IReadOnlyList<LyricsMediaCoexistence> CoexistenceModes { get; } =
        (LyricsMediaCoexistence[])Enum.GetValues(typeof(LyricsMediaCoexistence));

    public string RoomyPreview => Preview(90);

    public string TightPreview => Preview(40);

    public string OffsetSummary => Settings.OffsetMs == 0
        ? "In sync"
        : Settings.OffsetMs > 0
            ? $"Lyrics run {Settings.OffsetMs} ms early"
            : $"Lyrics run {Math.Abs(Settings.OffsetMs)} ms late";

    [RelayCommand]
    private void NudgeOffset(string amount)
    {
        if (!int.TryParse(amount, out int delta))
            return;

        Settings.OffsetMs = Math.Clamp(Settings.OffsetMs + delta, -10000, 10000);
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void ResetOffset()
    {
        Settings.OffsetMs = 0;
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
