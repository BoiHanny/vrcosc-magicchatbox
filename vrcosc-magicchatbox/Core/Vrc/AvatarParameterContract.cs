using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vrcosc_magicchatbox.Core.Vrc;

public static class AvatarParameterContract
{
    public const int Version = 1;

    public static readonly IReadOnlyList<AvatarParameter> Parameters = new AvatarParameter[]
    {
        new("isHRConnected", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "IAppState.PulsoidAuthConnected", "IntgrHeartRate_OSC"),
        new("isHRActive", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "PulsoidModule.PulsoidDeviceOnline", "IntgrHeartRate_OSC"),
        new("isHRBeat", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "Pulsoid beat event", "IntgrHeartRate_OSC"),
        new("HR", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-255", "PulsoidModule.GetOSCHeartRate()", "IntgrHeartRate_OSC"),
        new("HRPercent", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "IntgrHeartRate_OSC"),
        new("FullHRPercent", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "-1.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "IntgrHeartRate_OSC"),

        new("onesHR", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "GetOSCHeartRate() digit", "IntgrHeartRate_OSC and not DisableLegacySupport"),
        new("tensHR", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "GetOSCHeartRate() digit", "IntgrHeartRate_OSC and not DisableLegacySupport"),
        new("hundredsHR", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "GetOSCHeartRate() digit", "IntgrHeartRate_OSC and not DisableLegacySupport"),

        new("MCB_Heartrate_Hot", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "heart rate at or above HighTemperatureThreshold", "SentMCBHeartrateInfo"),
        new("MCB_Heartrate_Sleepy", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "heart rate below LowTemperatureThreshold", "SentMCBHeartrateInfo"),
        new("MCB_Heartrate_TrendUp", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "PulsoidModuleSettings.HeartRateTrendIndicator", "SentMCBHeartrateInfo"),
        new("MCB_Heartrate_TrendDown", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "PulsoidModuleSettings.HeartRateTrendIndicator", "SentMCBHeartrateInfo"),

        new("MCB_Heartrate_Min", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-255", "PulsoidStatistics.minimum_beats_per_minute", "SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Max", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-255", "PulsoidStatistics.maximum_beats_per_minute", "SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Avg", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-255", "PulsoidStatistics.average_beats_per_minute", "SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy"),

        new("MCB_Heartrate_Min_Ones", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.minimum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Min_Tens", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.minimum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Min_Hundreds", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.minimum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Max_Ones", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.maximum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Max_Tens", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.maximum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Max_Hundreds", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.maximum_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Avg_Ones", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.average_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Avg_Tens", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.average_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),
        new("MCB_Heartrate_Avg_Hundreds", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0-9", "PulsoidStatistics.average_beats_per_minute digit", "SentMCBHeartrateInfoLegacy"),

        new("DiscordMuted", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "DiscordModule.SelfMutedState", "DiscordSettings.SendMuteDeafenOsc"),
        new("DiscordDeafened", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "DiscordModule.SelfDeafenedState", "DiscordSettings.SendMuteDeafenOsc"),
        new("DiscordInVC", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "DiscordModule.InVoiceChannelState", "DiscordSettings.SendVoiceStateOsc"),
        new("DiscordVCCount", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "raw count", "DiscordModule.VoiceMemberCount", "DiscordSettings.SendVoiceStateOsc",
            "Sent as a raw float count rather than a normalised value. Kept as-is so existing avatars keep working."),
        new("DiscordSpeaking", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1", "DiscordModule.AnyoneSpeakingState", "DiscordSettings.SendVoiceStateOsc"),

        new("CameraFlash", AvatarParameterKind.Pulse, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Legacy,
            "0 or 1, 150 ms pulse", "VrcLogModule screenshot detection", "VrcLogSettings.SendCameraFlashOsc",
            "The name is user-editable through VrcLogSettings.OscCameraFlashParam; CameraFlash is the default."),

        new("VRCOSC/Heartrate/Connected", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0 or 1", "IAppState.PulsoidAuthConnected", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Enabled", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0 or 1", "IAppState.PulsoidAuthConnected", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Value", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0-255", "GetOSCHeartRate()", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Normalised", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Beat", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0 or 1", "Pulsoid beat event", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Average", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0-255", "PulsoidStatistics.average_beats_per_minute", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Units", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-0.9", "GetOSCHeartRate() digit divided by 10", "BroadPrefabCompatibility",
            "VRCOSC sends its digit parameters as floats at digit/10, not as ints."),
        new("VRCOSC/Heartrate/Tens", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-0.9", "GetOSCHeartRate() digit divided by 10", "BroadPrefabCompatibility"),
        new("VRCOSC/Heartrate/Hundreds", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-0.9", "GetOSCHeartRate() digit divided by 10", "BroadPrefabCompatibility"),

        new("HeartRateInt", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0-255", "GetOSCHeartRate()", "BroadPrefabCompatibility"),
        new("HeartRate3", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0-255", "GetOSCHeartRate()", "BroadPrefabCompatibility"),
        new("Heartrate3", AvatarParameterKind.Int, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0-255", "GetOSCHeartRate()", "BroadPrefabCompatibility",
            "Deliberately duplicates HeartRate3 with a lowercase r. VRChat parameter names are case sensitive and both spellings ship in the wild."),
        new("HeartRateFloat", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "-1.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("HeartRate", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "-1.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("HeartRateFloat01", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("HeartRate2", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("HeartBeatToggle", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0 or 1", "flips on every beat", "BroadPrefabCompatibility",
            "A toggle rather than a pulse: it holds its value until the next beat."),

        new("hr_percent", AvatarParameterKind.Float, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0.0-1.0", "GetOSCHeartRate() scaled by OscHrMin/OscHrMax", "BroadPrefabCompatibility"),
        new("hr_connected", AvatarParameterKind.Bool, AvatarParameterFlow.AppToAvatar, AvatarParameterTier.Compatibility,
            "0 or 1", "IAppState.PulsoidAuthConnected", "BroadPrefabCompatibility"),

        new("MCB/Ctrl/Tts/Stop", AvatarParameterKind.Bool, AvatarParameterFlow.AvatarToApp, AvatarParameterTier.Control,
            "false to true", "stops text-to-speech playback", "EnableBridge and EnableParameterInput",
            "Acts on the rising edge only, so holding it down does nothing further. Costs no synced parameter bits."),
        new("MCB/Ctrl/Panic", AvatarParameterKind.Bool, AvatarParameterFlow.AvatarToApp, AvatarParameterTier.Control,
            "false to true", "stops all output and text-to-speech", "EnableBridge and EnableParameterInput",
            "Deliberately one-way: it cannot be undone from the avatar, so a misbehaving world cannot switch MagicChatbox back on."),
    };

    private static readonly HashSet<string> KnownAddresses =
        new(Parameters.Select(p => p.Address), StringComparer.Ordinal);

    public static bool IsKnownAddress(string address) => KnownAddresses.Contains(address);

    public static IEnumerable<AvatarParameter> InTier(AvatarParameterTier tier)
        => Parameters.Where(p => p.Tier == tier);

    public static string ToClipboardText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"MagicChatbox avatar parameters (contract v{Version})");
        builder.AppendLine();

        foreach (var group in Parameters.GroupBy(p => p.Tier))
        {
            builder.AppendLine(DescribeTier(group.Key));

            foreach (var parameter in group)
                builder.AppendLine($"  {parameter.Name}\t{parameter.Kind}\t{parameter.Range}");

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# MagicChatbox avatar parameters — contract v{Version}");
        builder.AppendLine();
        builder.AppendLine("Every value below is written to `/avatar/parameters/<name>`. Names are case sensitive.");
        builder.AppendLine();

        foreach (var group in Parameters.GroupBy(p => p.Tier))
        {
            builder.AppendLine($"## {DescribeTier(group.Key)}");
            builder.AppendLine();
            builder.AppendLine("| Parameter | Type | Range | Source | Sent when |");
            builder.AppendLine("|---|---|---|---|---|");

            foreach (var parameter in group)
            {
                builder.AppendLine(
                    $"| `{parameter.Name}` | {parameter.Kind} | {parameter.Range} | {parameter.Source} | {parameter.Gate} |");
            }

            builder.AppendLine();

            var noted = group.Where(p => !string.IsNullOrEmpty(p.Notes)).ToList();
            if (noted.Count > 0)
            {
                foreach (var parameter in noted)
                    builder.AppendLine($"- `{parameter.Name}` — {parameter.Notes}");

                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string DescribeTier(AvatarParameterTier tier) => tier switch
    {
        AvatarParameterTier.Legacy => "Shipping parameters",
        AvatarParameterTier.Compatibility => "Compatibility names used by other heart rate apps",
        AvatarParameterTier.Synced => "Synced parameters",
        AvatarParameterTier.Local => "Local parameters",
        AvatarParameterTier.Control => "Control parameters",
        _ => tier.ToString(),
    };
}
