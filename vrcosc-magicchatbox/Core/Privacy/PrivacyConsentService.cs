using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Core.Privacy;

public sealed class PrivacyConsentService : IPrivacyConsentService
{
    private readonly ISettingsProvider<PrivacySettings> _provider;
    private readonly object _stateLock = new();
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
        _ => throw new ArgumentOutOfRangeException(nameof(hook), hook, "Unknown privacy hook."),
    };

    public void Approve(PrivacyHook hook) => SetState(hook, ConsentState.Approved);
    public void Deny(PrivacyHook hook) => SetState(hook, ConsentState.Denied);
    public void Reset(PrivacyHook hook) => SetState(hook, ConsentState.Unknown);

    public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
        => hooks.Where(h => GetState(h) == ConsentState.Unknown).ToList();

    private void SetState(PrivacyHook hook, ConsentState newState)
    {
        // Mutate + persist under lock so concurrent Approve/Deny/Reset calls cannot
        // interleave a half-updated PrivacySettings into the save pipeline. The
        // ConsentChanged event is fired AFTER the lock is released so subscribers
        // (some of which dispatch UI work) cannot deadlock against another caller.
        lock (_stateLock)
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(hook), hook, "Unknown privacy hook.");
            }

            try
            {
                _provider.Save();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        // Notify subscribers outside the lock. Isolate failures per-handler so a
        // misbehaving subscriber cannot starve later subscribers of the event.
        var handler = ConsentChanged;
        if (handler == null)
            return;

        var args = new ConsentChangedEventArgs(hook, newState);
        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<ConsentChangedEventArgs>)subscriber).Invoke(this, args);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }
    }
}
