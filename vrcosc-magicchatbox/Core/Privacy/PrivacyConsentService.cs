using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Core.Privacy;

public sealed class PrivacyConsentService : IPrivacyConsentService
{
    private readonly ISettingsProvider<PrivacySettings> _provider;
    private PrivacySettings Settings => _provider.Value;

    public event EventHandler<ConsentChangedEventArgs> ConsentChanged;

    public PrivacyConsentService(ISettingsProvider<PrivacySettings> provider)
    {
        _provider = provider;
    }

    public bool IsApproved(PrivacyHook hook) => GetState(hook) == ConsentState.Approved;

    public ConsentState GetState(PrivacyHook hook) => hook switch
    {
        PrivacyHook.HardwareMonitor => Settings.HardwareMonitorConsent,
        PrivacyHook.WindowActivity => Settings.WindowActivityConsent,
        PrivacyHook.MediaSession => Settings.MediaSessionConsent,
        PrivacyHook.AfkSensor => Settings.AfkSensorConsent,
        PrivacyHook.InternetAccess => Settings.InternetAccessConsent,
        PrivacyHook.VrTrackerBattery => Settings.VrTrackerBatteryConsent,
        PrivacyHook.NetworkStats => Settings.NetworkStatsConsent,
        PrivacyHook.SoundpadBridge => Settings.SoundpadBridgeConsent,
        PrivacyHook.VrcLogReader => Settings.VrcLogReaderConsent,
        _ => ConsentState.Unknown,
    };

    public void Approve(PrivacyHook hook) => SetState(hook, ConsentState.Approved);
    public void Deny(PrivacyHook hook) => SetState(hook, ConsentState.Denied);
    public void Reset(PrivacyHook hook) => SetState(hook, ConsentState.Unknown);

    public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
        => hooks.Where(h => GetState(h) == ConsentState.Unknown).ToList();

    private void SetState(PrivacyHook hook, ConsentState newState)
    {
        switch (hook)
        {
            case PrivacyHook.HardwareMonitor:
                Settings.HardwareMonitorConsent = newState;
                Settings.HardwareMonitorDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.WindowActivity:
                Settings.WindowActivityConsent = newState;
                Settings.WindowActivityDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.MediaSession:
                Settings.MediaSessionConsent = newState;
                Settings.MediaSessionDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.AfkSensor:
                Settings.AfkSensorConsent = newState;
                Settings.AfkSensorDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.InternetAccess:
                Settings.InternetAccessConsent = newState;
                Settings.InternetAccessDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.VrTrackerBattery:
                Settings.VrTrackerBatteryConsent = newState;
                Settings.VrTrackerBatteryDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.NetworkStats:
                Settings.NetworkStatsConsent = newState;
                Settings.NetworkStatsDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.SoundpadBridge:
                Settings.SoundpadBridgeConsent = newState;
                Settings.SoundpadBridgeDecidedAt = DateTime.UtcNow;
                break;
            case PrivacyHook.VrcLogReader:
                Settings.VrcLogReaderConsent = newState;
                Settings.VrcLogReaderDecidedAt = DateTime.UtcNow;
                break;
        }

        _provider.Save();
        ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, newState));
    }
}
