using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
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
    private readonly ISettingsProvider<TikTokLiveSettings> _tikTokLive;
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
    private readonly ISettingsProvider<VoicemodSettings> _voicemod;
    private readonly ISettingsProvider<vrcosc_magicchatbox.Classes.Modules.Lyrics.LyricsSettings> _lyrics;
    private readonly ISettingsProvider<vrcosc_magicchatbox.Classes.Modules.Vr.VrPerformanceSettings> _vrPerformance;
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly DiscordRichPresenceService _discordRichPresence;

    public OptionsSectionResetService(
        ISettingsResetService reset,
        ISettingsProvider<AppSettings> app,
        ISettingsProvider<IntegrationSettings> integrations,
        ISettingsProvider<TimeSettings> time,
        ISettingsProvider<WeatherSettings> weather,
        ISettingsProvider<TwitchSettings> twitch,
        ISettingsProvider<TikTokLiveSettings> tikTokLive,
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
        ISettingsProvider<VoicemodSettings> voicemod,
        ISettingsProvider<vrcosc_magicchatbox.Classes.Modules.Lyrics.LyricsSettings> lyrics,
        ISettingsProvider<vrcosc_magicchatbox.Classes.Modules.Vr.VrPerformanceSettings> vrPerformance,
        Lazy<IModuleHost> moduleHost,
        DiscordRichPresenceService discordRichPresence)
    {
        _reset = reset;
        _app = app;
        _integrations = integrations;
        _time = time;
        _weather = weather;
        _twitch = twitch;
        _tikTokLive = tikTokLive;
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
        _voicemod = voicemod;
        _lyrics = lyrics;
        _vrPerformance = vrPerformance;
        _moduleHost = moduleHost;
        _discordRichPresence = discordRichPresence;
    }

    public async Task<OptionsSectionResetResult> ResetSectionAsync(string sectionKey)
    {
        var key = NormalizeKey(sectionKey);
        int count = 0;

        switch (key)
        {
            case "status":
                count += _reset.ResetProperties(_app, StatusAppSettings);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrStatus), nameof(IntegrationSettings.IntgrStatus_VR), nameof(IntegrationSettings.IntgrStatus_DESKTOP));
                return Result("Status", count);

            case "vrc-radar":
                return await ResetModuleSectionAsync(
                    "VRChat Reader",
                    _moduleHost.Value.VrcRadar,
                    () => _reset.ResetAll(_vrcLog)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrVrcRadar_VR), nameof(IntegrationSettings.IntgrVrcRadar_DESKTOP)))
                    .ConfigureAwait(false);

            case "pulsoid":
                return await ResetModuleSectionAsync(
                    "Heart Rate",
                    _moduleHost.Value.Pulsoid,
                    () =>
                    {
                        int reset = _reset.ResetAll(_pulsoid);
                        _moduleHost.Value.Pulsoid?.RefreshTrendSymbols();
                        _moduleHost.Value.Pulsoid?.RefreshTimeRanges();

                        // Rebuilding the trend symbols and time ranges mutates the settings again after
                        // the reset already wrote them, so the rebuilt lists need a write of their own.
                        _pulsoid.FlushPendingSave();

                        return reset + ResetIntegration(
                            nameof(IntegrationSettings.IntgrHeartRate_VR),
                            nameof(IntegrationSettings.IntgrHeartRate_DESKTOP),
                            nameof(IntegrationSettings.IntgrHeartRate_OSC));
                    })
                    .ConfigureAwait(false);

            case "time":
                count += _reset.ResetAll(_time);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrScanWindowTime), nameof(IntegrationSettings.IntgrCurrentTime_VR), nameof(IntegrationSettings.IntgrCurrentTime_DESKTOP));
                return Result("Time", count);

            case "weather":
                count += _reset.ResetAll(_weather);
                count += ResetIntegration(nameof(IntegrationSettings.IntgrWeather_VR), nameof(IntegrationSettings.IntgrWeather_DESKTOP));
                return Result("Weather", count);

            case "twitch":
                return await ResetModuleSectionAsync(
                    "Twitch",
                    _moduleHost.Value.Twitch,
                    () => _reset.ResetAll(_twitch)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrTwitch_VR), nameof(IntegrationSettings.IntgrTwitch_DESKTOP)))
                    .ConfigureAwait(false);

            case "tiktok-live":
                return await ResetModuleSectionAsync(
                    "TikTok",
                    _moduleHost.Value.TikTokLive,
                    () => _reset.ResetAll(_tikTokLive)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrTikTokLive_VR), nameof(IntegrationSettings.IntgrTikTokLive_DESKTOP)))
                    .ConfigureAwait(false);

            case "discord":
                return await ResetModuleSectionAsync(
                    "Discord",
                    _moduleHost.Value.Discord,
                    () => _reset.ResetAll(_discord)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrDiscord_VR), nameof(IntegrationSettings.IntgrDiscord_DESKTOP)),
                    afterReset: () => _discordRichPresence.ClearAsync())
                    .ConfigureAwait(false);

            case "spotify":
                return await ResetModuleSectionAsync(
                    "Spotify",
                    _moduleHost.Value.Spotify,
                    () => _reset.ResetAll(_spotify)
                        + ResetIntegration(
                            nameof(IntegrationSettings.IntgrSpotify_VR),
                            nameof(IntegrationSettings.IntgrSpotify_DESKTOP),
                            nameof(IntegrationSettings.IntgrSpotifyStatus_VR),
                            nameof(IntegrationSettings.IntgrSpotifyStatus_DESKTOP)))
                    .ConfigureAwait(false);

            case "lyrics":
                return await ResetModuleSectionAsync(
                    "Lyrics",
                    _moduleHost.Value.Lyrics,
                    () => _reset.ResetAll(_lyrics)
                        + ResetIntegration(
                            nameof(IntegrationSettings.IntgrLyrics_Spotify),
                            nameof(IntegrationSettings.IntgrLyrics_MediaLink),
                            nameof(IntegrationSettings.IntgrLyrics_VR),
                            nameof(IntegrationSettings.IntgrLyrics_DESKTOP)))
                    .ConfigureAwait(false);

            case "vr-performance":
                return await ResetModuleSectionAsync(
                    "VR Performance",
                    _moduleHost.Value.VrPerformance,
                    () => _reset.ResetAll(_vrPerformance))
                    .ConfigureAwait(false);

            case "openai":
                count += _reset.ResetAll(_openAI);
                count += _reset.ResetProperties(_chat, [nameof(ChatSettings.HideOpenAITools)]);
                return Result("OpenAI", count, note: "Credentials were preserved.");

            case "component-stats":
                return await ResetModuleSectionAsync(
                    "Component Stats",
                    _moduleHost.Value.ComponentStats,
                    () => _reset.ResetAll(_componentStats)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrComponentStats_VR), nameof(IntegrationSettings.IntgrComponentStats_DESKTOP)))
                    .ConfigureAwait(false);

            case "network-statistics":
                return await ResetModuleSectionAsync(
                    "Network Statistics",
                    FindModule<NetworkStatisticsModule>(),
                    () => _reset.ResetAll(_networkStats)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrNetworkStatistics_VR), nameof(IntegrationSettings.IntgrNetworkStatistics_DESKTOP)))
                    .ConfigureAwait(false);

            case "chatting":
                count += _reset.ResetAll(_chat);
                return Result("Chatting", count);

            case "tts":
                count += _reset.ResetAll(_tts);
                return Result("Speech To Text / TTS", count);

            case "voicemod":
                return await ResetModuleSectionAsync(
                    "Voicemod",
                    _moduleHost.Value.Voicemod,
                    () => _reset.ResetAll(_voicemod)
                        + ResetIntegration(nameof(IntegrationSettings.IntgrVoicemod_VR), nameof(IntegrationSettings.IntgrVoicemod_DESKTOP)),
                    note: "Credentials were preserved.")
                    .ConfigureAwait(false);

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
                return await ResetModuleSectionAsync(
                    "Tracker Battery",
                    _moduleHost.Value.TrackerBattery,
                    () => _reset.ResetAll(_trackerBattery))
                    .ConfigureAwait(false);

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

    /// <summary>
    /// Stops the section's module, resets the section, and brings the module back up when it was
    /// running beforehand.
    /// </summary>
    private static async Task<OptionsSectionResetResult> ResetModuleSectionAsync(
        string displayName,
        IModule? module,
        Func<int> resetSettings,
        Func<Task>? afterReset = null,
        string? note = null)
    {
        // Read this before anything is written: a module whose running state is derived from the
        // settings being reset can no longer answer the question afterwards.
        bool wasRunning = module is { IsRunning: true };
        bool stopFailed = false;

        if (wasRunning)
        {
            try
            {
                Logging.WriteInfo($"[SettingsReset] Stopping '{module!.Name}' before resetting its settings.");
                await module.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logging.WriteException(ex, MSGBox: false);
                stopFailed = true;
            }
        }

        int count = resetSettings();

        if (afterReset is not null)
        {
            try
            {
                await afterReset().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        if (!wasRunning)
            return Result(displayName, count, note: note);

        if (stopFailed)
            return Result(displayName, count, restartFailed: true, note: note);

        try
        {
            await module!.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
            return Result(displayName, count, restartFailed: true, note: note);
        }

        // Claim a restart only when the module says it is up again, not merely because the calls returned.
        return Result(displayName, count, restarted: module!.IsRunning, note: note);
    }

    private IModule? FindModule<T>() where T : class, IModule
        => _moduleHost.Value.AllModules.OfType<T>().FirstOrDefault();

    /// <summary>
    /// Resets integration flags. Sections that own a module pass their display-mode flags only: the
    /// master integration toggle is the user's on/off choice rather than a tuning value, so it
    /// survives the reset and the module can be brought back up on it.
    /// </summary>
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

        return new(displayName, count, RestartRequired: false, JoinNotes(restarted ? "Running module was restarted." : null, note));
    }

    private static string? JoinNotes(string? first, string? second)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(first))
            parts.Add(first!);
        if (!string.IsNullOrWhiteSpace(second))
            parts.Add(second!);

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string NormalizeKey(string sectionKey)
        => (sectionKey ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");

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
        nameof(AppSettings.StartWithSteamVr),
        nameof(AppSettings.QuitWithSteamVr),
        nameof(AppSettings.Topmost),
        nameof(AppSettings.CheckUpdateOnStartup),
        nameof(AppSettings.StableUpdateMode),
        nameof(AppSettings.PreReleaseUpdateMode),
        nameof(AppSettings.AppOpacity),
        nameof(AppSettings.AppIsEnabled),
        nameof(AppSettings.StartInBackground),
        nameof(AppSettings.MinimizeToTray),
        nameof(AppSettings.CloseToTray),
        nameof(AppSettings.MinimizeToTrayOnMinimize),
        nameof(AppSettings.EnableTrayNotifications),
        nameof(AppSettings.ShowTrayRunningReminder),
        nameof(AppSettings.OpenTrayWithAltX),
        nameof(AppSettings.ShowHiddenIntegrationWarning),
        nameof(AppSettings.ReducedVisuals),
        nameof(AppSettings.ReducedVisualsInVr),
    ];

    private static readonly string[] EggSettings =
    [
        nameof(AppSettings.EggPrefixIconStatus),
        nameof(AppSettings.BlankEgg),
        nameof(AppSettings.SettingsDev)
    ];
}
