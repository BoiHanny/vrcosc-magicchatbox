using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for the experimental TikTok Live integration.
/// </summary>
public partial class TikTokLiveSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public TikTokLiveSettings TikTokSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    public TikTokLiveDisplayMode[] DisplayModes { get; } =
    [
        TikTokLiveDisplayMode.SummaryOnly,
        TikTokLiveDisplayMode.EventOverlay,
        TikTokLiveDisplayMode.TransientOnly
    ];

    public TikTokOutputOrder[] OutputOrders { get; } =
    [
        TikTokOutputOrder.ProfileThenLive,
        TikTokOutputOrder.LiveThenProfile
    ];

    public TikTokLiveSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<TikTokLiveSettings> tikTokSettingsProvider,
        Lazy<IModuleHost> moduleHost)
    {
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        TikTokSettings = tikTokSettingsProvider.Value;
        _moduleHost = moduleHost;
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        var tikTok = Modules.TikTokLive;
        if (tikTok == null)
            return;

        await tikTok.RefreshProfileAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var tikTokLive = Modules.TikTokLive;
        if (tikTokLive == null)
            return;

        await tikTokLive.StartAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var tikTokLive = Modules.TikTokLive;
        if (tikTokLive == null)
            return;

        await tikTokLive.StopAsync().ConfigureAwait(false);
    }
}
