using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for component stats (CPU/GPU/RAM/VRAM) options.
/// </summary>
public partial class ComponentStatsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public ComponentStatsModule StatsManager { get; }
    public ComponentStatsViewModel ComponentStats { get; }

    /// <summary>
    /// Initializes the component-stats section ViewModel with its backing module
    /// and the shared app-state wrapper.
    /// </summary>
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
