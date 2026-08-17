using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Services.Scope;

namespace vrcosc_magicchatbox.Core.Integrations;

public sealed class IntegrationGate : IIntegrationGate
{
    private readonly ScopeRuntime _scope;
    private readonly IPrivacyConsentService? _consent;

    public IntegrationGate(ScopeRuntime scope, IPrivacyConsentService? consent = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _consent = consent;
    }

    public bool Permits(string integrationKey)
    {
        if (string.IsNullOrWhiteSpace(integrationKey))
            return true;

        string key = IntegrationTileCatalog.ResolveKey(integrationKey);

        if (!ConsentAllows(key))
            return false;

        return _scope.PermitsIntegration(key);
    }

    public bool PermitsSending() => _scope.PermitsSending();

    public bool TryDescribe(string integrationKey, out ScopeDecision decision) =>
        _scope.TryDescribeIntegration(IntegrationTileCatalog.ResolveKey(integrationKey), out decision);

    private bool ConsentAllows(string key)
    {
        if (_consent is null)
            return true;

        if (!IntegrationConsent.TryHookFor(key, out PrivacyHook hook))
            return true;

        try
        {
            return _consent.IsApproved(hook);
        }
        catch
        {
            return true;
        }
    }
}

public static class IntegrationConsent
{
    public static bool TryHookFor(string integrationKey, out PrivacyHook hook)
    {
        switch (IntegrationTileCatalog.ResolveKey(integrationKey))
        {
            case "Window":
                hook = PrivacyHook.WindowActivity;
                return true;
            case "Component":
                hook = PrivacyHook.HardwareMonitor;
                return true;
            case "Network":
                hook = PrivacyHook.NetworkStats;
                return true;
            case "TrackerBattery":
                hook = PrivacyHook.VrTrackerBattery;
                return true;
            case "VrPerformance":
                hook = PrivacyHook.VrPerformance;
                return true;
            case "Soundpad":
                hook = PrivacyHook.SoundpadBridge;
                return true;
            case "VrcRadar":
                hook = PrivacyHook.VrcLogReader;
                return true;
            case "MediaLink":
                hook = PrivacyHook.MediaSession;
                return true;
            default:
                hook = default;
                return false;
        }
    }
}
