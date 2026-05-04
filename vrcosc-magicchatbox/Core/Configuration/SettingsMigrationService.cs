using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Xml;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// One-time migration helpers that run on app startup.
/// Type 1: Copies property values from the legacy settings.xml into per-module JSON files.
///         Uses field-level merge with _migratedAt tracking to be safe for both fresh
///         installs and users already on the Refactor branch.
/// Type 2: Renames data files that use .xml extension but actually contain JSON.
/// </summary>
public static class SettingsMigrationService
{
    /// <summary>
    /// Run all migrations. Call once on app startup, BEFORE any settings provider
    /// is resolved from DI (i.e., before JsonSettingsProvider constructors run).
    /// </summary>
    public static void RunAll(string dataPath)
    {
        try
        {
            MigrateXmlSettingsToJson(dataPath);
            RenameJsonXmlFiles(dataPath);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Settings migration error (non-fatal): {ex.Message}");
        }
    }

    private static void MigrateXmlSettingsToJson(string dataPath)
    {
        string xmlPath = Path.Combine(dataPath, "settings.xml");
        if (!File.Exists(xmlPath))
            return;

        XmlDocument xmlDoc = new();
        try
        {
            xmlDoc.Load(xmlPath);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Cannot parse settings.xml for migration: {ex.Message}");
            return;
        }

        XmlNode root = xmlDoc.SelectSingleNode("Settings");
        if (root == null)
            return;

        MigrateModule<OscSettings>(root, OscMigrationMap(), dataPath);
        MigrateModule<ChatSettings>(root, ChatMigrationMap(), dataPath);
        MigrateModule<OpenAISettings>(root, OpenAIMigrationMap(), dataPath);
        MigrateModule<WindowActivitySettings>(root, WindowActivityMigrationMap(), dataPath);
        MigrateModule<ComponentStatsSettings>(root, ComponentStatsMigrationMap(), dataPath);
        MigrateModule<TimeSettings>(root, TimeMigrationMap(), dataPath);
        MigrateModule<TtsSettings>(root, TtsMigrationMap(), dataPath);
        MigrateModule<AppSettings>(root, AppMigrationMap(), dataPath);
        MigrateModule<IntegrationSettings>(root, IntegrationMigrationMap(), dataPath);
        MigrateModule<WeatherSettings>(root, WeatherMigrationMap(), dataPath);
        MigrateModule<TwitchSettings>(root, TwitchMigrationMap(), dataPath);
        MigrateModule<DiscordSettings>(root, DiscordMigrationMap(), dataPath);
        MigrateModule<MediaLinkSettings>(root, MediaLinkMigrationMap(), dataPath);
        MigrateModule<NetworkStatsSettings>(root, NetworkStatsMigrationMap(), dataPath);
        MigrateModule<TrackerBatterySettings>(root, TrackerBatteryMigrationMap(), dataPath);

        // Back up settings.xml so the old version can still boot from it
        BackupXml(xmlPath);

        Logging.WriteInfo("XML→JSON settings migration pass complete.");
    }

    /// <summary>
    /// Field-level merge strategy:
    ///   • If the JSON file already has _migratedAt set → migration already done, skip.
    ///   • Otherwise (file doesn't exist OR exists without _migratedAt):
    ///     load/create a T, then for each map entry only apply the XML value if the
    ///     current JSON value is still the type default (preserves intentional user edits).
    ///   • Sets _migratedAt = UtcNow and writes to disk if any values were merged.
    /// </summary>
    private static void MigrateModule<T>(
        XmlNode root, List<MigrationEntry> map, string dataPath) where T : class, new()
    {
        string jsonPath = Path.Combine(dataPath, $"{typeof(T).Name}.json");

        T settings;
        if (File.Exists(jsonPath))
        {
            try
            {
                var raw = File.ReadAllText(jsonPath);
                settings = JsonConvert.DeserializeObject<T>(raw) ?? new T();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Cannot read {typeof(T).Name}.json for merge-check: {ex.Message}");
                return;
            }

            if (settings is VersionedSettings vs && vs.MigratedAt.HasValue)
                return;
        }
        else
        {
            settings = new T();
        }

        T defaults = new();
        bool anyMigrated = false;

        foreach (var entry in map)
        {
            string xmlValue = GetXmlValue(root, entry.XmlCategory, entry.XmlKey);
            if (xmlValue == null) continue;

            try
            {
                var prop = typeof(T).GetProperty(entry.JsonPropertyName);
                if (prop == null || !prop.CanRead || !prop.CanWrite) continue;

                object current = prop.GetValue(settings);
                object defaultVal = prop.GetValue(defaults);
                if (!IsDefaultValue(current, defaultVal)) continue;

                object converted = ConvertValue(xmlValue, prop.PropertyType);
                if (converted == null) continue;

                prop.SetValue(settings, converted);
                anyMigrated = true;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo(
                    $"Migration skip {typeof(T).Name}.{entry.JsonPropertyName}: {ex.Message}");
            }
        }

        // Stamp the migration timestamp only when actual values were merged
        if (anyMigrated && settings is VersionedSettings versioned)
            versioned.MigratedAt = DateTime.UtcNow;

        if (anyMigrated || !File.Exists(jsonPath))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
                string json = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(jsonPath, json);
                Logging.WriteInfo($"Migrated {typeof(T).Name} ({map.Count} entries mapped)");
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Failed to write {typeof(T).Name}.json: {ex.Message}");
            }
        }
    }

    private static string GetXmlValue(XmlNode root, string category, string key)
    {
        XmlNode catNode = root.SelectSingleNode(category);
        XmlNode valNode = catNode?.SelectSingleNode(key);
        return valNode?.InnerText;
    }

    /// <summary>
    /// Returns true when the value is the type's default (unmodified by user).
    /// Handles collections (empty = default), nullable types, and value types.
    /// </summary>
    private static bool IsDefaultValue(object current, object defaultVal)
    {
        if (current == null && defaultVal == null) return true;
        if (current == null || defaultVal == null) return false;
        if (current is System.Collections.ICollection col) return col.Count == 0;
        return Equals(current, defaultVal);
    }

    private static object ConvertValue(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (targetType == typeof(bool)) return bool.TryParse(value, out var b) ? b : (object)null;
        if (targetType == typeof(int)) return int.TryParse(value, out var i) ? i : (object)null;
        if (targetType == typeof(long)) return long.TryParse(value, out var l) ? l : (object)null;
        if (targetType == typeof(uint)) return uint.TryParse(value, out var u) ? u : (object)null;
        if (targetType == typeof(byte)) return byte.TryParse(value, out var by) ? by : (object)null;
        if (targetType == typeof(double))
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dInv))
                return dInv;
            return double.TryParse(value, out var dSys) ? dSys : (object)null;
        }
        if (targetType == typeof(float))
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fInv))
                return fInv;
            return float.TryParse(value, out var fSys) ? fSys : (object)null;
        }
        if (targetType == typeof(DateTime))
            return DateTime.TryParse(value, out var dt) ? dt : (object)null;
        if (targetType.IsEnum)
            return Enum.TryParse(targetType, value, ignoreCase: true, out var e) ? e : null;

        // ObservableCollection<TrackerDevice> — value is JSON-serialized in old DataController
        if (targetType == typeof(ObservableCollection<TrackerDevice>))
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<TrackerDevice>>(value);
                if (list == null || list.Count == 0) return null;
                return new ObservableCollection<TrackerDevice>(list);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"TrackerDevices deserialization failed: {ex.Message}");
                return null;
            }
        }

        return null;
    }

    private static void BackupXml(string xmlPath)
    {
        try
        {
            string bakPath = xmlPath + ".bak";
            if (!File.Exists(bakPath))
            {
                File.Copy(xmlPath, bakPath);
                Logging.WriteInfo("Backed up settings.xml → settings.xml.bak");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Could not back up settings.xml: {ex.Message}");
        }
    }

    private record MigrationEntry(string XmlCategory, string XmlKey, string JsonPropertyName);

    private static List<MigrationEntry> OscMigrationMap() =>
    [
        new("OSC", "OSCIP", "OscIP"),
        new("OSC", "OSCPortOut", "OscPortOut"),
        new("OSC", "SecOSC", "SecOSC"),
        new("OSC", "SecOSCPort", "SecOSCPort"),
        new("OSC", "ThirdOSC", "ThirdOSC"),
        new("OSC", "ThirdOSCPort", "ThirdOSCPort"),
        new("OSC", "UnmuteMainOutput", "UnmuteMainOutput"),
        new("OSC", "UnmuteSecOutput", "UnmuteSecOutput"),
        new("OSC", "UnmuteThirdOutput", "UnmuteThirdOutput"),
    ];

    private static List<MigrationEntry> ChatMigrationMap() =>
    [
        new("Chat", "PrefixChat", "PrefixChat"),
        new("Chat", "ChatFX", "ChatFX"),
        new("Chat", "ChatLiveEdit", "ChatLiveEdit"),
        new("Chat", "KeepUpdatingChat", "KeepUpdatingChat"),
        new("Chat", "ChatSendAgainFX", "ChatSendAgainFX"),
        new("Chat", "ChatAddSmallDelay", "ChatAddSmallDelay"),
        new("Chat", "ChatAddSmallDelayTIME", "ChatAddSmallDelayTIME"),
        new("Chat", "ChattingUpdateRate", "ChattingUpdateRate"),
        new("Chat", "RealTimeChatEdit", "RealTimeChatEdit"),
        new("Chat", "HideOpenAITools", "HideOpenAITools"),
    ];

    private static List<MigrationEntry> OpenAIMigrationMap() =>
    [
        new("OpenAI", "OpenAIAccessTokenEncrypted", "AccessTokenEncrypted"),
        new("OpenAI", "OpenAIOrganizationIDEncrypted", "OrganizationIDEncrypted"),
    ];

    private static List<MigrationEntry> WindowActivityMigrationMap() =>
    [
        new("WindowActivity", "WindowActivityShowFocusedApp",        "ShowFocusedApp"),
        new("WindowActivity", "WindowActivityDesktopFocusTitle",      "DesktopFocusTitle"),
        new("WindowActivity", "WindowActivityDesktopTitle",           "DesktopTitle"),
        new("WindowActivity", "WindowActivityVRFocusTitle",           "VrFocusTitle"),
        new("WindowActivity", "WindowActivityVRTitle",                "VrTitle"),
        new("WindowActivity", "AutoShowTitleOnNewApp",                "AutoShowTitleOnNewApp"),
        new("WindowActivity", "WindowActivityTitleScan",              "TitleScan"),
        new("WindowActivity", "MaxShowTitleCount",                    "MaxShowTitleCount"),
        new("WindowActivity", "LimitTitleOnApp",                      "LimitTitleOnApp"),
        new("WindowActivity", "TitleOnAppVR",                         "TitleOnAppVR"),
        new("WindowActivity", "WindowActivityPrivateName",            "PrivateName"),
        new("Integrations",   "ApplicationHookV2",                   "ApplicationHookV2"),
    ];

    private static List<MigrationEntry> ComponentStatsMigrationMap() =>
    [
        new("ComponentStats", "SelectedGPU",                  "SelectedGPU"),
        new("ComponentStats", "AutoSelectGPU",                "AutoSelectGPU"),
        new("ComponentStats", "UseEmojisForTempAndPower",     "UseEmojisForTempAndPower"),
        new("ComponentStats", "IsTemperatureSwitchEnabled",   "IsTemperatureSwitchEnabled"),
    ];

    private static List<MigrationEntry> TimeMigrationMap() =>
    [
        new("Time", "Time24H",                 "Time24H"),
        new("Time", "PrefixTime",              "PrefixTime"),
        new("Time", "TimeShowTimeZone",        "TimeShowTimeZone"),
        new("Time", "SelectedTimeZone",        "SelectedTimeZone"),
        new("Time", "UseDaylightSavingTime",   "UseDaylightSavingTime"),
        new("Time", "AutoSetDaylight",         "AutoSetDaylight"),
        new("Time", "UseSystemCulture",        "UseSystemCulture"),
        new("Time", "BussyBoysDate",           "BussyBoysDate"),
        new("Time", "BussyBoysDateEnable",     "BussyBoysDateEnable"),
        new("Time", "BussyBoysMultiMODE",      "BussyBoysMultiMODE"),
    ];

    private static List<MigrationEntry> TtsMigrationMap() =>
    [
        new("TTS", "TTSTikTokEnabled",      "TtsTikTokEnabled"),
        new("TTS", "TTSCutOff",             "TtsCutOff"),
        new("TTS", "AutoUnmuteTTS",         "AutoUnmuteTTS"),
        new("TTS", "ToggleVoiceWithV",      "ToggleVoiceWithV"),
        new("TTS", "TTSVolume",             "TtsVolume"),
        new("TTS", "RecentTikTokTTSVoice",  "RecentTikTokTTSVoice"),
        new("TTS", "RecentPlayBackOutput",  "RecentPlayBackOutput"),
        new("TTS", "TTSOnResendChat",       "TtsOnResendChat"),
    ];

    private static List<MigrationEntry> AppMigrationMap() =>
    [
        new("Scanning",      "ScanningInterval",               "ScanningInterval"),
        new("Scanning",      "ScanPauseTimeout",               "ScanPauseTimeout"),
        new("Icons",         "PrefixIconStatus",               "PrefixIconStatus"),
        new("Icons",         "PrefixIconMusic",                "PrefixIconMusic"),
        new("Icons",         "PrefixIconSoundpad",             "PrefixIconSoundpad"),
        new("Icons",         "EnableEmojiShuffleInChats",      "EnableEmojiShuffleInChats"),
        new("Icons",         "EnableEmojiShuffle",             "EnableEmojiShuffle"),
        new("Custom",        "SeperateWithENTERS",             "SeperateWithENTERS"),
        new("Custom",        "OscMessagePrefix",               "OscMessagePrefix"),
        new("Custom",        "OscMessageSeparator",            "OscMessageSeparator"),
        new("Custom",        "OscMessageSuffix",               "OscMessageSuffix"),
        new("System",        "CountOculusSystemAsVR",          "CountOculusSystemAsVR"),
        new("Window",        "Topmost",                        "Topmost"),
        new("Update",        "JoinedAlphaChannel",             "JoinedAlphaChannel"),
        new("Update",        "CheckUpdateOnStartup",           "CheckUpdateOnStartup"),
        new("DEV",           "BlankEgg",                       "BlankEgg"),
        new("StatusSetting", "SwitchStatusInterval",           "SwitchStatusInterval"),
        new("StatusSetting", "EggPrefixIconStatus",            "EggPrefixIconStatus"),
        new("StatusSetting", "IsRandomCycling",                "IsRandomCycling"),
        new("StatusSetting", "CycleStatus",                    "CycleStatus"),
        new("Menu",          "CurrentMenuItem",                "CurrentMenuItem"),
        new("OptionsTabState", "Settings_Status",              "Settings_Status"),
        new("OptionsTabState", "Settings_OpenAI",              "Settings_OpenAI"),
        new("OptionsTabState", "Settings_HeartRate",           "Settings_HeartRate"),
        new("OptionsTabState", "Settings_Time",                "Settings_Time"),
        new("OptionsTabState", "Settings_Weather",             "Settings_Weather"),
        new("OptionsTabState", "Settings_Twitch",              "Settings_Twitch"),
        new("OptionsTabState", "Settings_Discord",             "Settings_Discord"),
        new("OptionsTabState", "Settings_Spotify",             "Settings_Spotify"),
        new("OptionsTabState", "Settings_ComponentStats",      "Settings_ComponentStats"),
        new("OptionsTabState", "Settings_NetworkStatistics",   "Settings_NetworkStatistics"),
        new("OptionsTabState", "Settings_Chatting",            "Settings_Chatting"),
        new("OptionsTabState", "Settings_TTS",                 "Settings_TTS"),
        new("OptionsTabState", "Settings_MediaLink",           "Settings_MediaLink"),
        new("OptionsTabState", "Settings_AppOptions",          "Settings_AppOptions"),
        new("OptionsTabState", "Settings_WindowActivity",      "Settings_WindowActivity"),
        new("OptionsTabState", "Settings_VrcRadar",            "Settings_VrcRadar"),
        new("OptionsTabState", "Settings_TrackerBattery",      "Settings_TrackerBattery"),
    ];

    private static List<MigrationEntry> IntegrationMigrationMap() =>
    [
        new("Integrations", "IntgrStatus",                "IntgrStatus"),
        new("Integrations", "IntgrScanWindowActivity",    "IntgrScanWindowActivity"),
        new("Integrations", "IntgrScanSpotify_OLD",       "IntgrScanSpotify_OLD"),
        new("Integrations", "IntgrScanWindowTime",        "IntgrScanWindowTime"),
        new("Integrations", "ApplicationHookV2",          "ApplicationHookV2"),
        new("Integrations", "IntgrHeartRate",             "IntgrHeartRate"),
        new("Integrations", "IntgrNetworkStatistics",     "IntgrNetworkStatistics"),
        new("Integrations", "IntgrScanMediaLink",         "IntgrScanMediaLink"),
        new("Integrations", "IntgrComponentStats",        "IntgrComponentStats"),
        new("Integrations", "IntgrSoundpad",              "IntgrSoundpad"),
        new("Integrations", "IntgrTwitch",                "IntgrTwitch"),
        new("Integrations", "IntgrDiscord",               "IntgrDiscord"),
        new("Integrations", "IntgrSpotify",               "IntgrSpotify"),
        new("Integrations", "IntgrVrcRadar",              "IntgrVrcRadar"),
        new("Integrations", "IntgrTrackerBattery",        "IntgrTrackerBattery"),

        new("IntegrationToggles", "IntgrComponentStats_VR",        "IntgrComponentStats_VR"),
        new("IntegrationToggles", "IntgrComponentStats_DESKTOP",   "IntgrComponentStats_DESKTOP"),
        new("IntegrationToggles", "IntgrNetworkStatistics_VR",     "IntgrNetworkStatistics_VR"),
        new("IntegrationToggles", "IntgrNetworkStatistics_DESKTOP","IntgrNetworkStatistics_DESKTOP"),
        new("IntegrationToggles", "IntgrStatus_VR",                "IntgrStatus_VR"),
        new("IntegrationToggles", "IntgrStatus_DESKTOP",           "IntgrStatus_DESKTOP"),
        new("IntegrationToggles", "IntgrMediaLink_VR",             "IntgrMediaLink_VR"),
        new("IntegrationToggles", "IntgrMediaLink_DESKTOP",        "IntgrMediaLink_DESKTOP"),
        new("IntegrationToggles", "IntgrWindowActivity_VR",        "IntgrWindowActivity_VR"),
        new("IntegrationToggles", "IntgrWindowActivity_DESKTOP",   "IntgrWindowActivity_DESKTOP"),
        new("IntegrationToggles", "IntgrHeartRate_VR",             "IntgrHeartRate_VR"),
        new("IntegrationToggles", "IntgrHeartRate_DESKTOP",        "IntgrHeartRate_DESKTOP"),
        new("IntegrationToggles", "IntgrHeartRate_OSC",            "IntgrHeartRate_OSC"),
        new("IntegrationToggles", "IntgrCurrentTime_VR",           "IntgrCurrentTime_VR"),
        new("IntegrationToggles", "IntgrCurrentTime_DESKTOP",      "IntgrCurrentTime_DESKTOP"),
        new("IntegrationToggles", "IntgrWeather_VR",               "IntgrWeather_VR"),
        new("IntegrationToggles", "IntgrWeather_DESKTOP",          "IntgrWeather_DESKTOP"),
        new("IntegrationToggles", "IntgrSpotifyStatus_VR",         "IntgrSpotifyStatus_VR"),
        new("IntegrationToggles", "IntgrSpotifyStatus_DESKTOP",    "IntgrSpotifyStatus_DESKTOP"),
        new("IntegrationToggles", "IntgrSoundpad_DESKTOP",         "IntgrSoundpad_DESKTOP"),
        new("IntegrationToggles", "IntgrSoundpad_VR",              "IntgrSoundpad_VR"),
        new("IntegrationToggles", "IntgrTwitch_DESKTOP",           "IntgrTwitch_DESKTOP"),
        new("IntegrationToggles", "IntgrTwitch_VR",                "IntgrTwitch_VR"),
        new("IntegrationToggles", "IntgrDiscord_DESKTOP",          "IntgrDiscord_DESKTOP"),
        new("IntegrationToggles", "IntgrDiscord_VR",               "IntgrDiscord_VR"),
        new("IntegrationToggles", "IntgrSpotify_DESKTOP",          "IntgrSpotify_DESKTOP"),
        new("IntegrationToggles", "IntgrSpotify_VR",               "IntgrSpotify_VR"),
        new("IntegrationToggles", "IntgrVrcRadar_DESKTOP",         "IntgrVrcRadar_DESKTOP"),
        new("IntegrationToggles", "IntgrVrcRadar_VR",              "IntgrVrcRadar_VR"),
    ];

    private static List<MigrationEntry> WeatherMigrationMap() =>
    [
        new("Time", "ShowWeatherInTime",            "ShowWeatherInTime"),
        new("Time", "ShowWeatherCondition",         "ShowWeatherCondition"),
        new("Time", "ShowWeatherEmoji",             "ShowWeatherEmoji"),
        new("Time", "WeatherUseDecimal",            "WeatherUseDecimal"),
        new("Time", "ShowWeatherHumidity",          "ShowWeatherHumidity"),
        new("Time", "ShowWeatherWind",              "ShowWeatherWind"),
        new("Time", "ShowWeatherFeelsLike",         "ShowWeatherFeelsLike"),
        new("Time", "WeatherSeparator",             "WeatherSeparator"),
        new("Time", "WeatherStatsSeparator",        "WeatherStatsSeparator"),
        new("Time", "WeatherTemplate",              "WeatherTemplate"),
        new("Time", "WeatherConditionOverrides",    "WeatherConditionOverrides"),
        new("Time", "WeatherCustomOverridesEnabled","WeatherCustomOverridesEnabled"),
        new("Time", "WeatherLayoutMode",            "WeatherLayoutMode"),
        new("Time", "WeatherOrder",                 "WeatherOrder"),
        new("Time", "WeatherUnitOverride",          "WeatherUnitOverride"),
        new("Time", "WeatherWindUnitOverride",      "WeatherWindUnitOverride"),
        new("Time", "WeatherFallbackMode",          "WeatherFallbackMode"),
        new("Time", "WeatherLocationMode",          "WeatherLocationMode"),
        new("Time", "WeatherAllowIPLocation",       "WeatherAllowIPLocation"),
        new("Time", "WeatherLocationLatitude",      "WeatherLocationLatitude"),
        new("Time", "WeatherLocationLongitude",     "WeatherLocationLongitude"),
        new("Time", "WeatherUpdateIntervalMinutes", "WeatherUpdateIntervalMinutes"),
        new("Time", "WeatherLocationCityEncrypted", "WeatherLocationCityEncrypted"),
    ];

    private static List<MigrationEntry> TwitchMigrationMap() =>
    [
        new("Twitch", "TwitchChannelName",                   "ChannelName"),
        new("Twitch", "TwitchShowViewerCount",               "ShowViewerCount"),
        new("Twitch", "TwitchShowGameName",                  "ShowGameName"),
        new("Twitch", "TwitchShowLiveIndicator",             "ShowLiveIndicator"),
        new("Twitch", "TwitchLivePrefix",                    "LivePrefix"),
        new("Twitch", "TwitchOfflineMessage",                "OfflineMessage"),
        new("Twitch", "TwitchShowStreamTitle",               "ShowStreamTitle"),
        new("Twitch", "TwitchStreamTitlePrefix",             "StreamTitlePrefix"),
        new("Twitch", "TwitchShowChannelName",               "ShowChannelName"),
        new("Twitch", "TwitchChannelPrefix",                 "ChannelPrefix"),
        new("Twitch", "TwitchGamePrefix",                    "GamePrefix"),
        new("Twitch", "TwitchShowViewerLabel",               "ShowViewerLabel"),
        new("Twitch", "TwitchViewerLabel",                   "ViewerLabel"),
        new("Twitch", "TwitchViewerCountCompact",            "ViewerCountCompact"),
        new("Twitch", "TwitchShowFollowerCount",             "ShowFollowerCount"),
        new("Twitch", "TwitchShowFollowerLabel",             "ShowFollowerLabel"),
        new("Twitch", "TwitchFollowerLabel",                 "FollowerLabel"),
        new("Twitch", "TwitchFollowerCountCompact",          "FollowerCountCompact"),
        new("Twitch", "TwitchUseSmallText",                  "UseSmallText"),
        new("Twitch", "TwitchSeparator",                     "Separator"),
        new("Twitch", "TwitchTemplate",                      "Template"),
        new("Twitch", "TwitchUpdateIntervalSeconds",         "UpdateIntervalSeconds"),
        new("Twitch", "TwitchAnnouncementsEnabled",          "AnnouncementsEnabled"),
        new("Twitch", "TwitchAnnouncementMessage",           "AnnouncementMessage"),
        new("Twitch", "TwitchAnnouncementColor",             "AnnouncementColor"),
        new("Twitch", "TwitchShoutoutsEnabled",              "ShoutoutsEnabled"),
        new("Twitch", "TwitchShoutoutTarget",                "ShoutoutTarget"),
        new("Twitch", "TwitchShoutoutAlsoAnnounce",          "ShoutoutAlsoAnnounce"),
        new("Twitch", "TwitchShoutoutAnnouncementTemplate",  "ShoutoutAnnouncementTemplate"),
        new("Twitch", "TwitchShoutoutAnnouncementColor",     "ShoutoutAnnouncementColor"),
        new("Twitch", "TwitchClientIdEncrypted",             "ClientIdEncrypted"),
        new("Twitch", "TwitchAccessTokenEncrypted",          "AccessTokenEncrypted"),
    ];

    private static List<MigrationEntry> DiscordMigrationMap() =>
    [
        new("Discord", "DiscordTemplate",                  "Template"),
        new("Discord", "DiscordEmptySpeakingText",         "EmptySpeakingText"),
        new("Discord", "DiscordNotInVcText",               "NotInVcText"),
        new("Discord", "DiscordMaxSpeakingUsersToShow",    "MaxSpeakingUsersToShow"),
        new("Discord", "DiscordShowMuteDeafenEmoji",       "ShowMuteDeafenEmoji"),
        new("Discord", "DiscordMuteEmoji",                 "MuteEmoji"),
        new("Discord", "DiscordDeafenEmoji",               "DeafenEmoji"),
        new("Discord", "DiscordAutoConnectOnStartup",      "AutoConnectOnStartup"),
        new("Discord", "DiscordHideSelfFromSpeakers",      "HideSelfFromSpeakers"),
        new("Discord", "DiscordShowUserCountOnly",         "ShowUserCountOnly"),
        new("Discord", "DiscordSpeakerDebounceMs",         "SpeakerDebounceMs"),
        new("Discord", "DiscordVoiceClientId",             "VoiceClientId"),
        new("Discord", "DiscordVoiceClientIdEncrypted",    "VoiceClientIdEncrypted"),
        new("Discord", "DiscordAccessTokenEncrypted",      "AccessTokenEncrypted"),
        new("Discord", "DiscordRefreshTokenEncrypted",     "RefreshTokenEncrypted"),
        new("Discord", "DiscordTokenExpiresAtUtcTicks",    "TokenExpiresAtUtcTicks"),
        new("Discord", "DiscordHasRpcScope",               "HasRpcScope"),
        new("Discord", "DiscordSendMuteDeafenOsc",         "SendMuteDeafenOsc"),
        new("Discord", "DiscordSendVoiceStateOsc",         "SendVoiceStateOsc"),
        new("Discord", "DiscordEnableRichPresence",        "EnableRichPresence"),
        new("Discord", "DiscordRichPresenceDetails",       "RichPresenceDetails"),
        new("Discord", "DiscordRichPresenceState",         "RichPresenceState"),
        new("Discord", "DiscordRichPresenceShowJoinButton", "RichPresenceShowJoinButton"),
        new("Discord", "DiscordRichPresenceLargeText",     "RichPresenceLargeText"),
        new("Discord", "DiscordRichPresenceLargeImageKey", "RichPresenceLargeImageKey"),
        new("Discord", "DiscordRichPresenceSmallImageKey", "RichPresenceSmallImageKey"),
        new("Discord", "DiscordRichPresenceSmallText",     "RichPresenceSmallText"),
        new("Discord", "DiscordRichPresenceShowElapsed",   "RichPresenceShowElapsed"),
        new("Discord", "DiscordRichPresenceShowVrDesktopMode", "RichPresenceShowVrDesktopMode"),
        new("Discord", "DiscordRichPresenceJoinButtonLabel", "RichPresenceJoinButtonLabel"),
    ];

    private static List<MigrationEntry> MediaLinkMigrationMap() =>
    [
        new("MediaLink", "MediaLink_ShowOnlyOnChange",   "ShowOnlyOnChange"),
        new("MediaLink", "MediaLink_IconPlay",           "IconPlay"),
        new("MediaLink", "MediaLink_IconPause",          "IconPause"),
        new("MediaLink", "MediaLink_IconStop",           "IconStop"),
        new("MediaLink", "MediaLink_ShowStopIcon",       "ShowStopIcon"),
        new("MediaLink", "MediaLink_Separator",          "Separator"),
        new("MediaLink", "MediaLink_TextPlaying",        "TextPlaying"),
        new("MediaLink", "MediaLink_TextPaused",         "TextPaused"),
        new("MediaLink", "MediaLink_UpperCase",          "UpperCase"),
        new("Icons",     "PauseIconMusic",               "PauseIconMusic"),
        new("MediaLink", "MediaLinkTimeSeekStyle",       "TimeSeekStyle"),
        new("MediaLink", "AutoDowngradeSeekbar",         "AutoDowngradeSeekbar"),
        new("MediaLink", "MediaSession_AutoSwitch",      "AutoSwitch"),
        new("MediaLink", "MediaSession_AutoSwitchSpawn", "AutoSwitchSpawn"),
        new("MediaLink", "MediaSession_Timeout",         "SessionTimeout"),
        new("MediaLink", "DisableMediaLink",             "Disabled"),
        new("MediaLink", "MediaLink_TransientDuration",  "TransientDuration"),
    ];

    private static List<MigrationEntry> NetworkStatsMigrationMap() =>
    [
        new("NetworkStatistics", "NetworkStats_ShowCurrentDown",      "ShowCurrentDown"),
        new("NetworkStatistics", "NetworkStats_ShowCurrentUp",        "ShowCurrentUp"),
        new("NetworkStatistics", "NetworkStats_ShowMaxDown",          "ShowMaxDown"),
        new("NetworkStatistics", "NetworkStats_ShowMaxUp",            "ShowMaxUp"),
        new("NetworkStatistics", "NetworkStats_ShowTotalDown",        "ShowTotalDown"),
        new("NetworkStatistics", "NetworkStats_ShowTotalUp",          "ShowTotalUp"),
        new("NetworkStatistics", "NetworkStats_ShowNetworkUtilization","ShowNetworkUtilization"),
    ];

    private static List<MigrationEntry> TrackerBatteryMigrationMap() =>
    [
        new("TrackerBattery", "TrackerBattery_Template",               "Template"),
        new("TrackerBattery", "TrackerBattery_Prefix",                 "Prefix"),
        new("TrackerBattery", "TrackerBattery_Suffix",                 "Suffix"),
        new("TrackerBattery", "TrackerBattery_Separator",              "Separator"),
        new("TrackerBattery", "TrackerBattery_GlobalEmergency",        "GlobalEmergency"),
        new("TrackerBattery", "TrackerBattery_ShowControllers",        "ShowControllers"),
        new("TrackerBattery", "TrackerBattery_ShowHeadset",            "ShowHeadset"),
        new("TrackerBattery", "TrackerBattery_ShowTrackers",           "ShowTrackers"),
        new("TrackerBattery", "TrackerBattery_ShowDisconnected",       "ShowDisconnected"),
        new("TrackerBattery", "TrackerBattery_OfflineBatteryText",     "OfflineBatteryText"),
        new("TrackerBattery", "TrackerBattery_OnlineText",             "OnlineText"),
        new("TrackerBattery", "TrackerBattery_OfflineText",            "OfflineText"),
        new("TrackerBattery", "TrackerBattery_LowTag",                 "LowTag"),
        new("TrackerBattery", "TrackerBattery_CompactWhitespace",      "CompactWhitespace"),
        new("TrackerBattery", "TrackerBattery_UseSmallText",           "UseSmallText"),
        new("TrackerBattery", "TrackerBattery_SortMode",               "SortMode"),
        new("TrackerBattery", "TrackerBattery_RotateOverflow",         "RotateOverflow"),
        new("TrackerBattery", "TrackerBattery_LowThreshold",           "LowThreshold"),
        new("TrackerBattery", "TrackerBattery_MaxEntries",             "MaxEntries"),
        new("TrackerBattery", "TrackerBattery_RotationIntervalSeconds","RotationIntervalSeconds"),
        new("TrackerBattery", "TrackerBattery_MaxEntryLength",         "MaxEntryLength"),
        new("TrackerBattery", "TrackerDevices",                        "SavedDevices"),
    ];

    private static void RenameJsonXmlFiles(string dataPath)
    {
        var renames = new[]
        {
            ("StatusList.xml",            "StatusList.json"),
            ("LastMessages.xml",          "LastMessages.json"),
            ("AppHistory.xml",            "AppHistory.json"),
            ("LastMediaLinkSessions.xml", "LastMediaLinkSessions.json"),
        };

        foreach (var (oldName, newName) in renames)
        {
            string oldPath = Path.Combine(dataPath, oldName);
            string newPath = Path.Combine(dataPath, newName);

            if (!File.Exists(oldPath)) continue;
            if (File.Exists(newPath)) continue; // Already migrated

            try
            {
                File.Copy(oldPath, newPath);
                string bakPath = oldPath + ".bak";
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(oldPath, bakPath);
                Logging.WriteInfo($"Renamed {oldName} → {newName} (backup: {oldName}.bak)");
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Failed to rename {oldName}: {ex.Message}");
            }
        }
    }
}
