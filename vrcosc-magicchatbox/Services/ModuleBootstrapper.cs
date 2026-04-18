using CommunityToolkit.Mvvm.Messaging;
using System.Net.Http;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Twitch;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Centralizes all module creation in a defined startup order.
/// Replaces scattered creation across App.xaml.cs, ViewModel.StartModules(), and MainWindow.xaml.cs.
/// </summary>
public class ModuleBootstrapper
{
    private readonly IModuleHost _host;
    private readonly IAppState _appState;
    private readonly IEnvironmentService _env;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IMenuNavigationService _menuNav;
    private readonly ITranscriptionService _transcription;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly IOscSender _oscSender;
    private readonly IMessenger _messenger;
    private readonly PulsoidOAuthHandler _pulsoidOAuth;
    private readonly IPulsoidClient _pulsoidClient;
    private readonly TrackerDisplayState _trackerDisplay;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly IOpenAiChatService _chatService;
    private readonly ISettingsProvider<TimeSettings> _timeSettingsProvider;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly ISettingsProvider<TwitchSettings> _twitchSettingsProvider;
    private readonly ISettingsProvider<TrackerBatterySettings> _trackerSettingsProvider;
    private readonly IPrivacyConsentService _consentService;

    public ModuleBootstrapper(
        IModuleHost host,
        IAppState appState,
        IEnvironmentService env,
        ChatStatusDisplayState chatStatus,
        IMenuNavigationService menuNav,
        ITranscriptionService transcription,
        IHttpClientFactory httpFactory,
        IUiDispatcher dispatcher,
        IOscSender oscSender,
        IMessenger messenger,
        PulsoidOAuthHandler pulsoidOAuth,
        IPulsoidClient pulsoidClient,
        TrackerDisplayState trackerDisplay,
        IntegrationDisplayState integrationDisplay,
        ITwitchApiClient twitchApiClient,
        IOpenAiChatService chatService,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<TwitchSettings> twitchSettingsProvider,
        ISettingsProvider<TrackerBatterySettings> trackerSettingsProvider,
        IPrivacyConsentService consentService)
    {
        _host = host;
        _appState = appState;
        _env = env;
        _chatStatus = chatStatus;
        _menuNav = menuNav;
        _transcription = transcription;
        _httpFactory = httpFactory;
        _dispatcher = dispatcher;
        _oscSender = oscSender;
        _messenger = messenger;
        _pulsoidOAuth = pulsoidOAuth;
        _pulsoidClient = pulsoidClient;
        _trackerDisplay = trackerDisplay;
        _integrationDisplay = integrationDisplay;
        _twitchApiClient = twitchApiClient;
        _chatService = chatService;
        _timeSettingsProvider = timeSettingsProvider;
        _integrationSettingsProvider = integrationSettingsProvider;
        _twitchSettingsProvider = twitchSettingsProvider;
        _trackerSettingsProvider = trackerSettingsProvider;
        _consentService = consentService;
    }

    /// <summary>
    /// Phase 1: Register the ComponentStatsModule that was already created by ViewModel.
    /// Called immediately after ViewModel construction.
    /// </summary>
    public void RegisterComponentStats(ComponentStatsModule statsModule)
    {
        _host.ComponentStats = statsModule;
        _host.RegisterModule(statsModule);
    }

    /// <summary>
    /// Phase 2: Create IntelliChatModule (needed before WhisperModule).
    /// Safe to call from background thread.
    /// </summary>
    public IntelliChatModule CreateIntelliChat()
    {
        var module = new IntelliChatModule(
            _env,
            _chatStatus,
            _menuNav,
            _chatService,
            _messenger,
            _dispatcher);
        _host.IntelliChat = module;
        _host.RegisterModule(module);
        return module;
    }

    /// <summary>
    /// Phase 3: Create runtime modules (Pulsoid, Soundpad, Twitch, TrackerBattery).
    /// These subscribe to IntegrationSettings.PropertyChanged for toggle reactivity.
    /// </summary>
    public void CreateRuntimeModules()
    {
        var timeSettings = _timeSettingsProvider.Value;
        var integrationSettings = _integrationSettingsProvider.Value;

        var pulsoid = new PulsoidModule(
            _appState,
            _pulsoidClient,
            _dispatcher,
            _oscSender,
            integrationSettings,
            _pulsoidOAuth,
            _env);
        var soundpad = new SoundpadModule(1000, _appState, _dispatcher, integrationSettings);
        var twitch = new TwitchModule(
            _twitchSettingsProvider,
            timeSettings,
            _twitchApiClient,
            integrationSettings,
            _dispatcher);
        var tracker = new TrackerBatteryModule(
            _trackerSettingsProvider,
            _appState,
            _trackerDisplay,
            _integrationDisplay,
            _dispatcher);

        _host.Pulsoid = pulsoid;
        _host.Soundpad = soundpad;
        _host.Twitch = twitch;
        _host.TrackerBattery = tracker;

        _host.RegisterModule(pulsoid);
        _host.RegisterModule(soundpad);
        _host.RegisterModule(twitch);
        _host.RegisterModule(tracker);

        // Wire up static IntegrationSettings reference for MediaLinkSettings POCO
        MediaLinkSettings.SetIntegrationSettings(integrationSettings);

        integrationSettings.PropertyChanged += pulsoid.PropertyChangedHandler;
        integrationSettings.PropertyChanged += soundpad.PropertyChangedHandler;

        // ViewModel.PropertyChanged carries IsVRRunning and PulsoidAuthConnected changes
        if (_appState is System.ComponentModel.INotifyPropertyChanged notifier)
        {
            notifier.PropertyChanged += pulsoid.PropertyChangedHandler;
            notifier.PropertyChanged += soundpad.PropertyChangedHandler;
        }
    }

    /// <summary>
    /// Phase 4: Create WhisperModule and AfkModule (require IntelliChatModule to exist).
    /// </summary>
    public void CreateLateModules()
    {
        var timeSettings = _timeSettingsProvider.Value;

        var whisper = new WhisperModule(
            _menuNav,
            _transcription,
            _dispatcher,
            _messenger);
        _host.Whisper = whisper;
        _host.RegisterModule(whisper);

        var afk = new AfkModule(_appState, _dispatcher, timeSettings, _env, _consentService);
        _host.Afk = afk;
        _host.RegisterModule(afk);
    }
}
