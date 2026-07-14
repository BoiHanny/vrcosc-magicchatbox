using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for Weather options.
/// Complete binding surface for WeatherSection.xaml.
/// </summary>
public partial class WeatherSectionViewModel : ObservableObject
{
    private readonly IWeatherService _weatherService;

    public AppSettings AppSettings { get; }
    public WeatherSettings WeatherSettings { get; }
    public IntegrationDisplayState IntegrationDisplay { get; }
    public WeatherOverrideState WeatherOverride { get; }

    /// <summary>
    /// Initializes the weather section ViewModel with the weather module, settings,
    /// app-state, display state, and override-item factory service.
    /// </summary>
    public WeatherSectionViewModel(
        IWeatherService weatherService,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<WeatherSettings> weatherSettingsProvider,
        IntegrationDisplayState integrationDisplay,
        WeatherOverrideState weatherOverride)
    {
        _weatherService = weatherService;
        AppSettings = appSettingsProvider.Value;
        WeatherSettings = weatherSettingsProvider.Value;
        IntegrationDisplay = integrationDisplay;
        WeatherOverride = weatherOverride;
    }

    [RelayCommand]
    private void WeatherSync() => _weatherService.TriggerManualRefresh();
}
