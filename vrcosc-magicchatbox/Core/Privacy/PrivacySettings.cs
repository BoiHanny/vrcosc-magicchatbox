using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Core.Privacy;

public partial class PrivacySettings : VersionedSettings
{
    [ObservableProperty] private ConsentState _hardwareMonitorConsent = ConsentState.Unknown;
    [ObservableProperty] private int _hardwareMonitorConsentVersion = 0;
    [ObservableProperty] private DateTime? _hardwareMonitorDecidedAt;

    [ObservableProperty] private ConsentState _windowActivityConsent = ConsentState.Unknown;
    [ObservableProperty] private int _windowActivityConsentVersion = 0;
    [ObservableProperty] private DateTime? _windowActivityDecidedAt;

    [ObservableProperty] private ConsentState _mediaSessionConsent = ConsentState.Unknown;
    [ObservableProperty] private int _mediaSessionConsentVersion = 0;
    [ObservableProperty] private DateTime? _mediaSessionDecidedAt;

    [ObservableProperty] private ConsentState _afkSensorConsent = ConsentState.Unknown;
    [ObservableProperty] private int _afkSensorConsentVersion = 0;
    [ObservableProperty] private DateTime? _afkSensorDecidedAt;

    [ObservableProperty] private ConsentState _internetAccessConsent = ConsentState.Unknown;
    [ObservableProperty] private int _internetAccessConsentVersion = 0;
    [ObservableProperty] private DateTime? _internetAccessDecidedAt;

    [ObservableProperty] private ConsentState _vrTrackerBatteryConsent = ConsentState.Unknown;
    [ObservableProperty] private int _vrTrackerBatteryConsentVersion = 0;
    [ObservableProperty] private DateTime? _vrTrackerBatteryDecidedAt;

    [ObservableProperty] private ConsentState _networkStatsConsent = ConsentState.Unknown;
    [ObservableProperty] private int _networkStatsConsentVersion = 0;
    [ObservableProperty] private DateTime? _networkStatsDecidedAt;

    [ObservableProperty] private ConsentState _soundpadBridgeConsent = ConsentState.Unknown;
    [ObservableProperty] private int _soundpadBridgeConsentVersion = 0;
    [ObservableProperty] private DateTime? _soundpadBridgeDecidedAt;
}
