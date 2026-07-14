using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for Tracker Battery options.
/// Complete binding surface for TrackerBatterySection.xaml.
/// </summary>
public partial class TrackerBatterySectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly TrackerDisplayState _trackerDisplay;
    private readonly ISettingsProvider<TrackerBatterySettings> _settingsProvider;

    public AppSettings AppSettings { get; }
    public IntegrationDisplayState IntegrationDisplay { get; }
    public TrackerDisplayState Tracker { get; }
    public IModuleHost Modules => _moduleHost.Value;

    /// <summary>
    /// Initializes the tracker-battery section ViewModel with the tracker module,
    /// app-state, settings, and display-state dependencies.
    /// </summary>
    public TrackerBatterySectionViewModel(
        Lazy<IModuleHost> moduleHost,
        TrackerDisplayState trackerDisplay,
        ISettingsProvider<TrackerBatterySettings> settingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IntegrationDisplayState integrationDisplay)
    {
        _moduleHost = moduleHost;
        _trackerDisplay = trackerDisplay;
        _settingsProvider = settingsProvider;
        AppSettings = appSettingsProvider.Value;
        IntegrationDisplay = integrationDisplay;
        Tracker = trackerDisplay;
    }

    [RelayCommand]
    private void TrackerBatteryScan()
    {
        _moduleHost.Value.TrackerBattery?.UpdateDevices();
        _moduleHost.Value.TrackerBattery?.BuildChatboxString();
    }

    [RelayCommand]
    private void ResetTrackerDevices()
    {
        var result = MessageBox.Show(
            "Reset all tracker device customizations and forget known devices?",
            "Reset devices",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var tracker = _moduleHost.Value.TrackerBattery;
        _trackerDisplay.TrackerDevices.Clear();
        tracker?.UpdateDevices();
        tracker?.BuildChatboxString();

        _settingsProvider.Value.SavedDevices = _trackerDisplay.TrackerDevices;
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void ResetTrackerBatteryTemplate()
    {
        var settings = _settingsProvider.Value;
        settings.Template = "{icon} {name} {batt}%";
        settings.Prefix = string.Empty;
        settings.Separator = " | ";
        settings.Suffix = string.Empty;
        settings.LowTag = "LOW";
        settings.OnlineText = "Online";
        settings.OfflineText = "Offline";
        settings.OfflineBatteryText = "N/A";
        settings.CompactWhitespace = true;
        _moduleHost.Value.TrackerBattery?.BuildChatboxString();
        _settingsProvider.Save();
    }
}
