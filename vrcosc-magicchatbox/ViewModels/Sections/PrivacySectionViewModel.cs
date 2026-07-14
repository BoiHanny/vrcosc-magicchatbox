using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.UI.Dialogs;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for the Privacy &amp; Permissions options page.
/// Shows per-hook approval state and provides commands to manage consent.
/// </summary>
public partial class PrivacySectionViewModel : ObservableObject
{
    private readonly IPrivacyConsentService _consentService;

    [ObservableProperty] private bool _isExpanded = false;

    [ObservableProperty] private ConsentState _hardwareMonitorState;
    [ObservableProperty] private ConsentState _windowActivityState;
    [ObservableProperty] private ConsentState _mediaSessionState;
    [ObservableProperty] private ConsentState _afkSensorState;
    [ObservableProperty] private ConsentState _internetAccessState;
    [ObservableProperty] private ConsentState _vrTrackerBatteryState;
    [ObservableProperty] private ConsentState _networkStatsState;
    [ObservableProperty] private ConsentState _soundpadBridgeState;
    [ObservableProperty] private ConsentState _vrcLogReaderState;

    public PrivacySectionViewModel(IPrivacyConsentService consentService)
    {
        _consentService = consentService;
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
