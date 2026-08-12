using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class TwitchSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public ISettingsProvider<TwitchSettings> TwitchSettingsProvider { get; }
    public INavigationService Navigation { get; }

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
    }

    [RelayCommand]
    private void TwitchSync() => _moduleHost.Value.Twitch?.TriggerManualRefresh();
}
