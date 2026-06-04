    public async Task CreateRuntimeModulesAsync()
    {
        var timeSettings = _timeSettingsProvider.Value;
        var integrationSettings = _integrationSettingsProvider.Value;

        var pulsoidTask = CreateRuntimeModuleAsync("Pulsoid", () => new PulsoidModule(
            _appState,
            _pulsoidClient,
            _dispatcher,
            _oscSender,
            integrationSettings,
            _pulsoidOAuth,
            _env,
            _toast));
        var soundpadTask = CreateRuntimeModuleAsync("Soundpad", () => new SoundpadModule(
            1000,
            _appState,
            _dispatcher,
            integrationSettings,
            _consentService,
            _toast));
        var twitchTask = CreateRuntimeModuleAsync("Twitch", () => new TwitchModule(
            _twitchSettingsProvider,
            timeSettings,
            _twitchApiClient,
            integrationSettings,
            _dispatcher,
            _toast));
        var tikTokLiveTask = CreateRuntimeModuleAsync("TikTokLive", () => new TikTokLiveModule(
            _tikTokLiveSettingsProvider,
            _integrationSettingsProvider,
            _appState,
            _dispatcher,
            _consentService,
            _httpFactory));
        var discordTask = CreateRuntimeModuleAsync("Discord", () => new DiscordModule(
            _discordSettingsProvider,
            _oscSender,
            _dispatcher));
        var spotifyTask = CreateRuntimeModuleAsync("Spotify", () => new SpotifyModule(
            _spotifySettingsProvider,
            _spotifyDisplay,
            _mediaLinkDisplay,
            _spotifyApiClient,
            _spotifyOAuth,
            integrationSettings,
            _dispatcher,
            _toast));
        var trackerTask = CreateRuntimeModuleAsync("TrackerBattery", () => new TrackerBatteryModule(
            _trackerSettingsProvider,
            _appState,
            _trackerDisplay,
            _integrationDisplay,
            _dispatcher,
            _consentService,
            _toast));
        var vrcRadarTask = CreateRuntimeModuleAsync("VrcRadar", () => new VrcLogModule(
            _vrcLogSettingsProvider,
            integrationSettings,
            _appState,
            _oscSender,
            _dispatcher,
            _consentService,
            _toast));

        await Task.WhenAll(pulsoidTask, soundpadTask, twitchTask, tikTokLiveTask, discordTask, spotifyTask, trackerTask, vrcRadarTask).ConfigureAwait(false);

        var pulsoid = await pulsoidTask;
        var soundpad = await soundpadTask;
        var twitch = await twitchTask;
        var tikTokLive = await tikTokLiveTask;
        var discord = await discordTask;
        var spotify = await spotifyTask;
        var tracker = await trackerTask;
        var vrcRadar = await vrcRadarTask;

        await _dispatcher.InvokeAsync(() =>
        {
            if (pulsoid != null)
            {
                _host.Pulsoid = pulsoid;
                _host.RegisterModule(pulsoid);
            }
            if (soundpad != null)
            {
                _host.Soundpad = soundpad;
                _host.RegisterModule(soundpad);
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
            }
            if (discord != null)
            {
                _host.Discord = discord;
                _host.RegisterModule(discord);
            }
            if (spotify != null)
            {
                _host.Spotify = spotify;
                _host.RegisterModule(spotify);
                if (integrationSettings.IntgrSpotify && spotify.Settings.AutoConnectOnStartup)
                {
                    _afterStartupTasks.Add(async () =>
                    {
                        try { await spotify.StartAsync(); }
                        catch (Exception ex) { Logging.WriteInfo($"Spotify auto-connect failed: {ex.Message}"); }
                    });
                }
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
            }
        });

        // Event subscriptions on background thread
        if (pulsoid != null) integrationSettings.PropertyChanged += pulsoid.PropertyChangedHandler;
        if (soundpad != null) integrationSettings.PropertyChanged += soundpad.PropertyChangedHandler;
        if (tikTokLive != null) integrationSettings.PropertyChanged += tikTokLive.PropertyChangedHandler;
        if (spotify != null) integrationSettings.PropertyChanged += spotify.PropertyChangedHandler;
        if (vrcRadar != null)
        {
            integrationSettings.PropertyChanged += vrcRadar.PropertyChangedHandler;
            vrcRadar.OnVrcWorldStateChanged += () =>
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
            };
            _discordSettingsProvider.Value.PropertyChanged += (_, e) =>
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
            };
        }

        if (_appState is System.ComponentModel.INotifyPropertyChanged notifier)
        {
            if (pulsoid != null) notifier.PropertyChanged += pulsoid.PropertyChangedHandler;
            if (soundpad != null) notifier.PropertyChanged += soundpad.PropertyChangedHandler;
            if (tikTokLive != null) notifier.PropertyChanged += tikTokLive.PropertyChangedHandler;
            if (vrcRadar != null) notifier.PropertyChanged += vrcRadar.PropertyChangedHandler;
            notifier.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IAppState.MasterSwitch) && !_appState.MasterSwitch)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await _discordRichPresence.ClearAsync(); }
                        catch (Exception ex) { Logging.WriteInfo($"Discord RP clear on master off failed: {ex.Message}"); }
                    });
                }
            };
        }
    }
