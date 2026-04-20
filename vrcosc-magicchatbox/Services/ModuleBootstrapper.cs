using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Twitch;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Centralizes all module creation in a defined startup order.
/// Replaces scattered creation across App.xaml.cs, ViewModel.StartModules(), and MainWindow.xaml.cs.
/// </summary>
public class ModuleBootstrapper
{
    private static readonly TimeSpan RuntimeModuleCreationTimeout = TimeSpan.FromSeconds(6);

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
    private readonly ISettingsProvider<DiscordSettings> _discordSettingsProvider;
    private readonly ISettingsProvider<VrcLogSettings> _vrcLogSettingsProvider;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService _toast;
    private readonly TaskCompletionSource _startupComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Signals that all startup initialization is complete.
    /// Modules waiting for auto-start should await this before connecting.
    /// </summary>
    public void SignalStartupComplete() => _startupComplete.TrySetResult();

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
        ISettingsProvider<DiscordSettings> discordSettingsProvider,
        ISettingsProvider<VrcLogSettings> vrcLogSettingsProvider,
        IPrivacyConsentService consentService,
        IToastService toast)
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
        _discordSettingsProvider = discordSettingsProvider;
        _vrcLogSettingsProvider = vrcLogSettingsProvider;
        _consentService = consentService;
        _toast = toast;
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
            _dispatcher,
            _toast);
        _host.IntelliChat = module;
        _host.RegisterModule(module);
        return module;
    }

    /// <summary>
    /// Phase 3: Create runtime modules (Pulsoid, Soundpad, Twitch, TrackerBattery).
    /// These subscribe to IntegrationSettings.PropertyChanged for toggle reactivity.
    /// </summary>
    public async Task CreateRuntimeModulesAsync()
    {
        var timeSettings = _timeSettingsProvider.Value;
        var integrationSettings = _integrationSettingsProvider.Value;

        var pulsoid = await CreateRuntimeModuleAsync("Pulsoid", () => new PulsoidModule(
            _appState,
            _pulsoidClient,
            _dispatcher,
            _oscSender,
            integrationSettings,
            _pulsoidOAuth,
            _env,
            _toast));
        var soundpad = await CreateRuntimeModuleAsync("Soundpad", () => new SoundpadModule(
            1000,
            _appState,
            _dispatcher,
            integrationSettings,
            _toast));
        var twitch = await CreateRuntimeModuleAsync("Twitch", () => new TwitchModule(
            _twitchSettingsProvider,
            timeSettings,
            _twitchApiClient,
            integrationSettings,
            _dispatcher,
            _toast));
        var discord = await CreateRuntimeModuleAsync("Discord", () => new DiscordModule(
            _discordSettingsProvider,
            _oscSender,
            _dispatcher));
        var tracker = await CreateRuntimeModuleAsync("TrackerBattery", () => new TrackerBatteryModule(
            _trackerSettingsProvider,
            _appState,
            _trackerDisplay,
            _integrationDisplay,
            _dispatcher,
            _toast));
        var vrcRadar = await CreateRuntimeModuleAsync("VrcRadar", () => new VrcLogModule(
            _vrcLogSettingsProvider,
            integrationSettings,
            _appState,
            _oscSender,
            _dispatcher));

        await _dispatcher.InvokeAsync(() =>
        {
            if (pulsoid != null)
            {
                _host.Pulsoid = pulsoid;
                _host.RegisterModule(pulsoid);
                integrationSettings.PropertyChanged += pulsoid.PropertyChangedHandler;
            }

            if (soundpad != null)
            {
                _host.Soundpad = soundpad;
                _host.RegisterModule(soundpad);
                integrationSettings.PropertyChanged += soundpad.PropertyChangedHandler;
            }

            if (twitch != null)
            {
                _host.Twitch = twitch;
                _host.RegisterModule(twitch);
            }

            if (discord != null)
            {
                _host.Discord = discord;
                _host.RegisterModule(discord);
            }

            if (tracker != null)
            {
                _host.TrackerBattery = tracker;
                _host.RegisterModule(tracker);
            }

            if (vrcRadar != null)
            {
                _host.VrcRadar = vrcRadar;
                _host.RegisterModule(vrcRadar);
                integrationSettings.PropertyChanged += vrcRadar.PropertyChangedHandler;
            }

            // Wire up static IntegrationSettings reference for MediaLinkSettings POCO
            MediaLinkSettings.SetIntegrationSettings(integrationSettings);

            // ViewModel.PropertyChanged carries IsVRRunning and PulsoidAuthConnected changes
            if (_appState is System.ComponentModel.INotifyPropertyChanged notifier)
            {
                if (pulsoid != null)
                    notifier.PropertyChanged += pulsoid.PropertyChangedHandler;

                if (soundpad != null)
                    notifier.PropertyChanged += soundpad.PropertyChangedHandler;

                if (vrcRadar != null)
                    notifier.PropertyChanged += vrcRadar.PropertyChangedHandler;
            }
        });

        // Discord auto-connect: start the module if user has a saved token and auto-connect enabled
        if (discord != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task; // wait for full startup instead of fixed delay
                    if (discord.Settings.AutoConnectOnStartup &&
                        !string.IsNullOrWhiteSpace(discord.Settings.AccessToken))
                    {
                        Logging.WriteInfo("Discord: Auto-connecting on startup...");
                        await discord.StartAsync();
                    }
                    else
                    {
                        Logging.WriteInfo($"Discord: Auto-connect skipped (enabled={discord.Settings.AutoConnectOnStartup}, hasToken={!string.IsNullOrWhiteSpace(discord.Settings.AccessToken)})");
                    }
                }
                catch (Exception ex) { Logging.WriteInfo($"Discord auto-connect failed: {ex.Message}"); }
            });
        }

        // VrcRadar auto-start: begin log tailing if integration toggles are enabled
        if (vrcRadar != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task; // wait for full startup instead of fixed delay
                    if (vrcRadar.ShouldBeRunning())
                    {
                        Logging.WriteInfo("VrcRadar: Auto-starting on startup...");
                        await vrcRadar.StartAsync();
                    }
                    else
                    {
                        Logging.WriteInfo($"VrcRadar: Auto-start skipped (enabled={_integrationSettingsProvider.Value.IntgrVrcRadar})");
                    }
                }
                catch (Exception ex) { Logging.WriteInfo($"VrcRadar auto-start failed: {ex.Message}"); }
            });
        }
    }

    private static async Task<T?> CreateRuntimeModuleAsync<T>(string moduleName, Func<T> factory)
        where T : class
    {
        Logging.WriteInfo($"Creating runtime module: {moduleName}");

        try
        {
            var createTask = Task.Run(factory);
            var completedTask = await Task.WhenAny(createTask, Task.Delay(RuntimeModuleCreationTimeout)).ConfigureAwait(false);

            if (completedTask != createTask)
            {
                Logging.WriteInfo(
                    $"Runtime module '{moduleName}' timed out after {RuntimeModuleCreationTimeout.TotalSeconds:0}s and will be skipped.");
                return null;
            }

            var module = await createTask.ConfigureAwait(false);
            Logging.WriteInfo($"Created runtime module: {moduleName}");
            return module;
        }
        catch (Exception ex)
        {
            Logging.WriteException(new Exception($"Failed to create runtime module '{moduleName}'.", ex), MSGBox: false);
            return null;
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
            _messenger,
            _toast);
        _host.Whisper = whisper;
        _host.RegisterModule(whisper);

        var afk = new AfkModule(_appState, _dispatcher, timeSettings, _env, _consentService);
        _host.Afk = afk;
        _host.RegisterModule(afk);
    }
}
