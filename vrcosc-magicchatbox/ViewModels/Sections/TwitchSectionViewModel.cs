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
    public const string SampleGame = "Beat Saber";

    public const int SampleViewerCount = 1234;
    public const int SampleFollowerCount = 8420;
    public const string SampleStreamTitle = "late night chill stream";

    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public ISettingsProvider<TwitchSettings> TwitchSettingsProvider { get; }
    public INavigationService Navigation { get; }

    [ObservableProperty] private string _livePreview = string.Empty;

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

        TwitchSettingsProvider.Value.PropertyChanged += OnTwitchSettingsChanged;
        RefreshPreview();
    }

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
