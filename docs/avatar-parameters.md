# MagicChatbox avatar parameters — contract v1

Every value below is written to `/avatar/parameters/<name>`. Names are case sensitive.

## Shipping parameters

| Parameter | Type | Range | Source | Sent when |
|---|---|---|---|---|
| `isHRConnected` | Bool | 0 or 1 | IAppState.PulsoidAuthConnected | IntgrHeartRate_OSC |
| `isHRActive` | Bool | 0 or 1 | PulsoidModule.PulsoidDeviceOnline | IntgrHeartRate_OSC |
| `isHRBeat` | Bool | 0 or 1 | Pulsoid beat event | IntgrHeartRate_OSC |
| `HR` | Int | 0-255 | PulsoidModule.GetOSCHeartRate() | IntgrHeartRate_OSC |
| `HRPercent` | Float | 0.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | IntgrHeartRate_OSC |
| `FullHRPercent` | Float | -1.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | IntgrHeartRate_OSC |
| `onesHR` | Int | 0-9 | GetOSCHeartRate() digit | IntgrHeartRate_OSC and not DisableLegacySupport |
| `tensHR` | Int | 0-9 | GetOSCHeartRate() digit | IntgrHeartRate_OSC and not DisableLegacySupport |
| `hundredsHR` | Int | 0-9 | GetOSCHeartRate() digit | IntgrHeartRate_OSC and not DisableLegacySupport |
| `MCB_Heartrate_Hot` | Bool | 0 or 1 | heart rate at or above HighTemperatureThreshold | SentMCBHeartrateInfo |
| `MCB_Heartrate_Sleepy` | Bool | 0 or 1 | heart rate below LowTemperatureThreshold | SentMCBHeartrateInfo |
| `MCB_Heartrate_TrendUp` | Bool | 0 or 1 | PulsoidModuleSettings.HeartRateTrendIndicator | SentMCBHeartrateInfo |
| `MCB_Heartrate_TrendDown` | Bool | 0 or 1 | PulsoidModuleSettings.HeartRateTrendIndicator | SentMCBHeartrateInfo |
| `MCB_Heartrate_Min` | Int | 0-255 | PulsoidStatistics.minimum_beats_per_minute | SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Max` | Int | 0-255 | PulsoidStatistics.maximum_beats_per_minute | SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Avg` | Int | 0-255 | PulsoidStatistics.average_beats_per_minute | SentMCBHeartrateInfo and not SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Min_Ones` | Int | 0-9 | PulsoidStatistics.minimum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Min_Tens` | Int | 0-9 | PulsoidStatistics.minimum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Min_Hundreds` | Int | 0-9 | PulsoidStatistics.minimum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Max_Ones` | Int | 0-9 | PulsoidStatistics.maximum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Max_Tens` | Int | 0-9 | PulsoidStatistics.maximum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Max_Hundreds` | Int | 0-9 | PulsoidStatistics.maximum_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Avg_Ones` | Int | 0-9 | PulsoidStatistics.average_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Avg_Tens` | Int | 0-9 | PulsoidStatistics.average_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `MCB_Heartrate_Avg_Hundreds` | Int | 0-9 | PulsoidStatistics.average_beats_per_minute digit | SentMCBHeartrateInfoLegacy |
| `DiscordMuted` | Bool | 0 or 1 | DiscordModule.SelfMutedState | DiscordSettings.SendMuteDeafenOsc |
| `DiscordDeafened` | Bool | 0 or 1 | DiscordModule.SelfDeafenedState | DiscordSettings.SendMuteDeafenOsc |
| `DiscordInVC` | Bool | 0 or 1 | DiscordModule.InVoiceChannelState | DiscordSettings.SendVoiceStateOsc |
| `DiscordVCCount` | Float | raw count | DiscordModule.VoiceMemberCount | DiscordSettings.SendVoiceStateOsc |
| `DiscordSpeaking` | Bool | 0 or 1 | DiscordModule.AnyoneSpeakingState | DiscordSettings.SendVoiceStateOsc |
| `CameraFlash` | Pulse | 0 or 1, 150 ms pulse | VrcLogModule screenshot detection | VrcLogSettings.SendCameraFlashOsc |

- `DiscordVCCount` — Sent as a raw float count rather than a normalised value. Kept as-is so existing avatars keep working.
- `CameraFlash` — The name is user-editable through VrcLogSettings.OscCameraFlashParam; CameraFlash is the default.

## Compatibility names used by other heart rate apps

| Parameter | Type | Range | Source | Sent when |
|---|---|---|---|---|
| `VRCOSC/Heartrate/Connected` | Bool | 0 or 1 | IAppState.PulsoidAuthConnected | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Enabled` | Bool | 0 or 1 | IAppState.PulsoidAuthConnected | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Value` | Int | 0-255 | GetOSCHeartRate() | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Normalised` | Float | 0.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Beat` | Bool | 0 or 1 | Pulsoid beat event | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Average` | Int | 0-255 | PulsoidStatistics.average_beats_per_minute | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Units` | Float | 0.0-0.9 | GetOSCHeartRate() digit divided by 10 | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Tens` | Float | 0.0-0.9 | GetOSCHeartRate() digit divided by 10 | BroadPrefabCompatibility |
| `VRCOSC/Heartrate/Hundreds` | Float | 0.0-0.9 | GetOSCHeartRate() digit divided by 10 | BroadPrefabCompatibility |
| `HeartRateInt` | Int | 0-255 | GetOSCHeartRate() | BroadPrefabCompatibility |
| `HeartRate3` | Int | 0-255 | GetOSCHeartRate() | BroadPrefabCompatibility |
| `Heartrate3` | Int | 0-255 | GetOSCHeartRate() | BroadPrefabCompatibility |
| `HeartRateFloat` | Float | -1.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `HeartRate` | Float | -1.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `HeartRateFloat01` | Float | 0.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `HeartRate2` | Float | 0.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `HeartBeatToggle` | Bool | 0 or 1 | flips on every beat | BroadPrefabCompatibility |
| `hr_percent` | Float | 0.0-1.0 | GetOSCHeartRate() scaled by OscHrMin/OscHrMax | BroadPrefabCompatibility |
| `hr_connected` | Bool | 0 or 1 | IAppState.PulsoidAuthConnected | BroadPrefabCompatibility |

- `VRCOSC/Heartrate/Units` — VRCOSC sends its digit parameters as floats at digit/10, not as ints.
- `Heartrate3` — Deliberately duplicates HeartRate3 with a lowercase r. VRChat parameter names are case sensitive and both spellings ship in the wild.
- `HeartBeatToggle` — A toggle rather than a pulse: it holds its value until the next beat.

## Control parameters

| Parameter | Type | Range | Source | Sent when |
|---|---|---|---|---|
| `MCB/Ctrl/Tts/Stop` | Bool | false to true | stops text-to-speech playback | EnableBridge and EnableParameterInput |
| `MCB/Ctrl/Panic` | Bool | false to true | stops all output and text-to-speech | EnableBridge and EnableParameterInput |

- `MCB/Ctrl/Tts/Stop` — Acts on the rising edge only, so holding it down does nothing further. Costs no synced parameter bits.
- `MCB/Ctrl/Panic` — Deliberately one-way: it cannot be undone from the avatar, so a misbehaving world cannot switch MagicChatbox back on.

