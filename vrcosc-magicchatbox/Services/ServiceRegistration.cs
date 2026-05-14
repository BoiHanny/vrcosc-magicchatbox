using CommunityToolkit.Mvvm.Messaging;
using MagicChatboxAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Sections;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Configures the DI container with all application services.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceProvider ConfigureServices(int profileNumber = 0)
    {
        var services = new ServiceCollection();
        var environment = new EnvironmentService();
        if (profileNumber > 0)
            environment.SetCustomProfile(profileNumber);

        services.AddSingleton<IEncryptionService, DpapiEncryptionService>();
        services.AddSingleton<INavigationService, SafeNavigationService>();
        services.AddSingleton<IAllowedForUsingService, AllowedForUsingService>();
        services.AddSingleton<IAppInfoService, AppInfoService>();
        services.AddSingleton<IBanEnforcementService, BanEnforcementService>();
        services.AddSingleton<IEnvironmentService>(environment);

        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<ModuleFaultTracker>();
        services.AddSingleton<Classes.Modules.Twitch.ITwitchApiClient, Classes.Modules.Twitch.TwitchApiClient>();
        services.AddSingleton<ISpotifyApiClient, SpotifyApiClient>();
        services.AddSingleton<IOpenAiChatService>(sp => new OpenAiChatService(
            sp.GetRequiredService<Classes.Modules.OpenAIModule>(),
            sp.GetRequiredService<IPrivacyConsentService>()));
        services.AddSingleton<IPulsoidTokenValidator>(sp => sp.GetRequiredService<Classes.Modules.PulsoidOAuthHandler>());
        services.AddSingleton<IPulsoidClient, PulsoidApiClient>();

        services.AddSingleton<IHardwareMonitorService, HardwareMonitorService>();

        // Privacy consent service — gates sensitive OS hooks (hardware metadata, UIAutomation, SMTC, GetLastInputInfo)
        services.AddSingleton<IPrivacyConsentService, PrivacyConsentService>();
        services.AddSingleton<PrivacySectionViewModel>();

        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IWindowActivityService>(sp => new Classes.Modules.WindowActivityModule(
            sp.GetRequiredService<ISettingsProvider<WindowActivitySettings>>(),
            sp.GetRequiredService<WindowActivityDisplayState>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IPrivacyConsentService>(),
            sp.GetRequiredService<IToastService>()));

        // Time formatting — extracted from ComponentStatsModule.GetTime() to break static coupling
        services.AddSingleton<ITimeFormattingService, TimeFormattingService>();

        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();

        // Unified settings provider — JsonSettingsProvider<T> receives the profile-aware IEnvironmentService above.
        services.AddSingleton(typeof(ISettingsProvider<>), typeof(JsonSettingsProvider<>));

        services.AddSingleton<IntegrationDisplayState>();
        services.AddSingleton<AppUpdateState>();
        services.AddSingleton<ChatStatusDisplayState>();
        services.AddSingleton<OscDisplayState>();
        services.AddSingleton<TtsAudioDisplayState>();
        services.AddSingleton<OpenAIDisplayState>();
        services.AddSingleton<MediaLinkDisplayState>();
        services.AddSingleton<SpotifyDisplayState>();
        services.AddSingleton<TrackerDisplayState>();
        services.AddSingleton<PulsoidDisplayState>();
        services.AddSingleton<WindowActivityDisplayState>(sp =>
            new WindowActivityDisplayState(sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<WeatherOverrideState>(sp =>
            new WeatherOverrideState(sp.GetRequiredService<IWeatherService>()));
        services.AddSingleton<EmojiService>(sp =>
            new EmojiService(sp.GetRequiredService<ISettingsProvider<AppSettings>>().Value));

        services.AddSingleton<ViewModel>(sp => new ViewModel(
            sp.GetRequiredService<AppUpdateState>(),
            sp.GetRequiredService<OscDisplayState>(),
            sp.GetRequiredService<TtsAudioDisplayState>(),
            sp.GetRequiredService<PulsoidDisplayState>(),
            sp.GetRequiredService<EmojiService>(),
            sp.GetRequiredService<IAppInfoService>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TimeSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            new Lazy<IOscSender>(() => sp.GetRequiredService<IOscSender>()),
            sp.GetRequiredService<IMenuNavigationService>(),
            sp.GetRequiredService<IVersionService>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<INavigationService>(),
            new Lazy<ScanLoopService>(() => sp.GetRequiredService<ScanLoopService>()),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<ChattingPageViewModel>(() => sp.GetRequiredService<ChattingPageViewModel>()),
            new Lazy<StatusPageViewModel>(() => sp.GetRequiredService<StatusPageViewModel>()),
            new Lazy<IntegrationsPageViewModel>(() => sp.GetRequiredService<IntegrationsPageViewModel>()),
            new Lazy<OptionsPageViewModel>(() => sp.GetRequiredService<OptionsPageViewModel>())));
        services.AddSingleton<IAppState>(sp => sp.GetRequiredService<ViewModel>());

        services.AddSingleton<StatusPageViewModel>(sp => new StatusPageViewModel(
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IStatusListService>(),
            sp.GetRequiredService<IMenuNavigationService>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<Core.State.IUiDispatcher>()));
        services.AddSingleton<ChattingPageViewModel>(sp => new ChattingPageViewModel(
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<IAppState>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            new Lazy<ScanLoopService>(() => sp.GetRequiredService<ScanLoopService>()),
            new Lazy<OSCController>(() => sp.GetRequiredService<OSCController>()),
            new Lazy<IChatHistoryService>(() => sp.GetRequiredService<IChatHistoryService>()),
            new Lazy<IAudioService>(() => sp.GetRequiredService<IAudioService>()),
            new Lazy<IOscSender>(() => sp.GetRequiredService<IOscSender>()),
            new Lazy<ITtsPlaybackService>(() => sp.GetRequiredService<ITtsPlaybackService>()),
            sp.GetRequiredService<IOpenAiChatService>(),
            sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IntegrationsPageViewModel>(sp => new IntegrationsPageViewModel(
            sp.GetRequiredService<ChatStatusDisplayState>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<OSCController>(() => sp.GetRequiredService<OSCController>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
            sp.GetRequiredService<ISettingsProvider<SpotifySettings>>(),
            sp.GetRequiredService<ISettingsProvider<WeatherSettings>>(),
            new Lazy<ComponentStatsViewModel>(() => sp.GetRequiredService<ComponentStatsViewModel>(), LazyThreadSafetyMode.PublicationOnly),
            new Lazy<ScanLoopService>(() => sp.GetRequiredService<ScanLoopService>()),
            new Lazy<IStatePersistenceCoordinator>(() => sp.GetRequiredService<IStatePersistenceCoordinator>()),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<MediaLinkDisplayState>(),
            sp.GetRequiredService<SpotifyDisplayState>(),
            sp.GetRequiredService<TrackerDisplayState>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IMenuNavigationService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IPrivacyConsentService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<WindowActivitySectionViewModel>();
        services.AddSingleton<MediaLinkSectionViewModel>(sp => new MediaLinkSectionViewModel(
            new Lazy<IMediaLinkPersistenceService>(() => sp.GetRequiredService<IMediaLinkPersistenceService>()),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
            sp.GetRequiredService<MediaLinkDisplayState>(),
            sp.GetRequiredService<IMenuNavigationService>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<WeatherSectionViewModel>(sp => new WeatherSectionViewModel(
            sp.GetRequiredService<IWeatherService>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<WeatherSettings>>(),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<WeatherOverrideState>()));
        services.AddSingleton<TwitchSectionViewModel>(sp => new TwitchSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<TwitchSettings>>(),
            sp.GetRequiredService<INavigationService>()));
        services.AddSingleton<TikTokLiveSectionViewModel>(sp => new TikTokLiveSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TikTokLiveSettings>>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>())));
        services.AddSingleton<DiscordSectionViewModel>(sp => new DiscordSectionViewModel(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<DiscordOAuthHandler>(() => sp.GetRequiredService<DiscordOAuthHandler>()),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<DiscordRichPresenceService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<SpotifySectionViewModel>(sp => new SpotifySectionViewModel(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<SpotifySettings>>(),
            sp.GetRequiredService<SpotifyDisplayState>(),
            sp.GetRequiredService<MediaLinkDisplayState>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<TrackerBatterySectionViewModel>(sp => new TrackerBatterySectionViewModel(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<TrackerDisplayState>(),
            sp.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<IntegrationDisplayState>()));
        services.AddSingleton<PulsoidSectionViewModel>(sp => new PulsoidSectionViewModel(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<PulsoidOAuthHandler>(() => sp.GetRequiredService<PulsoidOAuthHandler>()),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<PulsoidDisplayState>(),
            sp.GetRequiredService<INavigationService>()));
        services.AddSingleton<OpenAISectionViewModel>(sp => new OpenAISectionViewModel(
            sp.GetRequiredService<ISettingsProvider<OpenAISettings>>(),
            sp.GetRequiredService<OpenAIDisplayState>(),
            new Lazy<OpenAIModule>(() => sp.GetRequiredService<OpenAIModule>()),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<INavigationService>()));
        services.AddSingleton<TtsSectionViewModel>(sp => new TtsSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<TtsAudioDisplayState>(),
            sp.GetRequiredService<INavigationService>()));
        services.AddSingleton<TimeOptionsSectionViewModel>();
        services.AddSingleton<NetworkStatisticsSectionViewModel>(sp => new NetworkStatisticsSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            new Lazy<NetworkStatisticsModule>(() => sp.GetRequiredService<NetworkStatisticsModule>())));
        services.AddSingleton<ChattingOptionsSectionViewModel>(sp => new ChattingOptionsSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>())));
        services.AddSingleton<ComponentStatsSectionViewModel>(sp => new ComponentStatsSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            new Lazy<ComponentStatsModule>(() => sp.GetRequiredService<ComponentStatsModule>(), LazyThreadSafetyMode.PublicationOnly),
            new Lazy<ComponentStatsViewModel>(() => sp.GetRequiredService<ComponentStatsViewModel>(), LazyThreadSafetyMode.PublicationOnly)));
        services.AddSingleton<StatusSectionViewModel>(sp => new StatusSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TimeSettings>>(),
            sp.GetRequiredService<EmojiService>(),
            sp.GetRequiredService<IAppState>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>())));
        services.AddSingleton<VrcRadarSectionViewModel>(sp => new VrcRadarSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<VrcLogSettings>>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>())));
        services.AddSingleton<EggDevSectionViewModel>();
        services.AddSingleton<AppOptionsSectionViewModel>(sp => new AppOptionsSectionViewModel(
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<OscSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<IAppHistoryService>(),
            sp.GetRequiredService<AppUpdateState>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IUiDispatcher>(),
            new Lazy<IStatusListService>(() => sp.GetRequiredService<IStatusListService>()),
            sp.GetRequiredService<IMenuNavigationService>(),
            sp.GetRequiredService<INavigationService>()));

        services.AddSingleton<ISettingsResetService, SettingsResetService>();
        services.AddSingleton<IOptionsSectionResetService, OptionsSectionResetService>();

        services.AddSingleton<OptionsPageViewModel>(sp => new OptionsPageViewModel(
            sp.GetRequiredService<ChatStatusDisplayState>(),
            new Lazy<OSCController>(() => sp.GetRequiredService<OSCController>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IOptionsSectionResetService>(),
            sp.GetRequiredService<IToastService>(),
            sp.GetRequiredService<WindowActivitySectionViewModel>(),
            sp.GetRequiredService<MediaLinkSectionViewModel>(),
            sp.GetRequiredService<WeatherSectionViewModel>(),
            sp.GetRequiredService<TwitchSectionViewModel>(),
            sp.GetRequiredService<TikTokLiveSectionViewModel>(),
            sp.GetRequiredService<DiscordSectionViewModel>(),
            sp.GetRequiredService<SpotifySectionViewModel>(),
            sp.GetRequiredService<TrackerBatterySectionViewModel>(),
            sp.GetRequiredService<PulsoidSectionViewModel>(),
            sp.GetRequiredService<OpenAISectionViewModel>(),
            sp.GetRequiredService<TtsSectionViewModel>(),
            sp.GetRequiredService<TimeOptionsSectionViewModel>(),
            sp.GetRequiredService<NetworkStatisticsSectionViewModel>(),
            sp.GetRequiredService<ChattingOptionsSectionViewModel>(),
            sp.GetRequiredService<ComponentStatsSectionViewModel>(),
            sp.GetRequiredService<StatusSectionViewModel>(),
            sp.GetRequiredService<AppOptionsSectionViewModel>(),
            sp.GetRequiredService<EggDevSectionViewModel>(),
            sp.GetRequiredService<PrivacySectionViewModel>(),
            sp.GetRequiredService<VrcRadarSectionViewModel>()));

        // In-app menu/tab navigation — deferred ViewModel resolution breaks circular dep
        services.AddSingleton<IMenuNavigationService>(sp =>
        {
            var nav = new MenuNavigationService(
                sp.GetRequiredService<ISettingsProvider<AppSettings>>().Value,
                pageIndex => sp.GetRequiredService<ViewModel>().SelectedMenuIndex = pageIndex,
                () => sp.GetRequiredService<ViewModel>().SelectedMenuIndex,
                sp.GetRequiredService<IUiDispatcher>());
            nav.SetExpandPrivacyAction(() => sp.GetRequiredService<PrivacySectionViewModel>().IsExpanded = true);
            nav.SetScrollToSectionAction(settingName =>
                sp.GetRequiredService<OptionsPageViewModel>().RequestScrollToSection(settingName));
            return nav;
        });

        services.AddSingleton<IModuleHost, ModuleHost>();
        services.AddSingleton(sp =>
            new Lazy<DiscordOAuthHandler>(() => sp.GetRequiredService<DiscordOAuthHandler>()));
        services.AddSingleton(sp =>
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()));
        services.AddSingleton(sp => new DiscordRichPresenceService(
            sp.GetRequiredService<ISettingsProvider<DiscordSettings>>(),
            sp.GetRequiredService<IAppState>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<IWindowActivityService>(() => sp.GetRequiredService<IWindowActivityService>()),
            new Lazy<IWeatherService>(() => sp.GetRequiredService<IWeatherService>()),
            new Lazy<ComponentStatsModule>(() => sp.GetRequiredService<ComponentStatsModule>(), LazyThreadSafetyMode.PublicationOnly),
            new Lazy<NetworkStatisticsModule>(() => sp.GetRequiredService<NetworkStatisticsModule>()),
            sp.GetRequiredService<OscDisplayState>()));
        services.AddSingleton<ModuleBootstrapper>();
        services.AddSingleton<ITtsPlaybackService>(sp => new TtsPlaybackService(
            new Lazy<TTSModule>(() => sp.GetRequiredService<TTSModule>()),
            sp.GetRequiredService<TtsAudioDisplayState>(),
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<IPrivacyConsentService>()));

        services.AddSingleton<IStatePersistenceCoordinator>(sp => new StatePersistenceCoordinator(
            new Lazy<IOscSender>(() => sp.GetRequiredService<IOscSender>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>(),
            sp.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
            sp.GetRequiredService<ISettingsProvider<OscSettings>>(),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            sp.GetRequiredService<ISettingsProvider<OpenAISettings>>(),
            sp.GetRequiredService<ISettingsProvider<TimeSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<TrackerDisplayState>(),
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            new Lazy<IAppHistoryService>(() => sp.GetRequiredService<IAppHistoryService>()),
            new Lazy<IChatHistoryService>(() => sp.GetRequiredService<IChatHistoryService>()),
            new Lazy<IMediaLinkPersistenceService>(() => sp.GetRequiredService<IMediaLinkPersistenceService>()),
            sp.GetRequiredService<HotkeyManagement>(),
            sp.GetRequiredService<IWindowActivityService>(),
            sp.GetRequiredService<IWeatherService>(),
            sp.GetRequiredService<IStatusListService>()));

        services.AddSingleton<ScanLoopService>(sp => new ScanLoopService(
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<OscDisplayState>(),
            sp.GetRequiredService<EmojiService>(),
            new Lazy<ComponentStatsModule>(() => sp.GetRequiredService<ComponentStatsModule>(), LazyThreadSafetyMode.PublicationOnly),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IWindowActivityService>(),
            sp.GetRequiredService<ITimeFormattingService>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<ChatSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            new Lazy<OSCController>(() => sp.GetRequiredService<OSCController>()),
            new Lazy<IOscSender>(() => sp.GetRequiredService<IOscSender>())));

        services.AddSingleton<IOscSender, OscSenderService>();
        services.AddSingleton<Classes.DataAndSecurity.HotkeyManagement>(sp => new Classes.DataAndSecurity.HotkeyManagement(
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<IOscSender>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<IUiDispatcher>(),
            new Lazy<ITrayIconService>(() => sp.GetRequiredService<ITrayIconService>())));
        services.AddSingleton<Classes.Modules.OpenAIModule>(sp => new Classes.Modules.OpenAIModule(
            sp.GetRequiredService<ISettingsProvider<OpenAISettings>>(),
            sp.GetRequiredService<OpenAIDisplayState>(),
            sp.GetRequiredService<IPrivacyConsentService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<ITranscriptionService>(sp => sp.GetRequiredService<Classes.Modules.OpenAIModule>());
        services.AddSingleton<Classes.Modules.PulsoidOAuthHandler>();
        services.AddSingleton<Classes.Modules.DiscordOAuthHandler>(sp =>
            new Classes.Modules.DiscordOAuthHandler(
                sp.GetRequiredService<INavigationService>()));

        // Spotify OAuth handler — Authorization Code + PKCE (no client secret)
        services.AddSingleton<Classes.Modules.SpotifyOAuthHandler>(sp =>
            new Classes.Modules.SpotifyOAuthHandler(
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<IHttpClientFactory>()));

        services.AddSingleton<Classes.DataAndSecurity.OSCController>(sp => new Classes.DataAndSecurity.OSCController(
            sp.GetRequiredService<ChatStateManager>(),
            sp.GetRequiredService<OscOutputBuilder>(),
            sp.GetRequiredService<OscBuildResultPresenter>()));
        services.AddSingleton<IOscController, OscControllerAdapter>();

        services.AddSingleton<IAppHistoryService>(sp => new AppHistoryService(
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<WindowActivityDisplayState>(),
            sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IChatHistoryService>(sp => new ChatHistoryService(
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<IAppHistoryService>(),
            sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IStatusListService, StatusListService>();
        services.AddSingleton<IComponentStatsPersistenceService, ComponentStatsPersistenceService>();
        services.AddSingleton<IVersionService>(sp => new VersionService(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<AppUpdateState>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IAudioService>(sp => new AudioService(
            sp.GetRequiredService<TtsAudioDisplayState>(),
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>(),
            sp.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IMediaLinkPersistenceService>(sp => new MediaLinkPersistenceService(
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<MediaLinkDisplayState>(),
            sp.GetRequiredService<WindowActivityDisplayState>(),
            sp.GetRequiredService<IAppHistoryService>(),
            sp.GetRequiredService<IUiDispatcher>()));

        var retryPolicy = CreateHttpRetryPolicy();

        services.AddHttpClient(Constants.HttpClients.GitHub, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UpdateCheckerUserAgent);
            client.Timeout = Constants.DefaultApiTimeout;
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        services.AddHttpClient(Constants.HttpClients.Pulsoid, client =>
        {
            client.Timeout = Constants.DefaultApiTimeout;
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        services.AddHttpClient(Constants.HttpClients.Weather, client =>
        {
            client.Timeout = Constants.WeatherApiTimeout;
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        // Twitch — retry for reads only; writes (announcements, shoutouts)
        // are non-idempotent and must NOT be auto-retried.
        // The TwitchModule is responsible for deciding whether to retry writes.
        services.AddHttpClient(Constants.HttpClients.Twitch, client =>
        {
            client.BaseAddress = new Uri(Constants.TwitchApiBaseUrl);
            client.Timeout = Constants.DefaultApiTimeout;
        })
        .AddPolicyHandler(request =>
            request.Method == HttpMethod.Get
                ? retryPolicy
                : Policy.NoOpAsync<HttpResponseMessage>())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        services.AddHttpClient(Constants.HttpClients.TikTok, client =>
        {
            client.BaseAddress = new Uri("https://www.tiktok.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) MagicChatbox/1.0");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = Constants.DefaultApiTimeout;
        })
        .AddPolicyHandler(request =>
            request.Method == HttpMethod.Get
                ? retryPolicy
                : Policy.NoOpAsync<HttpResponseMessage>())
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        // Spotify: 429 is NOT retried or circuit-broken here — SpotifyModule honors
        // Retry-After and applies its own cooldown. Polly only retries safe transient
        // 5xx/408/network errors on GET requests. HttpClient timeout (15s) exceeds the
        // module's RefreshTimeout (12s) so the module CTS controls cancellation.
        services.AddHttpClient(Constants.HttpClients.Spotify, client =>
        {
            client.BaseAddress = new Uri(Constants.SpotifyApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddPolicyHandler(request =>
            request.Method == HttpMethod.Get
                ? CreateSpotifyGetRetryPolicy()
                : Policy.NoOpAsync<HttpResponseMessage>())
        .AddPolicyHandler(CreateSpotifyCircuitBreakerPolicy());

        services.AddHttpClient(Constants.HttpClients.Tts, client =>
        {
            client.Timeout = Constants.DefaultApiTimeout;
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        services.AddHttpClient(Constants.HttpClients.ModerationApi, client =>
        {
            client.Timeout = Constants.ModerationApiTimeout;
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(CreateCircuitBreakerPolicy());

        services.AddHttpClient();

        // ComponentStatsModule — explicit factory (needs Lazy<ScanLoopService> to break circular dep)
        services.AddSingleton<ComponentStatsModule>(sp => new ComponentStatsModule(
            sp.GetRequiredService<ISettingsProvider<ComponentStatsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TimeSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IEnvironmentService>(),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<IUiDispatcher>(),
            new Lazy<IStatePersistenceCoordinator>(() => sp.GetRequiredService<IStatePersistenceCoordinator>()),
            sp.GetRequiredService<IHardwareMonitorService>(),
            sp.GetRequiredService<IPrivacyConsentService>(),
            sp.GetRequiredService<IToastService>()));
        services.AddSingleton<ComponentStatsViewModel>(sp =>
        {
            var statsModule = sp.GetRequiredService<ComponentStatsModule>();
            var vm = new ComponentStatsViewModel(statsModule);
            statsModule.SetStatsViewModel(vm);
            return vm;
        });

        services.AddSingleton<NetworkStatisticsModule>(sp => new NetworkStatisticsModule(
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<ISettingsProvider<NetworkStatsSettings>>(),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IPrivacyConsentService>(),
            1000,
            sp.GetRequiredService<IToastService>()));

        services.AddSingleton<TTSModule>(sp => new TTSModule(
            sp.GetRequiredService<ISettingsProvider<TtsSettings>>().Value,
            sp.GetRequiredService<TtsAudioDisplayState>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOscSender>(),
            sp.GetRequiredService<IToastService>()));

        services.AddSingleton<Classes.DataAndSecurity.ChatStateManager>(sp =>
            new Classes.DataAndSecurity.ChatStateManager(
                sp.GetRequiredService<ISettingsProvider<ChatSettings>>().Value,
                sp.GetRequiredService<ISettingsProvider<AppSettings>>().Value,
                sp.GetRequiredService<ChatStatusDisplayState>(),
                sp.GetRequiredService<OscDisplayState>(),
                sp.GetRequiredService<EmojiService>(),
                sp.GetRequiredService<Core.State.IUiDispatcher>()));

        // Providers whose constructors take Lazy<T> require explicit factory registrations
        // because MSDI cannot auto-resolve Lazy<T> from a conventional AddSingleton<T, TImpl>().
        services.AddSingleton<IOscProvider>(sp => new StatusOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ISettingsProvider<TimeSettings>>(),
            sp.GetRequiredService<ChatStatusDisplayState>(),
            sp.GetRequiredService<OscDisplayState>(),
            sp.GetRequiredService<EmojiService>(),
            sp.GetRequiredService<IAppState>()));
        services.AddSingleton<IOscProvider, WindowOscProvider>();
        services.AddSingleton<IOscProvider>(sp => new HeartRateOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<IAppState>()));
        services.AddSingleton<IOscProvider>(sp => new TrackerBatteryOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider, ComponentStatsOscProvider>();
        services.AddSingleton<IOscProvider>(sp => new NetworkStatsOscProvider(
            new Lazy<NetworkStatisticsModule>(() => sp.GetRequiredService<NetworkStatisticsModule>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider, TimeOscProvider>();
        services.AddSingleton<IOscProvider, WeatherOscProvider>();
        services.AddSingleton<IOscProvider>(sp => new TwitchOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new TikTokLiveOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new DiscordOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new SpotifyOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new VrcLogOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new SoundpadOscProvider(
            new Lazy<IModuleHost>(() => sp.GetRequiredService<IModuleHost>()),
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>()));
        services.AddSingleton<IOscProvider>(sp => new MediaLinkOscProvider(
            sp.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
            sp.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
            sp.GetRequiredService<ISettingsProvider<SpotifySettings>>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<MediaLinkDisplayState>(),
            sp.GetRequiredService<SpotifyDisplayState>(),
            new Lazy<IMediaLinkService>(() => App.ApplicationMediaController)));

        // OscOutputBuilder — explicit factory: MSDI's reflection-based resolution
        // struggles with constructors that combine IEnumerable<T> + other params.
        services.AddSingleton<OscOutputBuilder>(sp => new OscOutputBuilder(
            sp.GetServices<IOscProvider>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IntegrationDisplayState>(),
            sp.GetRequiredService<ISettingsProvider<AppSettings>>(),
            sp.GetRequiredService<ModuleFaultTracker>()));
        services.AddSingleton<OscBuildResultPresenter>();

        return services.BuildServiceProvider();
    }
    private static IAsyncPolicy<HttpResponseMessage> CreateHttpRetryPolicy()
        => HttpPolicyExtensions.HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                Constants.HttpRetryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)));

    private static IAsyncPolicy<HttpResponseMessage> CreateSpotifyGetRetryPolicy()
        => HttpPolicyExtensions.HandleTransientHttpError()
            .WaitAndRetryAsync(
                Constants.HttpRetryCount,
                GetSpotifyRetryDelay);

    private static IAsyncPolicy<HttpResponseMessage> CreateSpotifyCircuitBreakerPolicy()
        => HttpPolicyExtensions.HandleTransientHttpError()
            .CircuitBreakerAsync(
                Constants.CircuitBreakerFailureThreshold,
                Constants.CircuitBreakerDuration);

    private static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
        => HttpPolicyExtensions.HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                Constants.CircuitBreakerFailureThreshold,
                Constants.CircuitBreakerDuration);

    private static TimeSpan GetSpotifyRetryDelay(int attempt)
        => TimeSpan.FromSeconds(Math.Min(5d, Math.Pow(2, attempt)))
           + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
}
