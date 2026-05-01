using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Services;

public sealed class OptionsSectionResetService : IOptionsSectionResetService
{
    private readonly ISettingsResetService _reset;
    private readonly ISettingsProvider<AppSettings> _app;
    private readonly ISettingsProvider<IntegrationSettings> _integrations;
    private readonly ISettingsProvider<TimeSettings> _time;
    private readonly ISettingsProvider<WeatherSettings> _weather;
    private readonly ISettingsProvider<TwitchSettings> _twitch;
    private readonly ISettingsProvider<DiscordSettings> _discord;
    private readonly ISettingsProvider<SpotifySettings> _spotify;
    private readonly ISettingsProvider<OpenAISettings> _openAI;
    private readonly ISettingsProvider<ComponentStatsSettings> _componentStats;
    private readonly ISettingsProvider<NetworkStatsSettings> _networkStats;
    private readonly ISettingsProvider<ChatSettings> _chat;
    private readonly ISettingsProvider<TtsSettings> _tts;
    private readonly ISettingsProvider<MediaLinkSettings> _mediaLink;
    private readonly ISettingsProvider<TrackerBatterySettings> _trackerBattery;
    private readonly ISettingsProvider<WindowActivitySettings> _windowActivity;
    private readonly ISettingsProvider<VrcLogSettings> _vrcLog;
    private readonly ISettingsProvider<PulsoidModuleSettings> _pulsoid;
    private readonly ISettingsProvider<OscSettings> _osc;
    private readonly ISettingsProvider<PrivacySettings> _privacy;
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly DiscordRichPresenceService _discordRichPresence;

    public OptionsSectionResetService(
        ISettingsResetService reset,
        ISettingsProvider<AppSettings> app,
        ISettingsProvider<IntegrationSettings> integrations,
        ISettingsProvider<TimeSettings> time,
        ISettingsProvider<WeatherSettings> weather,
        ISettingsProvider<TwitchSettings> twitch,
        ISettingsProvider<DiscordSettings> discord,
        ISettingsProvider<SpotifySettings> spotify,
        ISettingsProvider<OpenAISettings> openAI,
        ISettingsProvider<ComponentStatsSettings> componentStats,
        ISettingsProvider<NetworkStatsSettings> networkStats,
        ISettingsProvider<ChatSettings> chat,
        ISettingsProvider<TtsSettings> tts,
        ISettingsProvider<MediaLinkSettings> mediaLink,
        ISettingsProvider<TrackerBatterySettings> trackerBattery,
        ISettingsProvider<WindowActivitySettings> windowActivity,
        ISettingsProvider<VrcLogSettings> vrcLog,
        ISettingsProvider<PulsoidModuleSettings> pulsoid,
        ISettingsProvider<OscSettings> osc,
        ISettingsProvider<PrivacySettings> privacy,
        Lazy<IModuleHost> moduleHost,
        DiscordRichPresenceService discordRichPresence)
    {
        _reset = reset;
        _app = app;
        _integrations = integrations;
        _time = time;
        _weather = weather;
        _twitch = twitch;
        _discord = discord;
        _spotify = spotify;
        _openAI = openAI;
        _componentStats = componentStats;
        _networkStats = networkStats;
        _chat = chat;
        _tts = tts;
        _mediaLink = mediaLink;
        _trackerBattery = trackerBattery;
        _windowActivity = windowActivity;
        _vrcLog = vrcLog;
        _pulsoid = pulsoid;
        _osc = osc;
        _privacy = privacy;
        _moduleHost = moduleHost;
        _discordRichPresence = discordRichPresence;
    }

    public async Task<OptionsSectionResetResult> ResetSectionAsync(string sectionKey)
    {
        var key = NormalizeKey(sectionKey);
        int count = 0;
        bool restarted = false;
        bool restartFailed = false;

        switch (key)
        {
            case "status":
                count += _reset.ResetProperties(_app, StatusAppSettings);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrStatus), nameof(IntegrationSettings.IntgrStatus_VR), nameof(IntegrationSettings.IntgrStatus_DESKTOP));
                return Result("Status", count);

            case "vrc-radar":
                count += _reset.ResetAll(_vrcLog);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrVrcRadar), nameof(IntegrationSettings.IntgrVrcRadar_VR), nameof(IntegrationSettings.IntgrVrcRadar_DESKTOP));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.VrcRadar, () => restartFailed = true).ConfigureAwait(false);
                return Result("VRChat Reader", count, restarted, restartFailed);

            case "pulsoid":
                count += _reset.ResetAll(_pulsoid);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrHeartRate), nameof(IntegrationSettings.IntgrHeartRate_VR), nameof(IntegrationSettings.IntgrHeartRate_DESKTOP), nameof(IntegrationSettings.IntgrHeartRate_OSC));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.Pulsoid, () => restartFailed = true).ConfigureAwait(false);
                return Result("Heart Rate", count, restarted, restartFailed);

            case "time":
                count += _reset.ResetAll(_time);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrScanWindowTime), nameof(IntegrationSettings.IntgrCurrentTime_VR), nameof(IntegrationSettings.IntgrCurrentTime_DESKTOP));
                return Result("Time", count);

            case "weather":
                count += _reset.ResetAll(_weather);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrWeather_VR), nameof(IntegrationSettings.IntgrWeather_DESKTOP));
                return Result("Weather", count);

            case "twitch":
                count += _reset.ResetAll(_twitch);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrTwitch), nameof(IntegrationSettings.IntgrTwitch_VR), nameof(IntegrationSettings.IntgrTwitch_DESKTOP));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.Twitch, () => restartFailed = true).ConfigureAwait(false);
                return Result("Twitch", count, restarted, restartFailed);

            case "discord":
                count += _reset.ResetAll(_discord);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrDiscord), nameof(IntegrationSettings.IntgrDiscord_VR), nameof(IntegrationSettings.IntgrDiscord_DESKTOP));
                await _discordRichPresence.ClearAsync().ConfigureAwait(false);
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.Discord, () => restartFailed = true).ConfigureAwait(false);
                return Result("Discord", count, restarted, restartFailed);

            case "spotify":
                count += _reset.ResetAll(_spotify);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrSpotify), nameof(IntegrationSettings.IntgrSpotify_VR), nameof(IntegrationSettings.IntgrSpotify_DESKTOP), nameof(IntegrationSettings.IntgrSpotifyStatus_VR), nameof(IntegrationSettings.IntgrSpotifyStatus_DESKTOP));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.Spotify, () => restartFailed = true).ConfigureAwait(false);
                return Result("Spotify", count, restarted, restartFailed);

            case "openai":
                count += _reset.ResetAll(_openAI);
                count += _reset.ResetProperties(_chat, [nameof(ChatSettings.HideOpenAITools)]);
                return Result("OpenAI", count, note: "Credentials were preserved.");

            case "component-stats":
                count += _reset.ResetAll(_componentStats);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrComponentStats), nameof(IntegrationSettings.IntgrComponentStats_VR), nameof(IntegrationSettings.IntgrComponentStats_DESKTOP));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.ComponentStats, () => restartFailed = true).ConfigureAwait(false);
                return Result("Component Stats", count, restarted, restartFailed);

            case "network-statistics":
                count += _reset.ResetAll(_networkStats);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrNetworkStatistics), nameof(IntegrationSettings.IntgrNetworkStatistics_VR), nameof(IntegrationSettings.IntgrNetworkStatistics_DESKTOP));
                return Result("Network Statistics", count);

            case "chatting":
                count += _reset.ResetAll(_chat);
                return Result("Chatting", count);

            case "tts":
                count += _reset.ResetAll(_tts);
                return Result("Speech To Text / TTS", count);

            case "media-link":
                count += _reset.ResetAll(_mediaLink);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrScanMediaLink), nameof(IntegrationSettings.IntgrMediaLink_VR), nameof(IntegrationSettings.IntgrMediaLink_DESKTOP));
                return Result("MediaLink", count);

            case "app-options":
                count += _reset.ResetProperties(_app, AppOptionsSettings);
                count += _reset.ResetAll(_osc, preserveCredentials: false);
                return Result("App Options", count);

            case "egg-dev":
                count += _reset.ResetProperties(_app, EggSettings);
                return Result("Egg Options", count);

            case "tracker-battery":
                count += _reset.ResetAll(_trackerBattery);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrTrackerBattery));
                restarted |= await RestartIfRunningAsync(_moduleHost.Value.TrackerBattery, () => restartFailed = true).ConfigureAwait(false);
                return Result("Tracker Battery", count, restarted, restartFailed);

            case "privacy":
                count += _reset.ResetAll(_privacy, preserveCredentials: false);
                return Result("Privacy", count, note: "Privacy consent prompts may appear again when gated features are used.");

            case "window-activity":
                count += _reset.ResetAll(_windowActivity);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrScanWindowActivity), nameof(IntegrationSettings.IntgrWindowActivity_VR), nameof(IntegrationSettings.IntgrWindowActivity_DESKTOP), nameof(IntegrationSettings.ApplicationHookV2));
                return Result("Window Activity", count);

            default:
                Logging.WriteInfo($"[SettingsReset] Unknown section key '{sectionKey}'.");
                return Result("Unknown section", 0, note: "No reset mapping exists for this section.");
        }
    }

    private int ResetIntegration(params string[] propertyNames)
        => _reset.ResetProperties(_integrations, propertyNames, preserveCredentials: false);

    private static OptionsSectionResetResult Result(
        string displayName,
        int count,
        bool restarted = false,
        bool restartFailed = false,
        string? note = null)
    {
        if (restartFailed)
            return new(displayName, count, RestartRequired: true, "Running module could not be restarted automatically; restart MagicChatbox.");

        return new(displayName, count, RestartRequired: false, restarted ? "Running module was restarted." : note);
    }

    private static string NormalizeKey(string sectionKey)
        => (sectionKey ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");

    private static async Task<bool> RestartIfRunningAsync(IModule? module, Action onFailure)
    {
        if (module is null || !module.IsRunning)
            return false;

        try
        {
            Logging.WriteInfo($"[SettingsReset] Restarting running module '{module.Name}' after settings reset.");
            await module.StopAsync().ConfigureAwait(false);
            await module.StartAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
            onFailure();
            return false;
        }
    }

    private static readonly string[] StatusAppSettings =
    [
        nameof(AppSettings.PrefixIconStatus),
        nameof(AppSettings.EnableEmojiShuffle),
        nameof(AppSettings.SwitchStatusInterval),
        nameof(AppSettings.IsRandomCycling),
        nameof(AppSettings.CycleStatus),
        nameof(AppSettings.CycleOverrideCurrentGroup),
        nameof(AppSettings.CycleOverrideGroupId),
        nameof(AppSettings.LastSelectedGroupId),
        nameof(AppSettings.StatusRoundCorners)
    ];

    private static readonly string[] AppOptionsSettings =
    [
        nameof(AppSettings.ScanningInterval),
        nameof(AppSettings.ScanPauseTimeout),
        nameof(AppSettings.PrefixIconMusic),
        nameof(AppSettings.PrefixIconSoundpad),
        nameof(AppSettings.OscMessagePrefix),
        nameof(AppSettings.OscMessageSeparator),
        nameof(AppSettings.OscMessageSuffix),
        nameof(AppSettings.SeperateWithENTERS),
        nameof(AppSettings.CountOculusSystemAsVR),
        nameof(AppSettings.Topmost),
        nameof(AppSettings.CheckUpdateOnStartup),
        nameof(AppSettings.AppOpacity),
        nameof(AppSettings.AppIsEnabled)
    ];

    private static readonly string[] EggSettings =
    [
        nameof(AppSettings.EggPrefixIconStatus),
        nameof(AppSettings.BlankEgg),
        nameof(AppSettings.SettingsDev)
    ];
}
