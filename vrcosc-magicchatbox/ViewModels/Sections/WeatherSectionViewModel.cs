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

        // The template alone has nine placeholders and no other feedback. Every setting on this
        // page reshapes one line, so the preview has to move the moment any of them does - the
        // custom icon and text boxes included, which write through WeatherConditionOverrides.
        WeatherSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OutputPreview));
    }

    /// <summary>
    /// The weather line as the chatbox would receive it, built from fixed sample readings so it
    /// works before the first sync and with no location shared.
    /// </summary>
    public string OutputPreview => _weatherService.BuildSampleWeatherText();

    [RelayCommand]
    private void WeatherSync() => _weatherService.TriggerManualRefresh();
}
