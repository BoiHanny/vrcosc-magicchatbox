using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class NetworkStatisticsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public NetworkStatisticsModule NetworkStatsModule { get; }

    public NetworkStatisticsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<NetworkStatisticsModule> networkStatsModule)
    {
        AppSettings = appSettingsProvider.Value;
        NetworkStatsModule = networkStatsModule.Value;
    }
}
