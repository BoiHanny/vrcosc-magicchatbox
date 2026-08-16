using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.UI.Dialogs;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class PrivacySectionViewModel : ObservableObject
{
    private readonly IPrivacyConsentService _consentService;

    public AppSettings AppSettings { get; }

    public bool IsExpanded
    {
        get => AppSettings.Settings_Privacy;
        set => AppSettings.Settings_Privacy = value;
    }

    [ObservableProperty] private ConsentState _hardwareMonitorState;
    [ObservableProperty] private ConsentState _windowActivityState;
    [ObservableProperty] private ConsentState _mediaSessionState;
    [ObservableProperty] private ConsentState _afkSensorState;
    [ObservableProperty] private ConsentState _internetAccessState;
    [ObservableProperty] private ConsentState _vrTrackerBatteryState;
    [ObservableProperty] private ConsentState _networkStatsState;
    [ObservableProperty] private ConsentState _soundpadBridgeState;
    [ObservableProperty] private ConsentState _vrcLogReaderState;
    [ObservableProperty] private ConsentState _vrPerformanceState;
    [ObservableProperty] private ConsentState _sharedLayoutImportState;

    public PrivacySectionViewModel(
        IPrivacyConsentService consentService,
        ISettingsProvider<AppSettings> appSettingsProvider)
    {
        _consentService = consentService;
        AppSettings = appSettingsProvider.Value;
        _consentService.ConsentChanged += (_, _) => RefreshStates();
        RefreshStates();
    }

    private void RefreshStates()
    {
        HardwareMonitorState = _consentService.GetState(PrivacyHook.HardwareMonitor);
        WindowActivityState = _consentService.GetState(PrivacyHook.WindowActivity);
        MediaSessionState = _consentService.GetState(PrivacyHook.MediaSession);
        AfkSensorState = _consentService.GetState(PrivacyHook.AfkSensor);
        InternetAccessState = _consentService.GetState(PrivacyHook.InternetAccess);
        VrTrackerBatteryState = _consentService.GetState(PrivacyHook.VrTrackerBattery);
        NetworkStatsState = _consentService.GetState(PrivacyHook.NetworkStats);
        SoundpadBridgeState = _consentService.GetState(PrivacyHook.SoundpadBridge);
        VrcLogReaderState = _consentService.GetState(PrivacyHook.VrcLogReader);
        VrPerformanceState = _consentService.GetState(PrivacyHook.VrPerformance);
        SharedLayoutImportState = _consentService.GetState(PrivacyHook.SharedLayoutImport);
    }

    [RelayCommand]
    private void ManageHook(PrivacyHook hook)
    {
        var dialog = new PrivacyConsentDialog(_consentService, new[] { hook });
        DialogWindowHelper.PrepareModal(dialog);
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void RevokeHook(PrivacyHook hook) => _consentService.Deny(hook);

    [RelayCommand]
    private void ResetHook(PrivacyHook hook) => _consentService.Reset(hook);
}
