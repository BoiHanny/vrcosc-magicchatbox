using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class WeatherSectionViewModel : ObservableObject
{
    private readonly IWeatherService _weatherService;

    public AppSettings AppSettings { get; }
    public WeatherSettings WeatherSettings { get; }
    public IntegrationDisplayState IntegrationDisplay { get; }
    public WeatherOverrideState WeatherOverride { get; }

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

        WeatherSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OutputPreview));
    }

    public string OutputPreview => _weatherService.BuildSampleWeatherText();

    [RelayCommand]
    private void WeatherSync() => _weatherService.TriggerManualRefresh();
}
