using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class ComponentStatsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public ComponentStatsModule StatsManager { get; }
    public ComponentStatsViewModel ComponentStats { get; }

    public ComponentStatsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<ComponentStatsModule> statsManager,
        Lazy<ComponentStatsViewModel> componentStats)
    {
        AppSettings = appSettingsProvider.Value;
        StatsManager = statsManager.Value;
        ComponentStats = componentStats.Value;
    }
}
