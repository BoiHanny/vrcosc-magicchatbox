using System;
using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules;

public sealed record IntegrationModeGate(
    string MasterPropertyName,
    string DisplayName,
    Func<IntegrationSettings, bool> IsMasterEnabled,
    Func<IntegrationSettings, bool> IsVisibleInVr,
    Action<IntegrationSettings>? EnableInVr,
    Func<IntegrationSettings, bool> IsVisibleOnDesktop,
    Action<IntegrationSettings>? EnableOnDesktop)
{
    public bool IsVisibleIn(IntegrationSettings settings, bool isVR)
        => isVR ? IsVisibleInVr(settings) : IsVisibleOnDesktop(settings);

    public bool CanEnableIn(bool isVR)
        => (isVR ? EnableInVr : EnableOnDesktop) is not null;

    public void EnableIn(IntegrationSettings settings, bool isVR)
        => (isVR ? EnableInVr : EnableOnDesktop)?.Invoke(settings);
}

public readonly record struct HiddenIntegration(string DisplayName, bool CanEnableInCurrentMode);

public static class IntegrationModeVisibility
{
    public static IReadOnlyList<IntegrationModeGate> Gates { get; } = new IntegrationModeGate[]
    {
        new(nameof(IntegrationSettings.IntgrStatus), "Status",
            s => s.IntgrStatus,
            s => s.IntgrStatus_VR, s => s.IntgrStatus_VR = true,
            s => s.IntgrStatus_DESKTOP, s => s.IntgrStatus_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrScanWindowActivity), "Window Activity",
            s => s.IntgrScanWindowActivity,
            s => s.IntgrWindowActivity_VR, s => s.IntgrWindowActivity_VR = true,
            s => s.IntgrWindowActivity_DESKTOP, s => s.IntgrWindowActivity_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrScanWindowTime), "Current Time",
            s => s.IntgrScanWindowTime,
            s => s.IntgrCurrentTime_VR, s => s.IntgrCurrentTime_VR = true,
            s => s.IntgrCurrentTime_DESKTOP, s => s.IntgrCurrentTime_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrScanMediaLink), "MediaLink",
            s => s.IntgrScanMediaLink,
            s => s.IntgrMediaLink_VR, s => s.IntgrMediaLink_VR = true,
            s => s.IntgrMediaLink_DESKTOP, s => s.IntgrMediaLink_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrComponentStats), "Component Stats",
            s => s.IntgrComponentStats,
            s => s.IntgrComponentStats_VR, s => s.IntgrComponentStats_VR = true,
            s => s.IntgrComponentStats_DESKTOP, s => s.IntgrComponentStats_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrNetworkStatistics), "Network Statistics",
            s => s.IntgrNetworkStatistics,
            s => s.IntgrNetworkStatistics_VR, s => s.IntgrNetworkStatistics_VR = true,
            s => s.IntgrNetworkStatistics_DESKTOP, s => s.IntgrNetworkStatistics_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrHeartRate), "Heart Rate",
            s => s.IntgrHeartRate,
            s => s.IntgrHeartRate_VR, s => s.IntgrHeartRate_VR = true,
            s => s.IntgrHeartRate_DESKTOP, s => s.IntgrHeartRate_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrSoundpad), "Soundpad",
            s => s.IntgrSoundpad,
            s => s.IntgrSoundpad_VR, s => s.IntgrSoundpad_VR = true,
            s => s.IntgrSoundpad_DESKTOP, s => s.IntgrSoundpad_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrSpotify), "Spotify",
            s => s.IntgrSpotify,
            s => s.IntgrSpotify_VR, s => s.IntgrSpotify_VR = true,
            s => s.IntgrSpotify_DESKTOP, s => s.IntgrSpotify_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrTwitch), "Twitch",
            s => s.IntgrTwitch,
            s => s.IntgrTwitch_VR, s => s.IntgrTwitch_VR = true,
            s => s.IntgrTwitch_DESKTOP, s => s.IntgrTwitch_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrTikTokLive), "TikTok Live",
            s => s.IntgrTikTokLive,
            s => s.IntgrTikTokLive_VR, s => s.IntgrTikTokLive_VR = true,
            s => s.IntgrTikTokLive_DESKTOP, s => s.IntgrTikTokLive_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrDiscord), "Discord",
            s => s.IntgrDiscord,
            s => s.IntgrDiscord_VR, s => s.IntgrDiscord_VR = true,
            s => s.IntgrDiscord_DESKTOP, s => s.IntgrDiscord_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrVrcRadar), "VRChat Radar",
            s => s.IntgrVrcRadar,
            s => s.IntgrVrcRadar_VR, s => s.IntgrVrcRadar_VR = true,
            s => s.IntgrVrcRadar_DESKTOP, s => s.IntgrVrcRadar_DESKTOP = true),

        new(nameof(IntegrationSettings.IntgrTrackerBattery), "Tracker Battery (VR only)",
            s => s.IntgrTrackerBattery,
            _ => true, null,
            _ => false, null),

        new(nameof(IntegrationSettings.IntgrVrPerformance), "VR Performance (VR only)",
            s => s.IntgrVrPerformance,
            _ => true, null,
            _ => false, null),
    };

    private static readonly Dictionary<string, IntegrationModeGate> GatesByMaster =
        Gates.ToDictionary(gate => gate.MasterPropertyName, StringComparer.Ordinal);

    public static bool TryGetGate(string masterPropertyName, out IntegrationModeGate gate)
        => GatesByMaster.TryGetValue(masterPropertyName ?? string.Empty, out gate!);

    public static IReadOnlyList<HiddenIntegration> GetHiddenInCurrentMode(IntegrationSettings settings, bool isVR)
    {
        if (settings == null)
            return Array.Empty<HiddenIntegration>();

        return Gates
            .Where(gate => gate.IsMasterEnabled(settings) && !gate.IsVisibleIn(settings, isVR))
            .Select(gate => new HiddenIntegration(gate.DisplayName, gate.CanEnableIn(isVR)))
            .ToList();
    }

    public static bool TryDescribeHiddenMode(
        IntegrationSettings settings,
        string masterPropertyName,
        bool isVR,
        out HiddenIntegration hidden)
    {
        hidden = default;

        if (settings == null || !TryGetGate(masterPropertyName, out var gate))
            return false;

        if (!gate.IsMasterEnabled(settings) || gate.IsVisibleIn(settings, isVR))
            return false;

        hidden = new HiddenIntegration(gate.DisplayName, gate.CanEnableIn(isVR));
        return true;
    }

    public static bool TryEnableCurrentMode(
        IntegrationSettings settings,
        string masterPropertyName,
        bool isVR,
        out string displayName)
    {
        displayName = string.Empty;

        if (settings == null || !TryGetGate(masterPropertyName, out var gate))
            return false;

        if (!gate.IsMasterEnabled(settings))
            return false;

        if (gate.IsVisibleIn(settings, isVR) || !gate.CanEnableIn(isVR))
            return false;

        gate.EnableIn(settings, isVR);
        displayName = gate.DisplayName;
        return gate.IsVisibleIn(settings, isVR);
    }

    public static string? BuildWarning(IntegrationSettings settings, bool isVR)
    {
        var hidden = GetHiddenInCurrentMode(settings, isVR);
        if (hidden.Count == 0)
            return null;

        string mode = isVR ? "VR" : "Desktop";
        string names = string.Join(", ", hidden.Select(h => h.DisplayName));
        return hidden.All(h => !h.CanEnableInCurrentMode)
            ? $"Enabled but not shown in {mode} mode: {names}."
            : $"Enabled but not shown in {mode} mode: {names}. Turn on their {mode} switch to see them.";
    }
}
