using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class TwitchSectionViewModel : ObservableObject
{
    /// <summary>
    /// Stand-in values for the preview. Twitch asks the user to lay out a line before they have
    /// ever been live, so the preview uses fixed plausible numbers rather than showing nothing -
    /// the same trick that lets the Spotify section preview with no track playing.
    /// </summary>
    public const string SampleGame = "Beat Saber";

    public const int SampleViewerCount = 1234;
    public const int SampleFollowerCount = 8420;
    public const string SampleStreamTitle = "late night chill stream";

    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public ISettingsProvider<TwitchSettings> TwitchSettingsProvider { get; }
    public INavigationService Navigation { get; }

    /// <summary>The line as the chatbox would get it while the channel is live.</summary>
    [ObservableProperty] private string _livePreview = string.Empty;

    /// <summary>The same line once the stream ends - empty means Twitch shows nothing at all.</summary>
    [ObservableProperty] private string _offlinePreview = string.Empty;

    public TwitchSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<IModuleHost> moduleHost,
        ISettingsProvider<TwitchSettings> twitchSettingsProvider,
        INavigationService nav)
    {
        AppSettings = appSettingsProvider.Value;
        _moduleHost = moduleHost;
        TwitchSettingsProvider = twitchSettingsProvider;
        Navigation = nav;

        // Every control in the section shapes the same line, so one subscription covers them all.
        TwitchSettingsProvider.Value.PropertyChanged += OnTwitchSettingsChanged;
        RefreshPreview();
    }

    /// <summary>
    /// Renders the user's current layout against the sample values. Pure, so a test can prove the
    /// preview and the chatbox agree without a Twitch account.
    /// </summary>
    public static string BuildSampleLine(TwitchSettings settings, bool isLive)
        => TwitchModule.BuildOutputString(
            settings,
            SampleGame,
            SampleViewerCount,
            SampleFollowerCount,
            SampleStreamTitle,
            isLive);

    [RelayCommand]
    private void TwitchSync() => _moduleHost.Value.Twitch?.TriggerManualRefresh();

    private void OnTwitchSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    private void RefreshPreview()
    {
        TwitchSettings settings = TwitchSettingsProvider.Value;
        LivePreview = BuildSampleLine(settings, isLive: true);
        OfflinePreview = BuildSampleLine(settings, isLive: false);
    }
}
