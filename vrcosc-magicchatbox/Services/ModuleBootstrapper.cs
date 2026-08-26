using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Classes.Modules.Vr;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Classes.Modules.Twitch;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services.Voicemod;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

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
    private readonly ISpotifyApiClient _spotifyApiClient;
    private readonly SpotifyOAuthHandler _spotifyOAuth;
    private readonly TrackerDisplayState _trackerDisplay;
    private readonly SpotifyDisplayState _spotifyDisplay;
    private readonly MediaLinkDisplayState _mediaLinkDisplay;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly IOpenAiChatService _chatService;
    private readonly ISettingsProvider<TimeSettings> _timeSettingsProvider;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly ISettingsProvider<TwitchSettings> _twitchSettingsProvider;
    private readonly ISettingsProvider<TikTokLiveSettings> _tikTokLiveSettingsProvider;
    private readonly ISettingsProvider<TrackerBatterySettings> _trackerSettingsProvider;
    private readonly ISettingsProvider<Classes.Modules.Vr.VrPerformanceSettings> _vrPerformanceSettingsProvider;
    private readonly Vr.IOpenVrSessionService _openVrSession;
    private readonly ISettingsProvider<Classes.Modules.Lyrics.LyricsSettings> _lyricsSettingsProvider;
    private readonly Lyrics.LyricsResolver _lyricsResolver;
    private readonly LyricsDisplayState _lyricsDisplay;
    private readonly ISettingsProvider<DiscordSettings> _discordSettingsProvider;
    private readonly ISettingsProvider<SpotifySettings> _spotifySettingsProvider;
    private readonly ISettingsProvider<VrcLogSettings> _vrcLogSettingsProvider;
    private readonly ISettingsProvider<PulsoidModuleSettings> _pulsoidSettingsProvider;
    private readonly Lazy<DiscordOAuthHandler> _discordOAuth;
    private readonly DiscordRichPresenceService _discordRichPresence;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService _toast;
    private readonly ISettingsProvider<VoicemodSettings> _voicemodSettingsProvider;
    private readonly VoicemodDisplayState _voicemodDisplay;
    private readonly IVoicemodClientKeyProvider _voicemodClientKeyProvider;
    private readonly IVoicemodSocketFactory _voicemodSocketFactory;
    private readonly IVoicemodArtworkCache _voicemodArtwork;
    private readonly TaskCompletionSource _startupComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _teardownLock = new();
    private List<Action> _teardownActions = new();

    public void SignalStartupComplete() => _startupComplete.TrySetResult();

    private void TrackSubscription(Action unsubscribe)
    {
        lock (_teardownLock)
            _teardownActions.Add(unsubscribe);
    }

    public void TeardownSubscriptions()
    {
        List<Action> actions;
        lock (_teardownLock)
        {
            if (_teardownActions.Count == 0)
                return;

            actions = _teardownActions;
            _teardownActions = new List<Action>();
        }

        foreach (var unsubscribe in actions)
        {
            try
            {
                unsubscribe();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"ModuleBootstrapper: teardown action failed: {ex.Message}");
            }
        }
    }

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
        ISpotifyApiClient spotifyApiClient,
        SpotifyOAuthHandler spotifyOAuth,
        TrackerDisplayState trackerDisplay,
        SpotifyDisplayState spotifyDisplay,
        MediaLinkDisplayState mediaLinkDisplay,
        IntegrationDisplayState integrationDisplay,
        ITwitchApiClient twitchApiClient,
        IOpenAiChatService chatService,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<TwitchSettings> twitchSettingsProvider,
        ISettingsProvider<TikTokLiveSettings> tikTokLiveSettingsProvider,
        ISettingsProvider<TrackerBatterySettings> trackerSettingsProvider,
        ISettingsProvider<Classes.Modules.Vr.VrPerformanceSettings> vrPerformanceSettingsProvider,
        Vr.IOpenVrSessionService openVrSession,
        ISettingsProvider<Classes.Modules.Lyrics.LyricsSettings> lyricsSettingsProvider,
        Lyrics.LyricsResolver lyricsResolver,
        LyricsDisplayState lyricsDisplay,
        ISettingsProvider<DiscordSettings> discordSettingsProvider,
        ISettingsProvider<SpotifySettings> spotifySettingsProvider,
        ISettingsProvider<VrcLogSettings> vrcLogSettingsProvider,
        ISettingsProvider<PulsoidModuleSettings> pulsoidSettingsProvider,
        Lazy<DiscordOAuthHandler> discordOAuth,
        DiscordRichPresenceService discordRichPresence,
        IPrivacyConsentService consentService,
        IToastService toast,
        ISettingsProvider<VoicemodSettings> voicemodSettingsProvider,
        VoicemodDisplayState voicemodDisplay,
        IVoicemodClientKeyProvider voicemodClientKeyProvider,
        IVoicemodSocketFactory voicemodSocketFactory,
        IVoicemodArtworkCache voicemodArtwork)
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
        _spotifyApiClient = spotifyApiClient;
        _spotifyOAuth = spotifyOAuth;
        _trackerDisplay = trackerDisplay;
        _spotifyDisplay = spotifyDisplay;
        _mediaLinkDisplay = mediaLinkDisplay;
        _integrationDisplay = integrationDisplay;
        _twitchApiClient = twitchApiClient;
        _chatService = chatService;
        _timeSettingsProvider = timeSettingsProvider;
        _integrationSettingsProvider = integrationSettingsProvider;
        _twitchSettingsProvider = twitchSettingsProvider;
        _tikTokLiveSettingsProvider = tikTokLiveSettingsProvider;
        _trackerSettingsProvider = trackerSettingsProvider;
        _vrPerformanceSettingsProvider = vrPerformanceSettingsProvider;
        _openVrSession = openVrSession;
        _lyricsSettingsProvider = lyricsSettingsProvider;
        _lyricsResolver = lyricsResolver;
        _lyricsDisplay = lyricsDisplay;
        _discordSettingsProvider = discordSettingsProvider;
        _spotifySettingsProvider = spotifySettingsProvider;
        _vrcLogSettingsProvider = vrcLogSettingsProvider;
        _pulsoidSettingsProvider = pulsoidSettingsProvider;
        _discordOAuth = discordOAuth;
        _discordRichPresence = discordRichPresence;
        _consentService = consentService;
        _toast = toast;
        _voicemodSettingsProvider = voicemodSettingsProvider;
        _voicemodDisplay = voicemodDisplay;
        _voicemodClientKeyProvider = voicemodClientKeyProvider;
        _voicemodSocketFactory = voicemodSocketFactory;
        _voicemodArtwork = voicemodArtwork;
    }

    public Task RegisterComponentStatsAsync(ComponentStatsModule statsModule)
        => _dispatcher.InvokeAsync(() =>
        {
            _host.ComponentStats = statsModule;
            _host.RegisterModule(statsModule);
        });

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

    public async Task CreateRuntimeModulesAsync()
    {
        var timeSettings = _timeSettingsProvider.Value;
        var integrationSettings = _integrationSettingsProvider.Value;

        var lyricSources = LyricsSourceSelection.Reconcile(
            integrationSettings.IntgrLyrics,
            integrationSettings.IntgrLyrics_Spotify,
            integrationSettings.IntgrLyrics_MediaLink);
        integrationSettings.IntgrLyrics_Spotify = lyricSources.Spotify;
        integrationSettings.IntgrLyrics_MediaLink = lyricSources.MediaLink;
        integrationSettings.IntgrLyrics = lyricSources.Any;

        var pulsoid = await CreateRuntimeModuleAsync("Pulsoid", () => new PulsoidModule(
            _appState,
            _pulsoidClient,
            _dispatcher,
            _oscSender,
            integrationSettings,
            _pulsoidOAuth,
            _pulsoidSettingsProvider,
            _toast));
        var soundpad = await CreateRuntimeModuleAsync("Soundpad", () => new SoundpadModule(
            1000,
            _appState,
            _dispatcher,
            integrationSettings,
            _consentService,
            _toast));
        var voicemod = await CreateRuntimeModuleAsync("Voicemod", () => new VoicemodModule(
            _integrationSettingsProvider,
            _voicemodSettingsProvider,
            _voicemodDisplay,
            _voicemodClientKeyProvider,
            _voicemodSocketFactory,
            _dispatcher,
            _consentService,
            _voicemodArtwork));
        var twitch = await CreateRuntimeModuleAsync("Twitch", () => new TwitchModule(
            _twitchSettingsProvider,
            timeSettings,
            _twitchApiClient,
            integrationSettings,
            _dispatcher,
            _toast));
        var tikTokLive = await CreateRuntimeModuleAsync("TikTokLive", () => new TikTokLiveModule(
            _tikTokLiveSettingsProvider,
            _integrationSettingsProvider,
            _appState,
            _dispatcher,
            _consentService,
            _httpFactory));
        var discord = await CreateRuntimeModuleAsync("Discord", () => new DiscordModule(
            _discordSettingsProvider,
            _oscSender,
            _dispatcher));
        var spotify = await CreateRuntimeModuleAsync("Spotify", () => new SpotifyModule(
            _spotifySettingsProvider,
            _spotifyDisplay,
            _mediaLinkDisplay,
            _spotifyApiClient,
            _spotifyOAuth,
            integrationSettings,
            _dispatcher,
            _toast));
        var tracker = await CreateRuntimeModuleAsync("TrackerBattery", () => new TrackerBatteryModule(
            _trackerSettingsProvider,
            _appState,
            _trackerDisplay,
            _integrationDisplay,
            _dispatcher,
            _consentService,
            _openVrSession,
            _toast));
        var vrPerformance = await CreateRuntimeModuleAsync("VrPerformance", () => new VrPerformanceModule(
            _vrPerformanceSettingsProvider,
            _openVrSession,
            _appState,
            _integrationDisplay,
            _consentService));
        var lyrics = await CreateRuntimeModuleAsync("Lyrics", () => new LyricsModule(
            _lyricsSettingsProvider,
            integrationSettings,
            _lyricsResolver,
            _mediaLinkDisplay,
            _spotifyDisplay,
            _lyricsDisplay,
            _consentService));
        var vrcRadar = await CreateRuntimeModuleAsync("VrcRadar", () => new VrcLogModule(
            _vrcLogSettingsProvider,
            integrationSettings,
            _appState,
            _oscSender,
            _dispatcher,
            _consentService,
            _toast));

        await _dispatcher.InvokeAsync(() =>
        {
            if (pulsoid != null)
            {
                _host.Pulsoid = pulsoid;
                _host.RegisterModule(pulsoid);
                integrationSettings.PropertyChanged += pulsoid.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= pulsoid.PropertyChangedHandler);

                pulsoid.RestoreAuthStateFromSettings();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _startupComplete.Task;
                        await pulsoid.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        Logging.WriteInfo($"Pulsoid startup restore failed: {ex.Message}");
                    }
                });
            }

            if (soundpad != null)
            {
                _host.Soundpad = soundpad;
                _host.RegisterModule(soundpad);
                integrationSettings.PropertyChanged += soundpad.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= soundpad.PropertyChangedHandler);
            }

            if (voicemod != null)
            {
                _host.Voicemod = voicemod;
                _host.RegisterModule(voicemod);
                integrationSettings.PropertyChanged += voicemod.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= voicemod.PropertyChangedHandler);

                if (integrationSettings.IntgrVoicemod)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _startupComplete.Task;
                            await voicemod.StartAsync();
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteInfo($"Voicemod auto-connect failed: {ex.Message}");
                        }
                    });
                }
            }

            if (twitch != null)
            {
                _host.Twitch = twitch;
                _host.RegisterModule(twitch);
            }

            if (tikTokLive != null)
            {
                _host.TikTokLive = tikTokLive;
                _host.RegisterModule(tikTokLive);
                integrationSettings.PropertyChanged += tikTokLive.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= tikTokLive.PropertyChangedHandler);
            }

            if (discord != null)
            {
                _host.Discord = discord;
                _host.RegisterModule(discord);
                integrationSettings.PropertyChanged += discord.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= discord.PropertyChangedHandler);
            }

            if (spotify != null)
            {
                _host.Spotify = spotify;
                _host.RegisterModule(spotify);
                integrationSettings.PropertyChanged += spotify.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= spotify.PropertyChangedHandler);
                if (integrationSettings.IntgrSpotify && spotify.Settings.AutoConnectOnStartup)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _startupComplete.Task;
                            await spotify.StartAsync();
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteInfo($"Spotify auto-connect failed: {ex.Message}");
                        }
                    });
                }
            }

            if (tracker != null)
            {
                _host.TrackerBattery = tracker;
                _host.RegisterModule(tracker);
            }

            if (vrPerformance != null)
            {
                _host.VrPerformance = vrPerformance;
                _host.RegisterModule(vrPerformance);

                integrationSettings.PropertyChanged += vrPerformance.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= vrPerformance.PropertyChangedHandler);

                if (integrationSettings.IntgrVrPerformance)
                    _ = vrPerformance.StartAsync();
            }

            if (lyrics != null)
            {
                _host.Lyrics = lyrics;
                _host.RegisterModule(lyrics);
                integrationSettings.PropertyChanged += lyrics.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= lyrics.PropertyChangedHandler);

                if (integrationSettings.IntgrLyrics)
                    _ = lyrics.StartAsync();
            }

            if (vrcRadar != null)
            {
                _host.VrcRadar = vrcRadar;
                _host.RegisterModule(vrcRadar);
                integrationSettings.PropertyChanged += vrcRadar.PropertyChangedHandler;
                TrackSubscription(() => integrationSettings.PropertyChanged -= vrcRadar.PropertyChangedHandler);
            }

            if (vrcRadar != null)
            {
                void OnVrcWorldStateChanged()
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var worldName = vrcRadar.CurrentWorldName == "Not in a world" ? null : vrcRadar.CurrentWorldName;
                            await _discordRichPresence.UpdateAsync(
                                worldName,
                                vrcRadar.PlayerCount,
                                vrcRadar.InstanceType,
                                vrcRadar.Region,
                                vrcRadar.GetCurrentJoinUrl(),
                                vrcRadar.WorldJoinedAt);
                        }
                        catch (Exception ex) { Logging.WriteInfo($"Discord RP update failed: {ex.Message}"); }
                    });
                }

                vrcRadar.OnVrcWorldStateChanged += OnVrcWorldStateChanged;
                TrackSubscription(() => vrcRadar.OnVrcWorldStateChanged -= OnVrcWorldStateChanged);

                void OnDiscordRichPresenceSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
                {
                    if (e.PropertyName is nameof(DiscordSettings.EnableRichPresence)
                        or nameof(DiscordSettings.RichPresenceDetails)
                        or nameof(DiscordSettings.RichPresenceState)
                        or nameof(DiscordSettings.RichPresenceShowElapsed)
                        or nameof(DiscordSettings.RichPresenceShowJoinButton)
                        or nameof(DiscordSettings.RichPresenceLargeText)
                        or nameof(DiscordSettings.RichPresenceLargeImageKey)
                        or nameof(DiscordSettings.RichPresenceSmallImageKey)
                        or nameof(DiscordSettings.RichPresenceSmallText)
                        or nameof(DiscordSettings.RichPresenceShowVrDesktopMode)
                        or nameof(DiscordSettings.RichPresenceJoinButtonLabel)
                        or nameof(DiscordSettings.VoiceClientId))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (_discordSettingsProvider.Value.EnableRichPresence)
                                {
                                    var worldName = vrcRadar.CurrentWorldName == "Not in a world" ? null : vrcRadar.CurrentWorldName;
                                    await _discordRichPresence.UpdateAsync(
                                        worldName,
                                        vrcRadar.PlayerCount,
                                        vrcRadar.InstanceType,
                                        vrcRadar.Region,
                                        vrcRadar.GetCurrentJoinUrl(),
                                        vrcRadar.WorldJoinedAt);
                                }
                                else
                                {
                                    await _discordRichPresence.ClearAsync();
                                }
                            }
                            catch (Exception ex) { Logging.WriteInfo($"Discord RP toggle handler failed: {ex.Message}"); }
                        });
                    }
                }

                _discordSettingsProvider.Value.PropertyChanged += OnDiscordRichPresenceSettingsChanged;
                TrackSubscription(() => _discordSettingsProvider.Value.PropertyChanged -= OnDiscordRichPresenceSettingsChanged);
            }

            if (_appState is System.ComponentModel.INotifyPropertyChanged notifier)
            {
                if (pulsoid != null)
                {
                    notifier.PropertyChanged += pulsoid.PropertyChangedHandler;
                    TrackSubscription(() => notifier.PropertyChanged -= pulsoid.PropertyChangedHandler);
                }

                if (soundpad != null)
                {
                    notifier.PropertyChanged += soundpad.PropertyChangedHandler;
                    TrackSubscription(() => notifier.PropertyChanged -= soundpad.PropertyChangedHandler);
                }

                if (tikTokLive != null)
                {
                    notifier.PropertyChanged += tikTokLive.PropertyChangedHandler;
                    TrackSubscription(() => notifier.PropertyChanged -= tikTokLive.PropertyChangedHandler);
                }

                if (vrcRadar != null)
                {
                    notifier.PropertyChanged += vrcRadar.PropertyChangedHandler;
                    TrackSubscription(() => notifier.PropertyChanged -= vrcRadar.PropertyChangedHandler);
                }

                void OnMasterSwitchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
                {
                    if (e.PropertyName == nameof(IAppState.MasterSwitch) && !_appState.MasterSwitch)
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await _discordRichPresence.ClearAsync(); }
                            catch (Exception ex) { Logging.WriteInfo($"Discord RP clear on master off failed: {ex.Message}"); }
                        });
                    }
                }

                notifier.PropertyChanged += OnMasterSwitchChanged;
                TrackSubscription(() => notifier.PropertyChanged -= OnMasterSwitchChanged);
            }
        });

        if (discord != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task;                    bool hasToken = !string.IsNullOrWhiteSpace(discord.Settings.AccessToken);

                    if (integrationSettings.IntgrDiscord && discord.Settings.AutoConnectOnStartup && hasToken)
                    {
                        Logging.WriteInfo($"Discord voice: Auto-connecting on startup (hasToken={hasToken}, richPresenceRunning={_discordRichPresence.IsRunning})...");
                        await discord.StartAsync();
                    }
                    else
                    {
                        Logging.WriteInfo($"Discord voice: Auto-connect skipped (enabled={integrationSettings.IntgrDiscord}, autoConnect={discord.Settings.AutoConnectOnStartup}, hasToken={hasToken})");
                    }
                }
                catch (Exception ex) { Logging.WriteInfo($"Discord auto-connect failed: {ex.Message}"); }
            });
        }

        if (_discordSettingsProvider.Value.EnableRichPresence && vrcRadar != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task;
                    var worldName = vrcRadar.CurrentWorldName == "Not in a world" ? null : vrcRadar.CurrentWorldName;
                    await _discordRichPresence.UpdateAsync(
                        worldName,
                        vrcRadar.PlayerCount,
                        vrcRadar.InstanceType,
                        vrcRadar.Region,
                        vrcRadar.GetCurrentJoinUrl(),
                        vrcRadar.WorldJoinedAt);
                }
                catch (Exception ex) { Logging.WriteInfo($"Discord RP startup update failed: {ex.Message}"); }
            });
        }

        if (tikTokLive != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task;
                    if (integrationSettings.IntgrTikTokLive)
                        await tikTokLive.RefreshProfileIfStaleAsync();

                    if (integrationSettings.IntgrTikTokLive
                        && tikTokLive.Settings.EnableLiveConnector
                        && tikTokLive.Settings.AutoConnectOnStartup
                        && tikTokLive.ShouldBeRunning())
                    {
                        Logging.WriteInfo("TikTok Live: Auto-starting on startup...");
                        await tikTokLive.StartAsync();
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"TikTok Live auto-connect failed: {ex.Message}");
                }
            });
        }

        if (vrcRadar != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _startupComplete.Task;                    if (vrcRadar.ShouldBeRunning())
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
