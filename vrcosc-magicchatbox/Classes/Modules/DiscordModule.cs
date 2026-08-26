using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class DiscordModule : ObservableObject, IModule
{
    private readonly ISettingsProvider<DiscordSettings> _settingsProvider;
    private readonly IOscSender _oscSender;
    private readonly IUiDispatcher _dispatcher;

    private DiscordIpcClient? _ipcClient;
    private string? _currentChannelId;
    private bool _disposed;
    private Timer? _channelRefreshTimer;
    private const int ChannelRefreshIntervalMs = 30_000;
    private CancellationTokenSource? _reconnectCts;

    private const int MaxChannelChars = 24;
    private const int MinChannelChars = 12;
    private const int MaxSpeakerNameChars = 16;

    public const string UnknownSpeaker = "someone";

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _speakerDebounce = new();

    private readonly HashSet<string> _userIdsInVc = new();
    private readonly HashSet<string> _speakingUserIds = new();
    private readonly object _vcLock = new();
    private readonly object _speakLock = new();

    private readonly ConcurrentDictionary<string, string> _userNames = new();

    private string? _selfUserId;

    public DiscordSettings Settings => _settingsProvider.Value;
    public string EffectiveVoiceClientId => string.IsNullOrWhiteSpace(Settings.VoiceClientId)
        ? Core.Constants.DiscordClientId
        : Settings.VoiceClientId.Trim();
    public void SaveSettings() => _settingsProvider.Save();

    public string Name => "Discord";
    public bool IsEnabled { get; set; } = true;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isInVoiceChannel;
    [ObservableProperty] private string _currentChannelName = string.Empty;
    [ObservableProperty] private int _voiceChannelCount;
    [ObservableProperty] private bool _isSelfMuted;
    [ObservableProperty] private bool _isSelfDeafened;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isReady;

    bool IModule.IsRunning => IsRunning;

    public DiscordModule(
        ISettingsProvider<DiscordSettings> settingsProvider,
        IOscSender oscSender,
        IUiDispatcher dispatcher)
    {
        _settingsProvider = settingsProvider;
        _oscSender = oscSender;
        _dispatcher = dispatcher;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;

        _ipcClient = new DiscordIpcClient();
        _ipcClient.MessageReceived += OnIpcMessage;
        _ipcClient.Disconnected += OnIpcDisconnected;

        if (await _ipcClient.ConnectAsync(ct).ConfigureAwait(false))
        {
            await _ipcClient.SendHandshakeAsync(EffectiveVoiceClientId).ConfigureAwait(false);
            _dispatcher.BeginInvoke(() => IsRunning = true);
        }
        else
        {
            Logging.WriteInfo("Discord IPC: Could not connect to any Discord pipe. Starting auto-reconnect.");
            StartAutoReconnect();
        }
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        StopCore();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { StopCore(); }
        catch (Exception ex) { Logging.WriteInfo($"Discord: Error during dispose: {ex.Message}"); }
    }

    public void PropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IntegrationSettings.IntgrDiscord))
            return;

        if (sender is not IntegrationSettings settings)
            return;

        if (settings.IntgrDiscord)
            _ = StartAsync();
        else
            _ = StopAsync();
    }

    private void StartAutoReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var ct = _reconnectCts.Token;

        _ = Task.Run(async () =>
        {
            int attempt = 1;
            TimeSpan delay = Core.Constants.DiscordReconnectMinDelay;
            while (!ct.IsCancellationRequested && !_disposed)
            {
                Logging.WriteInfo($"Discord IPC reconnect attempt {attempt} in {delay.TotalSeconds:0.#}s");

                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested || _disposed) return;

                    if (_ipcClient != null && await _ipcClient.ConnectAsync(ct).ConfigureAwait(false))
                    {
                        await OnReconnectedAsync().ConfigureAwait(false);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Discord IPC reconnect failed: {ex.Message}");
                }

                attempt++;
                delay = TimeSpan.FromSeconds(Math.Min(
                    delay.TotalSeconds * 2,
                    Core.Constants.DiscordReconnectMaxDelay.TotalSeconds));
            }
        }, ct);
    }

    private void StopCore()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;

        _dispatcher.BeginInvoke(() => IsRunning = false);

        if (_ipcClient != null)
        {
            _ipcClient.MessageReceived -= OnIpcMessage;
            _ipcClient.Disconnected -= OnIpcDisconnected;
            _ipcClient.Disconnect();
            _ipcClient.Dispose();
            _ipcClient = null;
        }

        ClearState();
        ResetAllOscParams();
    }

    public string GetOutputString(int budget = OscBuildContext.MaxOscLength)
    {
        if (!IsInVoiceChannel || string.IsNullOrEmpty(_currentChannelId))
            return SegmentWriter.Truncate(Settings.NotInVcText, budget);

        List<string> names;
        lock (_speakLock)
        {
            var ids = Settings.HideSelfFromSpeakers && _selfUserId != null
                ? _speakingUserIds.Where(id => id != _selfUserId)
                : _speakingUserIds.AsEnumerable();

            names = ids.Select(id => _userNames.GetValueOrDefault(id, UnknownSpeaker)).ToList();
        }

        return BuildOutputString(
            Settings, CurrentChannelName, VoiceChannelCount, names, IsSelfMuted, IsSelfDeafened, budget);
    }

    public static string BuildOutputString(
        DiscordSettings settings,
        string channelName,
        int channelCount,
        IReadOnlyList<string> speakerNames,
        bool isMuted,
        bool isDeafened,
        int budget)
    {
        if (budget <= 0)
            return string.Empty;

        var names = speakerNames.Select(n => SegmentWriter.Truncate(n, MaxSpeakerNameChars)).ToList();
        string channel = SegmentWriter.Truncate(channelName, MaxChannelChars);

        string muteEmoji = string.Empty;
        if (settings.ShowMuteDeafenEmoji)
        {
            if (isDeafened) muteEmoji = settings.DeafenEmoji;
            else if (isMuted) muteEmoji = settings.MuteEmoji;
        }

        string muteState = isDeafened ? "deafened" : isMuted ? "muted" : "unmuted";
        string voiceState = names.Count > 0 ? "speaking" : "quiet";

        string Speakers(int show)
        {
            if (settings.ShowUserCountOnly)
                return names.Count.ToString();

            if (names.Count == 0)
                return State(settings.EmptySpeakingText).Rendered;

            int take = Math.Clamp(show, 1, names.Count);
            string shown = string.Join(", ", names.Take(take));

            return names.Count > take
                ? new SegmentWriter().Field(OscText.Value(shown), State($"(+{names.Count - take})")).Text
                : shown;
        }

        string Render(string channelText, string speakingText)
            => settings.Template
                .Replace("{channel}", channelText)
                .Replace("{count}", channelCount.ToString())
                .Replace("{speaking}", speakingText)
                .Replace("{speaking_count}", names.Count.ToString())
                .Replace("{mute_emoji}", muteEmoji)
                .Replace("{mute_state}", State(muteState).Rendered)
                .Replace("{voice_state}", State(voiceState).Rendered)
                .Replace("\\n", "\n").Replace("/n", "\n");

        return SegmentWriter.Fit(
            budget,
            () => Render(channel, Speakers(settings.MaxSpeakingUsersToShow)),
            () => Render(channel, Speakers(1)),
            () => Render(channel, names.Count.ToString()),
            () => Render(SegmentWriter.Truncate(channel, MinChannelChars), names.Count.ToString()));
    }

    private static OscText State(string? word)
    {
        string text = word?.Trim() ?? string.Empty;

        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c) && !SuperscriptText.CanRaise(char.ToLowerInvariant(c)))
                return OscText.Raw(text);
        }

        return OscText.Label(text);
    }

    public static string ResolveDisplayName(string? nick, string? globalName, string? username)
        => !string.IsNullOrWhiteSpace(nick) ? nick
         : !string.IsNullOrWhiteSpace(globalName) ? globalName
         : !string.IsNullOrWhiteSpace(username) ? username
         : UnknownSpeaker;

    private void OnIpcMessage(JObject message)
    {
        try
        {
            var cmd = message["cmd"]?.ToString();
            var evt = message["evt"]?.ToString();
            var data = message["data"] as JObject;

            switch (cmd)
            {
                case "DISPATCH":
                    HandleDispatch(evt, data);
                    break;

                case "AUTHENTICATE":
                    HandleAuthenticateResponse(evt, data);
                    break;

                case "GET_SELECTED_VOICE_CHANNEL":
                    HandleGetSelectedVoiceChannel(evt, data);
                    break;

                case "SUBSCRIBE":
                    if (evt == "ERROR")
                        Logging.WriteInfo($"Discord subscribe error: {data}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Discord message handler error: {ex.Message}");
        }
    }

    private void HandleDispatch(string? evt, JObject? data)
    {
        if (data == null) return;

        switch (evt)
        {
            case "READY":
                _dispatcher.BeginInvoke(() => IsReady = true);

                if (!string.IsNullOrWhiteSpace(Settings.AccessToken))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _ipcClient!.SendAuthenticateAsync(
                                Settings.AccessToken,
                                Guid.NewGuid().ToString());
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteInfo($"Discord AUTHENTICATE send failed: {ex.Message}");
                        }
                    });
                }
                break;

            case "VOICE_CHANNEL_SELECT":
                HandleVoiceChannelSelect(data);
                break;

            case "VOICE_STATE_CREATE":
                HandleVoiceStateCreate(data);
                break;

            case "VOICE_STATE_UPDATE":
                HandleVoiceStateUpdate(data);
                break;

            case "VOICE_STATE_DELETE":
                HandleVoiceStateDelete(data);
                break;

            case "SPEAKING_START":
                HandleSpeakingStart(data);
                break;

            case "SPEAKING_STOP":
                HandleSpeakingStop(data);
                break;
        }
    }

    private void HandleAuthenticateResponse(string? evt, JObject? data)
    {
        if (evt == "ERROR")
        {
            string code = data?["code"]?.ToString() ?? "unknown";
            string message = data?["message"]?.ToString() ?? "No error message returned.";
            Logging.WriteInfo($"Discord authentication failed: code={code}, message={message}");
            _dispatcher.BeginInvoke(() => IsAuthenticated = false);
            return;
        }

        _selfUserId = data?["user"]?["id"]?.ToString();
        Logging.WriteInfo($"Discord authenticated successfully. Self userId={_selfUserId}");
        _dispatcher.BeginInvoke(() => IsAuthenticated = true);

        var scopes = data?["scopes"] as JArray;
        bool hasRpcScope;
        if (scopes != null)
        {
            hasRpcScope = scopes.Any(s =>
            {
                var str = s.ToString();
                return str is "rpc" or "rpc.voice.read" or "rpc.voice.channel.read";
            });
            Logging.WriteInfo($"Discord: AUTHENTICATE scopes={string.Join(", ", scopes)}, hasRpc={hasRpcScope}");
            if (hasRpcScope != Settings.HasRpcScope)
            {
                Settings.HasRpcScope = hasRpcScope;
                SaveSettings();
            }
        }
        else
        {
            hasRpcScope = Settings.HasRpcScope;
            Logging.WriteInfo($"Discord: No scopes in AUTHENTICATE response, using stored HasRpcScope={hasRpcScope}");
        }

        if (!hasRpcScope)
        {
            Logging.WriteInfo("Discord: No rpc/voice scope — voice features unavailable. Rich Presence still works.");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Logging.WriteInfo("Discord: Subscribing to VOICE_CHANNEL_SELECT...");
                await _ipcClient!.SubscribeAsync("VOICE_CHANNEL_SELECT");
                Logging.WriteInfo("Discord: Requesting current voice channel...");
                await _ipcClient.SendGetSelectedVoiceChannelAsync();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Discord post-auth subscribe failed: {ex.Message}");
            }
        });
    }

    private void HandleGetSelectedVoiceChannel(string? evt, JObject? data)
    {
        if (evt == "ERROR" || data == null)
        {
            Logging.WriteInfo($"Discord GET_SELECTED_VOICE_CHANNEL error or null data: evt={evt}");
            if (_currentChannelId == null)
                ClearVoiceState();
            return;
        }

        var channelId = data["id"]?.ToString();
        if (string.IsNullOrEmpty(channelId))
        {
            Logging.WriteInfo("Discord: Not in a voice channel (no id in response).");
            ClearVoiceState();
            return;
        }

        var channelName = data["name"]?.ToString();
        if (string.IsNullOrEmpty(channelName))
            channelName = "Call";

        var voiceStates = data["voice_states"] as JArray;
        lock (_vcLock)
        {
            _userIdsInVc.Clear();
            _userNames.Clear();
        }

        Logging.WriteInfo($"Discord: Channel data — name='{channelName}', voice_states count={voiceStates?.Count ?? 0}");

        if (voiceStates != null)
        {
            foreach (var vs in voiceStates)
            {
                var user = vs["user"];
                var userId = user?["id"]?.ToString();
                var displayName = ResolveDisplayName(
                    vs["nick"]?.ToString(),
                    user?["global_name"]?.ToString(),
                    user?["username"]?.ToString());

                if (userId != null)
                {
                    lock (_vcLock) _userIdsInVc.Add(userId);
                    _userNames[userId] = displayName;
                }

                if (userId == _selfUserId)
                {
                    var voiceState = vs["voice_state"];
                    if (voiceState != null)
                    {
                        bool selfMute = voiceState["self_mute"]?.Value<bool>() == true;
                        bool selfDeaf = voiceState["self_deaf"]?.Value<bool>() == true;
                        bool serverMute = voiceState["mute"]?.Value<bool>() == true;
                        bool serverDeaf = voiceState["deaf"]?.Value<bool>() == true;
                        _dispatcher.BeginInvoke(() =>
                        {
                            IsSelfMuted = selfMute || serverMute;
                            IsSelfDeafened = selfDeaf || serverDeaf;
                        });
                    }
                }
            }
        }

        Logging.WriteInfo($"Discord: Joined voice channel '{channelName}' (id={channelId}) with {_userIdsInVc.Count} users.");
        SetVoiceChannel(channelId, channelName);
    }

    private void HandleVoiceChannelSelect(JObject data)
    {
        var channelId = data["channel_id"]?.ToString();

        if (string.IsNullOrEmpty(channelId))
        {
            ClearVoiceState();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _ipcClient!.SendGetSelectedVoiceChannelAsync();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Discord re-fetch channel failed: {ex.Message}");
            }
        });
    }

    private void HandleVoiceStateCreate(JObject data)
    {
        var user = data["user"];
        var userId = user?["id"]?.ToString();
        var displayName = ResolveDisplayName(
            data["nick"]?.ToString(),
            user?["global_name"]?.ToString(),
            user?["username"]?.ToString());

        if (userId != null)
        {
            _userNames[userId] = displayName;
            lock (_vcLock) _userIdsInVc.Add(userId);
            UpdateVoiceCount();
            EmitVoiceStateOsc();
        }
    }

    private void HandleVoiceStateUpdate(JObject data)
    {
        var user = data["user"];
        var userId = user?["id"]?.ToString();
        var voiceState = data["voice_state"];
        if (voiceState == null || userId == null) return;

        if (userId == _selfUserId)
        {
            bool selfMute = voiceState["self_mute"]?.Value<bool>() == true;
            bool selfDeaf = voiceState["self_deaf"]?.Value<bool>() == true;
            bool serverMute = voiceState["mute"]?.Value<bool>() == true;
            bool serverDeaf = voiceState["deaf"]?.Value<bool>() == true;

            _dispatcher.BeginInvoke(() =>
            {
                IsSelfMuted = selfMute || serverMute;
                IsSelfDeafened = selfDeaf || serverDeaf;
            });

            EmitMuteDeafenOsc();
        }

        var nick = data["nick"]?.ToString();
        var username = user?["username"]?.ToString();
        var globalName = user?["global_name"]?.ToString();
        if (!string.IsNullOrEmpty(nick))
            _userNames[userId] = nick;
        else if (!string.IsNullOrEmpty(globalName))
            _userNames[userId] = globalName;
        else if (!string.IsNullOrEmpty(username))
            _userNames[userId] = username;
    }

    private void HandleVoiceStateDelete(JObject data)
    {
        var user = data["user"];
        var userId = user?["id"]?.ToString();

        if (userId != null)
        {
            _userNames.TryRemove(userId, out _);
            lock (_vcLock) _userIdsInVc.Remove(userId);
            lock (_speakLock) _speakingUserIds.Remove(userId);
            CancelSpeakerDebounce(userId);
            UpdateVoiceCount();
            EmitVoiceStateOsc();
        }
    }

    private void HandleSpeakingStart(JObject data)
    {
        var userId = data["user_id"]?.ToString();
        if (userId == null) return;

        CancelSpeakerDebounce(userId);
        lock (_speakLock) _speakingUserIds.Add(userId);
        EmitVoiceStateOsc();
    }

    private void HandleSpeakingStop(JObject data)
    {
        var userId = data["user_id"]?.ToString();
        if (userId == null) return;

        CancelSpeakerDebounce(userId);
        var cts = new CancellationTokenSource();
        _speakerDebounce[userId] = cts;

        var debounceMs = Math.Clamp(Settings.SpeakerDebounceMs, 100, 5000);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(debounceMs, cts.Token);
                lock (_speakLock) _speakingUserIds.Remove(userId);
                _speakerDebounce.TryRemove(userId, out _);
                EmitVoiceStateOsc();
            }
            catch (OperationCanceledException) { }
        });
    }

    private void SetVoiceChannel(string channelId, string channelName)
    {
        bool isNewChannel = _currentChannelId != channelId;
        _currentChannelId = channelId;
        _dispatcher.BeginInvoke(() =>
        {
            CurrentChannelName = channelName;
            IsInVoiceChannel = true;
            UpdateVoiceCount();
        });

        EmitVoiceStateOsc();
        EmitMuteDeafenOsc();

        if (isNewChannel)
        {
            StartChannelRefreshTimer();
            _ = Task.Run(async () =>
            {
                try
                {
                    var args = new JObject { ["channel_id"] = channelId };
                    await _ipcClient!.SubscribeAsync("VOICE_STATE_CREATE", args);
                    await _ipcClient.SubscribeAsync("VOICE_STATE_UPDATE", args);
                    await _ipcClient.SubscribeAsync("VOICE_STATE_DELETE", args);
                    await _ipcClient.SubscribeAsync("SPEAKING_START", args);
                    await _ipcClient.SubscribeAsync("SPEAKING_STOP", args);
                    Logging.WriteInfo($"Discord: Subscribed to channel events for {channelId}.");
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Discord channel subscribe failed: {ex.Message}");
                }
            });
        }
    }

    private void ClearVoiceState()
    {
        StopChannelRefreshTimer();
        var oldChannelId = _currentChannelId;
        _currentChannelId = null;

        if (!string.IsNullOrEmpty(oldChannelId) && _ipcClient?.IsConnected == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var args = new JObject { ["channel_id"] = oldChannelId };
                    await UnsubscribeAsync("VOICE_STATE_CREATE", args);
                    await UnsubscribeAsync("VOICE_STATE_UPDATE", args);
                    await UnsubscribeAsync("VOICE_STATE_DELETE", args);
                    await UnsubscribeAsync("SPEAKING_START", args);
                    await UnsubscribeAsync("SPEAKING_STOP", args);
                }
                catch { }
            });
        }

        lock (_vcLock) _userIdsInVc.Clear();
        lock (_speakLock) _speakingUserIds.Clear();
        _userNames.Clear();
        ClearAllSpeakerDebounce();

        _dispatcher.BeginInvoke(() =>
        {
            IsInVoiceChannel = false;
            CurrentChannelName = string.Empty;
            VoiceChannelCount = 0;
            IsSelfMuted = false;
            IsSelfDeafened = false;
        });

        ResetAllOscParams();
    }

    private void StartChannelRefreshTimer()
    {
        StopChannelRefreshTimer();
        _channelRefreshTimer = new Timer(OnChannelRefreshTick, null, ChannelRefreshIntervalMs, ChannelRefreshIntervalMs);
    }

    private void StopChannelRefreshTimer()
    {
        _channelRefreshTimer?.Dispose();
        _channelRefreshTimer = null;
    }

    private void OnChannelRefreshTick(object? state)
    {
        if (_ipcClient?.IsConnected != true || _currentChannelId == null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _ipcClient!.SendGetSelectedVoiceChannelAsync();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Discord channel periodic refresh failed: {ex.Message}");
            }
        });
    }

    private void ClearState()
    {
        _selfUserId = null;
        ClearVoiceState();
        _dispatcher.BeginInvoke(() =>
        {
            IsAuthenticated = false;
            IsReady = false;
        });
    }

    private void UpdateVoiceCount()
    {
        int count;
        lock (_vcLock) count = _userIdsInVc.Count;
        _dispatcher.BeginInvoke(() => VoiceChannelCount = count);
    }

    private void CancelSpeakerDebounce(string userId)
    {
        if (_speakerDebounce.TryRemove(userId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private void ClearAllSpeakerDebounce()
    {
        foreach (var kvp in _speakerDebounce)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _speakerDebounce.Clear();
    }

    private void EmitMuteDeafenOsc()
    {
        if (!Settings.SendMuteDeafenOsc) return;
        _oscSender.SendOscParam("/avatar/parameters/DiscordMuted", IsSelfMuted);
        _oscSender.SendOscParam("/avatar/parameters/DiscordDeafened", IsSelfDeafened);
    }

    private void EmitVoiceStateOsc()
    {
        if (!Settings.SendVoiceStateOsc) return;
        _oscSender.SendOscParam("/avatar/parameters/DiscordInVC", IsInVoiceChannel);
        _oscSender.SendOscParam("/avatar/parameters/DiscordVCCount", (float)VoiceChannelCount);

        bool anySpeaking;
        lock (_speakLock) anySpeaking = _speakingUserIds.Count > 0;
        _oscSender.SendOscParam("/avatar/parameters/DiscordSpeaking", anySpeaking);
    }

    private void ResetAllOscParams()
    {
        if (Settings.SendMuteDeafenOsc)
        {
            _oscSender.SendOscParam("/avatar/parameters/DiscordMuted", false);
            _oscSender.SendOscParam("/avatar/parameters/DiscordDeafened", false);
        }
        if (Settings.SendVoiceStateOsc)
        {
            _oscSender.SendOscParam("/avatar/parameters/DiscordInVC", false);
            _oscSender.SendOscParam("/avatar/parameters/DiscordVCCount", 0f);
            _oscSender.SendOscParam("/avatar/parameters/DiscordSpeaking", false);
        }
    }

    private async Task UnsubscribeAsync(string evt, JObject? args = null)
    {
        if (_ipcClient == null) return;
        var payload = new JObject
        {
            ["cmd"] = "UNSUBSCRIBE",
            ["evt"] = evt,
            ["nonce"] = Guid.NewGuid().ToString()
        };
        if (args != null) payload["args"] = args;
        await _ipcClient.SendFrameAsync(payload).ConfigureAwait(false);
    }

    private void OnIpcDisconnected(Exception? ex)
    {
        Logging.WriteInfo($"Discord IPC disconnected: {ex?.Message ?? "unknown reason"}");

        ClearState();
        _dispatcher.BeginInvoke(() => IsRunning = false);

        if (!_disposed)
        {
            StartAutoReconnect();
        }
    }

    private async Task OnReconnectedAsync()
    {
        _dispatcher.BeginInvoke(() => IsRunning = true);
        await _ipcClient!.SendHandshakeAsync(EffectiveVoiceClientId).ConfigureAwait(false);
    }
}
