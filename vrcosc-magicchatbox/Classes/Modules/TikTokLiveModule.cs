using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TikTokLiveSharp.Client;
using TikTokLiveSharp.Events;
using TikTokLiveSharp.Events.Objects;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// TikTok module with a public profile follower summary and an opt-in experimental LIVE connector.
/// </summary>
public sealed partial class TikTokLiveModule : ObservableObject, IModule
{
    private static readonly Regex MultiSpaceRegex = new("[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex TikTokUserNameRegex = new("^[A-Za-z0-9._]{2,24}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FollowerCountRegex = new("\"followerCount\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NicknameRegex = new("\"nickname\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UniqueIdRegex = new("\"uniqueId\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int CommentPreviewLength = 60;
    private const int UserPreviewLength = 24;
    private const int PriorityLike = 1;
    private const int PriorityComment = 2;
    private const int PriorityFollow = 3;
    private const int PriorityViewerMilestone = 4;
    private const int PriorityGift = 5;

    private readonly ISettingsProvider<TikTokLiveSettings> _settingsProvider;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _profileRefreshGate = new(1, 1);
    private readonly object _stateLock = new();

    private CancellationTokenSource? _sessionCts;
    private CancellationTokenSource? _profileRefreshDebounceCts;
    private Task? _connectionLoopTask;
    private TikTokLiveClient? _client;
    private int _sessionGeneration;

    private bool _profileRefreshing;
    private string _activeProfileUserName = string.Empty;
    private string _profileDisplayName = string.Empty;
    private long _profileFollowerCount = -1;
    private DateTime _profileFetchedAtUtc = DateTime.MinValue;
    private string _profileStatus = "Set a TikTok profile username.";
    private string _profileError = string.Empty;
    private string _profileEventText = string.Empty;
    private DateTime _profileEventExpiryUtc = DateTime.MinValue;

    private bool _moduleRunning;
    private bool _connecting;
    private bool _connected;
    private bool _live;
    private string _status = "LIVE connector is off.";
    private string _error = string.Empty;
    private string _activeHostUserName = string.Empty;
    private string _roomId = string.Empty;
    private long _viewerCount;
    private long _likeTotal;
    private string _lastEventText = string.Empty;
    private string _transientMessage = string.Empty;
    private DateTime _transientExpiryUtc = DateTime.MinValue;
    private int _transientPriority;
    private DateTime _lastLikeOverlayUtc = DateTime.MinValue;
    private int _lastViewerMilestoneBucket;
    private bool _disposed;

    private bool _isRunning;

    public TikTokLiveModule(
        ISettingsProvider<TikTokLiveSettings> settingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IAppState appState,
        IUiDispatcher dispatcher,
        IPrivacyConsentService consentService,
        IHttpClientFactory httpClientFactory)
    {
        _settingsProvider = settingsProvider;
        _integrationSettings = integrationSettingsProvider.Value;
        _appState = appState;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _httpClientFactory = httpClientFactory;

        ApplyCompatibilityMigration();
        Settings.PropertyChanged += OnSettingsChanged;
        _consentService.ConsentChanged += OnConsentChanged;
    }

    public string Name => "TikTok";

    public TikTokLiveSettings Settings => _settingsProvider.Value;

    public bool IsEnabled
    {
        get => _integrationSettings.IntgrTikTokLive;
        set => _integrationSettings.IntgrTikTokLive = value;
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private bool _isProfileRefreshing;
    [ObservableProperty] private string _profileStatusText = "Set a TikTok profile username.";
    [ObservableProperty] private string _profileDisplay = "-";
    [ObservableProperty] private string _followerCountDisplay = "-";
    [ObservableProperty] private string _profilePreview = string.Empty;
    [ObservableProperty] private string _profileEventDisplay = "-";
    [ObservableProperty] private string _statusText = "LIVE connector is off.";
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private string _hostDisplay = "-";
    [ObservableProperty] private string _roomIdDisplay = "-";
    [ObservableProperty] private string _viewerCountDisplay = "0";
    [ObservableProperty] private string _likeCountDisplay = "0";
    [ObservableProperty] private string _lastEventDisplay = "-";
    [ObservableProperty] private string _summaryPreview = string.Empty;
    [ObservableProperty] private string _outputPreview = string.Empty;

    public Task InitializeAsync(CancellationToken ct = default)
        => PublishStateAsync();

    public Task RefreshProfileAsync(CancellationToken ct = default)
        => RefreshProfileInternalAsync(force: true, ct);

    public Task RefreshProfileIfStaleAsync(CancellationToken ct = default)
        => RefreshProfileInternalAsync(force: false, ct);

    private void ApplyCompatibilityMigration()
    {
        var settings = Settings;
        if (settings.SchemaVersion >= 2)
            return;

        if (string.IsNullOrWhiteSpace(settings.ProfileUserName) && !string.IsNullOrWhiteSpace(settings.HostUserName))
        {
            settings.ProfileUserName = settings.HostUserName;
        }

        if (!settings.EnableLiveConnector
            && settings.ExperimentalEnabled
            && !string.IsNullOrWhiteSpace(settings.HostUserName))
        {
            settings.EnableLiveConnector = true;
        }

        settings.SchemaVersion = 3;
        _settingsProvider.Save();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        bool restartForHostChange = false;

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            if (!ShouldBeRunning())
            {
                lock (_stateLock)
                {
                    _moduleRunning = false;
                    _connecting = false;
                    _connected = false;
                    _live = false;
                    _status = GetBlockedReason();
                    _error = string.Empty;
                    ClearLiveSessionState_NoLock();
                }

                await PublishStateAsync().ConfigureAwait(false);
                return;
            }

            string host = ResolveLiveHostUserName();
            bool hostChanged;
            lock (_stateLock)
            {
                hostChanged = !string.Equals(_activeHostUserName, host, StringComparison.OrdinalIgnoreCase);
            }

            if (_moduleRunning && !hostChanged)
                return;

            if (_moduleRunning && hostChanged)
            {
                restartForHostChange = true;
            }
            else
            {
                int sessionId = Interlocked.Increment(ref _sessionGeneration);
                var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var loopTask = Task.Run(() => RunConnectionLoopAsync(sessionId, sessionCts.Token), CancellationToken.None);

                lock (_stateLock)
                {
                    _sessionCts = sessionCts;
                    _connectionLoopTask = loopTask;
                    _moduleRunning = true;
                    _connecting = true;
                    _connected = false;
                    _live = false;
                    _error = string.Empty;
                    _activeHostUserName = host;
                    _status = $"Connecting to @{host}...";
                    ClearLiveSessionState_NoLock();
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (restartForHostChange)
        {
            await StopInternalAsync("Switching host...", ct).ConfigureAwait(false);
            await StartAsync(ct).ConfigureAwait(false);
            return;
        }

        await PublishStateAsync().ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken ct = default)
        => StopInternalAsync("Stopped.", ct);

    public void SaveSettings() => _settingsProvider.Save();

    public void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IntegrationSettings.IntgrTikTokLive)
            or nameof(IntegrationSettings.IntgrTikTokLive_VR)
            or nameof(IntegrationSettings.IntgrTikTokLive_DESKTOP)
            or nameof(IAppState.IsVRRunning)
            or "IsVRRunning")
        {
            _ = EvaluateDesiredStateAsync(restartIfRunning: false);
            QueueProfileRefresh();
        }
    }

    public bool ShouldBeRunning()
    {
        if (!_integrationSettings.IntgrTikTokLive)
            return false;

        if (!_consentService.IsApproved(PrivacyHook.InternetAccess))
            return false;

        bool isVr = _appState.IsVRRunning;
        if (!(isVr ? _integrationSettings.IntgrTikTokLive_VR : _integrationSettings.IntgrTikTokLive_DESKTOP))
            return false;

        if (!Settings.EnableLiveConnector)
            return false;

        if (!Settings.ExperimentalEnabled)
            return false;

        return !string.IsNullOrWhiteSpace(ResolveLiveHostUserName());
    }

    public string GetOutputString()
    {
        lock (_stateLock)
        {
            return BuildOutput_NoLock();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Settings.PropertyChanged -= OnSettingsChanged;
        _consentService.ConsentChanged -= OnConsentChanged;

        CancellationTokenSource? sessionCts;
        CancellationTokenSource? profileRefreshCts;
        TikTokLiveClient? client;
        lock (_stateLock)
        {
            sessionCts = _sessionCts;
            _sessionCts = null;
            profileRefreshCts = _profileRefreshDebounceCts;
            _profileRefreshDebounceCts = null;
            client = _client;
            _client = null;
        }

        Interlocked.Increment(ref _sessionGeneration);
        sessionCts?.Cancel();
        sessionCts?.Dispose();
        profileRefreshCts?.Cancel();
        profileRefreshCts?.Dispose();
        if (client != null)
            _ = StopClientAsync(client);

        _lifecycleGate.Dispose();
        _profileRefreshGate.Dispose();
    }

    private async Task RunConnectionLoopAsync(int sessionId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!ShouldBeRunning())
                    break;

                string host = ResolveLiveHostUserName();
                var disconnectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var client = new TikTokLiveClient(
                    host,
                    reconnectInterval: Settings.ReconnectDelaySeconds,
                    enableExtendedGiftInfo: false,
                    logDebug: false);

                lock (_stateLock)
                {
                    _client = client;
                    _activeHostUserName = host;
                    _connecting = true;
                    _connected = false;
                    _live = false;
                    _status = $"Connecting to @{host}...";
                    _error = string.Empty;
                }

                await PublishStateAsync().ConfigureAwait(false);
                WireClientEvents(client, sessionId, disconnectedTcs);

                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(TimeSpan.FromSeconds(Settings.ConnectionTimeoutSeconds));

                    string roomId = await client.Start(
                        attemptCts.Token,
                        ex => OnClientException(sessionId, ex),
                        retryConnection: false).ConfigureAwait(false);

                    if (!IsActiveSession(sessionId))
                        return;

                    lock (_stateLock)
                    {
                        _roomId = roomId ?? client.RoomID ?? string.Empty;
                        _connected = true;
                        _connecting = false;
                        _live = true;
                        _status = $"Live connected to @{host}.";
                        _error = string.Empty;
                        if (client.ViewerCount.HasValue)
                            _viewerCount = client.ViewerCount.Value;
                    }

                    await PublishStateAsync().ConfigureAwait(false);
                    await disconnectedTcs.Task.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    await HandleConnectionFailureAsync(sessionId, "Connection timed out.").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, MSGBox: false);
                    await HandleConnectionFailureAsync(sessionId, SummarizeExceptionMessage(ex)).ConfigureAwait(false);
                }
                finally
                {
                    await StopClientAsync(client).ConfigureAwait(false);

                    lock (_stateLock)
                    {
                        if (ReferenceEquals(_client, client))
                            _client = null;
                    }
                }

                if (ct.IsCancellationRequested || !ShouldBeRunning())
                    break;

                int reconnectDelay = Settings.ReconnectDelaySeconds;
                lock (_stateLock)
                {
                    _connecting = false;
                    _connected = false;
                    _live = false;
                    _status = $"Reconnecting in {reconnectDelay}s...";
                }

                await PublishStateAsync().ConfigureAwait(false);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelay), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
            }
        }
        finally
        {
            if (IsActiveSession(sessionId))
            {
                string finalStatus = ct.IsCancellationRequested ? "Stopped." : GetBlockedReason();

                lock (_stateLock)
                {
                    _moduleRunning = false;
                    _connecting = false;
                    _connected = false;
                    _live = false;
                    _status = finalStatus;
                    ClearLiveSessionState_NoLock();
                    _client = null;
                    _sessionCts = null;
                    _connectionLoopTask = null;
                }

                await PublishStateAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task StopInternalAsync(string status, CancellationToken ct)
    {
        CancellationTokenSource? sessionCts = null;
        Task? connectionLoop = null;
        TikTokLiveClient? client = null;

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Interlocked.Increment(ref _sessionGeneration);

            lock (_stateLock)
            {
                sessionCts = _sessionCts;
                connectionLoop = _connectionLoopTask;
                client = _client;
                _sessionCts = null;
                _connectionLoopTask = null;
                _client = null;
                _moduleRunning = false;
                _connecting = false;
                _connected = false;
                _live = false;
                _status = status;
                _error = string.Empty;
                ClearLiveSessionState_NoLock();
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }

        sessionCts?.Cancel();
        await PublishStateAsync().ConfigureAwait(false);

        if (client != null)
            await StopClientAsync(client).ConfigureAwait(false);

        if (connectionLoop != null)
        {
            try
            {
                await connectionLoop.WaitAsync(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Logging.WriteInfo("TikTok Live: connection loop did not stop within the timeout window.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
        }

        sessionCts?.Dispose();
    }

    private async Task EvaluateDesiredStateAsync(bool restartIfRunning)
    {
        bool shouldRun = ShouldBeRunning();
        bool hostChanged;
        lock (_stateLock)
        {
            hostChanged = !string.Equals(_activeHostUserName, ResolveLiveHostUserName(), StringComparison.OrdinalIgnoreCase);
        }

        if (!shouldRun)
        {
            await StopInternalAsync("Stopped.", CancellationToken.None).ConfigureAwait(false);
            lock (_stateLock)
            {
                _status = GetBlockedReason();
            }
            await PublishStateAsync().ConfigureAwait(false);
            return;
        }

        if (restartIfRunning && hostChanged && IsRunning)
            await StopInternalAsync("Switching host...", CancellationToken.None).ConfigureAwait(false);

        await StartAsync().ConfigureAwait(false);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        string? propertyName = e.PropertyName;

        if (propertyName is nameof(TikTokLiveSettings.HostUserName)
            or nameof(TikTokLiveSettings.EnableLiveConnector)
            or nameof(TikTokLiveSettings.ExperimentalEnabled))
        {
            _ = EvaluateDesiredStateAsync(restartIfRunning: true);
            if (propertyName == nameof(TikTokLiveSettings.HostUserName) && string.IsNullOrWhiteSpace(Settings.ProfileUserName))
                QueueProfileRefresh();

            return;
        }

        if (propertyName is nameof(TikTokLiveSettings.ProfileUserName)
            or nameof(TikTokLiveSettings.ShowProfileSummary)
            or nameof(TikTokLiveSettings.ShowProfileFollowerChangeEvents)
            or nameof(TikTokLiveSettings.ProfileRefreshMinutes))
        {
            if (propertyName == nameof(TikTokLiveSettings.ProfileUserName))
                ClearProfileState();

            QueueProfileRefresh();
            _ = PublishStateAsync();
            return;
        }

        _ = PublishStateAsync();
    }

    private void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
    {
        if (e.Hook != PrivacyHook.InternetAccess)
            return;

        _ = EvaluateDesiredStateAsync(restartIfRunning: false);
        QueueProfileRefresh();
    }

    private void QueueProfileRefresh()
    {
        CancellationTokenSource nextCts = new();
        CancellationTokenSource? previousCts;
        lock (_stateLock)
        {
            previousCts = _profileRefreshDebounceCts;
            _profileRefreshDebounceCts = nextCts;
        }

        previousCts?.Cancel();
        previousCts?.Dispose();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(850), nextCts.Token).ConfigureAwait(false);
                await RefreshProfileInternalAsync(force: true, nextCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                lock (_stateLock)
                {
                    if (ReferenceEquals(_profileRefreshDebounceCts, nextCts))
                        _profileRefreshDebounceCts = null;
                }

                nextCts.Dispose();
            }
        });
    }

    private async Task RefreshProfileInternalAsync(bool force, CancellationToken ct)
    {
        await _profileRefreshGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!ShouldProfileLookupRun())
            {
                lock (_stateLock)
                {
                    _profileRefreshing = false;
                    _profileStatus = GetProfileBlockedReason();
                    _profileError = string.Empty;
                    if (!Settings.ShowProfileSummary)
                        ClearProfileSessionState_NoLock();
                }

                await PublishStateAsync().ConfigureAwait(false);
                return;
            }

            string profileUserName = ResolveProfileUserName();
            DateTime now = DateTime.UtcNow;
            lock (_stateLock)
            {
                if (!force
                    && _profileFollowerCount >= 0
                    && string.Equals(_activeProfileUserName, profileUserName, StringComparison.OrdinalIgnoreCase)
                    && now - _profileFetchedAtUtc < TimeSpan.FromMinutes(Settings.ProfileRefreshMinutes))
                {
                    return;
                }

                _profileRefreshing = true;
                _profileStatus = $"Refreshing @{profileUserName} profile...";
                _profileError = string.Empty;
            }

            await PublishStateAsync().ConfigureAwait(false);

            TikTokProfileSnapshot profile = await FetchProfileAsync(profileUserName, ct).ConfigureAwait(false);

            lock (_stateLock)
            {
                long previousFollowerCount = _profileFollowerCount;
                bool sameProfile = string.Equals(_activeProfileUserName, profile.UniqueId, StringComparison.OrdinalIgnoreCase);
                long followerIncrease = sameProfile && previousFollowerCount >= 0
                    ? profile.FollowerCount - previousFollowerCount
                    : 0;

                _profileRefreshing = false;
                _activeProfileUserName = profile.UniqueId;
                _profileDisplayName = profile.DisplayName;
                _profileFollowerCount = profile.FollowerCount;
                _profileFetchedAtUtc = now;
                _profileStatus = followerIncrease > 0
                    ? $"Profile updated: @{profile.UniqueId} (+{FormatCount(followerIncrease, Settings.CompactViewerCount)})."
                    : $"Profile updated: @{profile.UniqueId}.";
                _profileError = string.Empty;

                if (followerIncrease > 0 && Settings.ShowProfileFollowerChangeEvents)
                    SetProfileFollowerChangeTransient_NoLock(profile, followerIncrease);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            string message = SummarizeExceptionMessage(ex);
            Logging.WriteException(ex, MSGBox: false);
            lock (_stateLock)
            {
                _profileRefreshing = false;
                _profileStatus = _profileFollowerCount >= 0
                    ? $"Profile refresh failed; using last follower count. {message}"
                    : $"Profile refresh failed. {message}";
                _profileError = message;
            }
        }
        finally
        {
            _profileRefreshGate.Release();
        }

        await PublishStateAsync().ConfigureAwait(false);
    }

    private async Task<TikTokProfileSnapshot> FetchProfileAsync(string profileUserName, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(Constants.HttpClients.TikTok);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"@{Uri.EscapeDataString(profileUserName)}");
        request.Headers.Referrer = new Uri("https://www.tiktok.com/");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
            throw new InvalidOperationException("TikTok is rate-limiting or blocking public profile lookup.");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"TikTok profile lookup failed ({(int)response.StatusCode}).");

        string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!TryParseProfileSnapshot(html, profileUserName, out TikTokProfileSnapshot profile))
        {
            Logging.WriteInfo($"TikTok profile parser could not find followerCount for @{profileUserName}.");
            throw new InvalidOperationException("Follower count was not available on the public profile page.");
        }

        return profile;
    }

    private void WireClientEvents(TikTokLiveClient client, int sessionId, TaskCompletionSource<bool> disconnectedTcs)
    {
        client.OnConnected += (sender, connectedState) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _connected = true;
                _connecting = false;
                _live = true;
                _status = $"Live connected to @{_activeHostUserName}.";
                _error = string.Empty;
                _roomId = client.RoomID ?? _roomId;
                if (client.ViewerCount.HasValue)
                    _viewerCount = client.ViewerCount.Value;
            }

            _ = PublishStateAsync();
        };

        client.OnDisconnected += (sender, disconnectedState) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _connected = false;
                _connecting = false;
                _live = false;
                _status = "Disconnected from TikTok Live.";
                ClearLiveSessionState_NoLock();
            }

            disconnectedTcs.TrySetResult(true);
            _ = PublishStateAsync();
        };

        client.OnLiveEnded += (sender, controlMessage) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _connected = false;
                _connecting = false;
                _live = false;
                _status = "Live ended.";
                ClearLiveSessionState_NoLock();
            }

            disconnectedTcs.TrySetResult(true);
            _ = PublishStateAsync();
        };

        client.OnLivePaused += (sender, controlMessage) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _status = "Live paused.";
            }

            _ = PublishStateAsync();
        };

        client.OnLiveResumed += (sender, controlMessage) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _status = $"Live resumed for @{_activeHostUserName}.";
            }

            _ = PublishStateAsync();
        };

        client.OnRoomUpdate += (sender, roomUpdate) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            lock (_stateLock)
            {
                _connected = true;
                _connecting = false;
                _live = true;
                _status = $"Live connected to @{_activeHostUserName}.";
                if (roomUpdate.NumberOfViewers > 0)
                    _viewerCount = roomUpdate.NumberOfViewers;

                MaybeSetViewerMilestoneTransient_NoLock(roomUpdate.NumberOfViewers);
            }

            _ = PublishStateAsync();
        };

        client.OnFollow += (sender, follow) =>
        {
            if (!IsActiveSession(sessionId) || !Settings.ShowFollowEvents)
                return;

            string message = RenderTemplate(
                Settings.FollowTemplate,
                BuildEventTokens(
                    user: ExtractUserName(follow.User),
                    uniqueId: ExtractUniqueId(follow.User)));

            lock (_stateLock)
            {
                SetTransient_NoLock(message, Settings.EventDurationSeconds, PriorityFollow);
            }

            _ = PublishStateAsync();
        };

        client.OnChatMessage += (sender, chat) =>
        {
            if (!IsActiveSession(sessionId) || !Settings.ShowCommentEvents)
                return;

            string message = RenderTemplate(
                Settings.CommentTemplate,
                BuildEventTokens(
                    user: ExtractUserName(chat.Sender),
                    uniqueId: ExtractUniqueId(chat.Sender),
                    comment: Truncate(chat.Message, CommentPreviewLength)));

            lock (_stateLock)
            {
                SetTransient_NoLock(message, Settings.EventDurationSeconds, PriorityComment);
            }

            _ = PublishStateAsync();
        };

        client.OnGiftMessage += (sender, gift) =>
        {
            if (!IsActiveSession(sessionId) || !Settings.ShowGiftEvents)
                return;

            int count = ResolveGiftCount(gift);
            string message = RenderTemplate(
                Settings.GiftTemplate,
                BuildEventTokens(
                    user: ExtractUserName(gift.User),
                    uniqueId: ExtractUniqueId(gift.User),
                    giftName: gift.Gift?.Name,
                    count: count.ToString(CultureInfo.InvariantCulture),
                    amount: gift.Amount.ToString(CultureInfo.InvariantCulture)));

            lock (_stateLock)
            {
                SetTransient_NoLock(message, Settings.EventDurationSeconds, PriorityGift);
            }

            _ = PublishStateAsync();
        };

        client.OnLike += (sender, like) =>
        {
            if (!IsActiveSession(sessionId))
                return;

            bool showOverlay = Settings.ShowLikeEvents && like.Count >= Settings.LikeBurstThreshold;
            string message = showOverlay
                ? RenderTemplate(
                    Settings.LikeTemplate,
                    BuildEventTokens(
                        user: ExtractUserName(like.Sender),
                        uniqueId: ExtractUniqueId(like.Sender),
                        count: like.Count.ToString(CultureInfo.InvariantCulture),
                        total: like.Total.ToString(CultureInfo.InvariantCulture)))
                : string.Empty;

            lock (_stateLock)
            {
                _likeTotal = Math.Max(_likeTotal, like.Total);

                if (showOverlay && DateTime.UtcNow - _lastLikeOverlayUtc >= TimeSpan.FromSeconds(3))
                {
                    _lastLikeOverlayUtc = DateTime.UtcNow;
                    SetTransient_NoLock(message, Settings.EventDurationSeconds, PriorityLike);
                }
            }

            _ = PublishStateAsync();
        };
    }

    private void OnClientException(int sessionId, Exception exception)
    {
        if (!IsActiveSession(sessionId) || exception is OperationCanceledException)
            return;

        Logging.WriteException(exception, MSGBox: false);

        lock (_stateLock)
        {
            _error = SummarizeExceptionMessage(exception);
            if (!_connected)
                _status = _error;
        }

        _ = PublishStateAsync();
    }

    private async Task HandleConnectionFailureAsync(int sessionId, string message)
    {
        if (!IsActiveSession(sessionId))
            return;

        lock (_stateLock)
        {
            _connecting = false;
            _connected = false;
            _live = false;
            _status = message;
            _error = message;
            ClearLiveSessionState_NoLock();
        }

        await PublishStateAsync().ConfigureAwait(false);
    }

    private Task PublishStateAsync()
    {
        UiSnapshot snapshot;
        lock (_stateLock)
        {
            snapshot = CreateUiSnapshot_NoLock();
        }

        return _dispatcher.InvokeAsync(() =>
        {
            IsRunning = snapshot.IsRunning;
            IsConnecting = snapshot.IsConnecting;
            IsConnected = snapshot.IsConnected;
            IsLive = snapshot.IsLive;
            IsProfileRefreshing = snapshot.IsProfileRefreshing;
            ProfileStatusText = snapshot.ProfileStatusText;
            ProfileDisplay = snapshot.ProfileDisplay;
            FollowerCountDisplay = snapshot.FollowerCountDisplay;
            ProfilePreview = snapshot.ProfilePreview;
            ProfileEventDisplay = snapshot.ProfileEventDisplay;
            StatusText = snapshot.StatusText;
            ErrorText = snapshot.ErrorText;
            HostDisplay = snapshot.HostDisplay;
            RoomIdDisplay = snapshot.RoomIdDisplay;
            ViewerCountDisplay = snapshot.ViewerCountDisplay;
            LikeCountDisplay = snapshot.LikeCountDisplay;
            LastEventDisplay = snapshot.LastEventDisplay;
            SummaryPreview = snapshot.SummaryPreview;
            OutputPreview = snapshot.OutputPreview;
        });
    }

    private UiSnapshot CreateUiSnapshot_NoLock()
    {
        string profileSummary = BuildProfileSummary_NoLock();
        string liveSummary = BuildLiveSummary_NoLock();
        string profileOutput = BuildProfileOutput_NoLock(profileSummary);
        string liveOutput = Settings.EnableLiveConnector && _live ? BuildLiveOutput_NoLock(liveSummary) : string.Empty;
        string output = BuildOutput_NoLock(profileOutput, liveOutput);

        return new UiSnapshot(
            _moduleRunning,
            _connecting,
            _connected,
            _live,
            _profileRefreshing,
            _profileStatus,
            string.IsNullOrWhiteSpace(_activeProfileUserName) ? "-" : $"@{_activeProfileUserName}",
            _profileFollowerCount >= 0 ? FormatCount(_profileFollowerCount, Settings.CompactViewerCount) : "-",
            profileSummary,
            Settings.ShowProfileFollowerChangeEvents && HasActiveProfileEvent_NoLock() ? _profileEventText : "-",
            _status,
            _error,
            string.IsNullOrWhiteSpace(_activeHostUserName) ? "-" : $"@{_activeHostUserName}",
            string.IsNullOrWhiteSpace(_roomId) ? "-" : _roomId,
            FormatCount(_viewerCount, Settings.CompactViewerCount),
            FormatCount(_likeTotal, Settings.CompactLikeCount),
            string.IsNullOrWhiteSpace(_lastEventText) ? "-" : _lastEventText,
            liveSummary,
            output);
    }

    private string BuildProfileSummary_NoLock()
    {
        if (!Settings.ShowProfileSummary || _profileFollowerCount < 0 || string.IsNullOrWhiteSpace(_activeProfileUserName))
            return string.Empty;

        return RenderTemplate(
            Settings.ProfileTemplate,
            BuildEventTokens(
                profile: _activeProfileUserName,
                displayName: _profileDisplayName,
                followers: FormatCount(_profileFollowerCount, Settings.CompactViewerCount),
                followerCount: _profileFollowerCount.ToString(CultureInfo.InvariantCulture),
                updated: _profileFetchedAtUtc == DateTime.MinValue ? string.Empty : _profileFetchedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture)));
    }

    private string BuildLiveSummary_NoLock()
    {
        if (!_live || string.IsNullOrWhiteSpace(_activeHostUserName))
            return string.Empty;

        return RenderTemplate(
            Settings.SummaryTemplate,
            BuildEventTokens(
                host: _activeHostUserName,
                viewers: FormatCount(_viewerCount, Settings.CompactViewerCount),
                viewerCount: _viewerCount.ToString(CultureInfo.InvariantCulture),
                likes: FormatCount(_likeTotal, Settings.CompactLikeCount),
                likeCount: _likeTotal.ToString(CultureInfo.InvariantCulture),
                roomId: _roomId));
    }

    private string BuildOutput_NoLock()
    {
        string profileSummary = BuildProfileSummary_NoLock();
        string profileOutput = BuildProfileOutput_NoLock(profileSummary);
        string liveSummary = BuildLiveSummary_NoLock();
        string liveOutput = Settings.EnableLiveConnector && _live ? BuildLiveOutput_NoLock(liveSummary) : string.Empty;

        return BuildOutput_NoLock(profileOutput, liveOutput);
    }

    private string BuildProfileOutput_NoLock(string profileSummary)
    {
        if (Settings.ShowProfileFollowerChangeEvents && HasActiveProfileEvent_NoLock())
            return _profileEventText;

        return profileSummary;
    }

    private string BuildOutput_NoLock(string profileOutput, string liveOutput)
    {
        if (string.IsNullOrWhiteSpace(liveOutput))
            return profileOutput;

        if (!Settings.CombineProfileAndLive)
            return liveOutput;

        return CombineOutputParts(profileOutput, liveOutput);
    }

    private string BuildLiveOutput_NoLock(string summary)
    {
        string transient = HasActiveTransient_NoLock() ? _transientMessage : string.Empty;

        return Settings.DisplayMode switch
        {
            TikTokLiveDisplayMode.TransientOnly => transient,
            TikTokLiveDisplayMode.SummaryOnly => summary,
            _ => !string.IsNullOrWhiteSpace(transient) ? transient : summary
        };
    }

    private string CombineOutputParts(string profileOutput, string liveOutput)
    {
        var parts = new List<string>(capacity: 2);
        if (Settings.OutputOrder == TikTokOutputOrder.ProfileThenLive)
        {
            AddOutputPart(parts, profileOutput);
            AddOutputPart(parts, liveOutput);
        }
        else
        {
            AddOutputPart(parts, liveOutput);
            AddOutputPart(parts, profileOutput);
        }

        return string.Join(NormalizeSeparator(Settings.CombinedOutputSeparator), parts);
    }

    private static void AddOutputPart(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(value.Trim());
    }

    private static string NormalizeSeparator(string? separator)
        => string.IsNullOrEmpty(separator) ? " | " : separator.Replace("\\n", "\n", StringComparison.Ordinal);

    private void MaybeSetViewerMilestoneTransient_NoLock(long viewers)
    {
        if (!Settings.ShowViewerMilestones || viewers <= 0)
            return;

        int step = Settings.ViewerCountMilestoneStep;
        int bucket = (int)(viewers / step);
        if (bucket <= 0 || bucket == _lastViewerMilestoneBucket)
            return;

        _lastViewerMilestoneBucket = bucket;
        string message = RenderTemplate(
            Settings.ViewerMilestoneTemplate,
            BuildEventTokens(
                viewers: FormatCount(bucket * step, Settings.CompactViewerCount),
                viewerCount: (bucket * step).ToString(CultureInfo.InvariantCulture),
                host: _activeHostUserName));

        SetTransient_NoLock(message, Settings.ViewerMilestoneDurationSeconds, PriorityViewerMilestone);
    }

    private void SetTransient_NoLock(string message, int durationSeconds, int priority)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var now = DateTime.UtcNow;
        if (now < _transientExpiryUtc && priority < _transientPriority)
            return;

        _transientMessage = message;
        _transientExpiryUtc = now.AddSeconds(Math.Max(1, durationSeconds));
        _transientPriority = priority;
        _lastEventText = message;
    }

    private void SetProfileFollowerChangeTransient_NoLock(TikTokProfileSnapshot profile, long followerIncrease)
    {
        if (followerIncrease <= 0)
            return;

        string message = RenderTemplate(
            Settings.ProfileFollowerChangeTemplate,
            BuildEventTokens(
                profile: profile.UniqueId,
                displayName: profile.DisplayName,
                followers: FormatCount(profile.FollowerCount, Settings.CompactViewerCount),
                followerCount: profile.FollowerCount.ToString(CultureInfo.InvariantCulture),
                change: FormatCount(followerIncrease, Settings.CompactViewerCount),
                changeCount: followerIncrease.ToString(CultureInfo.InvariantCulture),
                updated: _profileFetchedAtUtc == DateTime.MinValue ? string.Empty : _profileFetchedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture)));

        if (string.IsNullOrWhiteSpace(message))
            return;

        _profileEventText = message;
        _profileEventExpiryUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, Settings.ProfileFollowerChangeDurationSeconds));
    }

    private bool HasActiveTransient_NoLock()
    {
        if (DateTime.UtcNow >= _transientExpiryUtc)
            return false;

        return !string.IsNullOrWhiteSpace(_transientMessage);
    }

    private bool HasActiveProfileEvent_NoLock()
    {
        if (DateTime.UtcNow >= _profileEventExpiryUtc)
            return false;

        return !string.IsNullOrWhiteSpace(_profileEventText);
    }

    private void ClearLiveSessionState_NoLock()
    {
        _roomId = string.Empty;
        _viewerCount = 0;
        _likeTotal = 0;
        _lastEventText = string.Empty;
        _transientMessage = string.Empty;
        _transientExpiryUtc = DateTime.MinValue;
        _transientPriority = 0;
        _lastLikeOverlayUtc = DateTime.MinValue;
        _lastViewerMilestoneBucket = 0;
    }

    private void ClearProfileState()
    {
        lock (_stateLock)
        {
            ClearProfileSessionState_NoLock();
            _profileStatus = GetProfileBlockedReason();
            _profileError = string.Empty;
        }
    }

    private void ClearProfileSessionState_NoLock()
    {
        _activeProfileUserName = string.Empty;
        _profileDisplayName = string.Empty;
        _profileFollowerCount = -1;
        _profileFetchedAtUtc = DateTime.MinValue;
        _profileEventText = string.Empty;
        _profileEventExpiryUtc = DateTime.MinValue;
    }

    private bool ShouldProfileLookupRun()
    {
        if (!_integrationSettings.IntgrTikTokLive)
            return false;

        if (!_consentService.IsApproved(PrivacyHook.InternetAccess))
            return false;

        bool isVr = _appState.IsVRRunning;
        if (!(isVr ? _integrationSettings.IntgrTikTokLive_VR : _integrationSettings.IntgrTikTokLive_DESKTOP))
            return false;

        if (!Settings.ShowProfileSummary && !Settings.ShowProfileFollowerChangeEvents)
            return false;

        return !string.IsNullOrWhiteSpace(ResolveProfileUserName());
    }

    private string GetBlockedReason()
    {
        if (!_integrationSettings.IntgrTikTokLive)
            return "TikTok is disabled.";

        if (!_consentService.IsApproved(PrivacyHook.InternetAccess))
            return "Internet access permission is required.";

        bool isVr = _appState.IsVRRunning;
        if (!(isVr ? _integrationSettings.IntgrTikTokLive_VR : _integrationSettings.IntgrTikTokLive_DESKTOP))
            return isVr ? "TikTok is disabled in VR mode." : "TikTok is disabled in Desktop mode.";

        if (!Settings.EnableLiveConnector)
            return "LIVE connector is off.";

        if (!Settings.ExperimentalEnabled)
            return "Acknowledge the experimental warning to enable TikTok Live.";

        if (string.IsNullOrWhiteSpace(ResolveLiveHostUserName()))
            return "Set a TikTok LIVE host username.";

        return "Ready to connect.";
    }

    private string GetProfileBlockedReason()
    {
        if (!_integrationSettings.IntgrTikTokLive)
            return "TikTok is disabled.";

        if (!_consentService.IsApproved(PrivacyHook.InternetAccess))
            return "Internet access permission is required.";

        bool isVr = _appState.IsVRRunning;
        if (!(isVr ? _integrationSettings.IntgrTikTokLive_VR : _integrationSettings.IntgrTikTokLive_DESKTOP))
            return isVr ? "TikTok is disabled in VR mode." : "TikTok is disabled in Desktop mode.";

        if (!Settings.ShowProfileSummary && !Settings.ShowProfileFollowerChangeEvents)
            return "Profile summary and follower alerts are off.";

        if (!Settings.ShowProfileSummary)
            return "Profile summary is off; follower alerts are active.";

        if (string.IsNullOrWhiteSpace(ResolveProfileUserName()))
            return "Set a TikTok profile username.";

        return "Ready to refresh profile.";
    }

    private static async Task StopClientAsync(TikTokLiveClient client)
    {
        try
        {
            await client.Stop().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Logging.WriteInfo("TikTok Live: timed out while stopping the client.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private bool IsActiveSession(int sessionId)
        => sessionId == Volatile.Read(ref _sessionGeneration);

    private string ResolveProfileUserName()
    {
        string preferred = !string.IsNullOrWhiteSpace(Settings.ProfileUserName)
            ? Settings.ProfileUserName
            : Settings.HostUserName;

        return NormalizeUserName(preferred);
    }

    private string ResolveLiveHostUserName()
    {
        string preferred = !string.IsNullOrWhiteSpace(Settings.HostUserName)
            ? Settings.HostUserName
            : Settings.ProfileUserName;

        return NormalizeUserName(preferred);
    }

    private static string NormalizeUserName(string? rawUserName)
    {
        string text = (rawUserName ?? string.Empty).Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            text = uri.AbsolutePath.Trim('/');
        }

        text = text.Trim().TrimStart('@');
        int separatorIndex = text.IndexOfAny(['/', '?', '#', ' ']);
        if (separatorIndex >= 0)
            text = text[..separatorIndex];

        return TikTokUserNameRegex.IsMatch(text) ? text : string.Empty;
    }

    private static string ExtractUserName(User? user)
    {
        string nickName = user?.NickName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(nickName))
            return Truncate(nickName, UserPreviewLength);

        string uniqueId = user?.UniqueId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(uniqueId))
            return Truncate(uniqueId, UserPreviewLength);

        return "Someone";
    }

    private static string ExtractUniqueId(User? user)
        => Truncate(user?.UniqueId ?? string.Empty, UserPreviewLength);

    private static int ResolveGiftCount(GiftMessage gift)
    {
        if (gift.RepeatCount > 0)
            return (int)Math.Min(gift.RepeatCount, int.MaxValue);

        if (gift.Amount > 0)
            return (int)gift.Amount;

        return 1;
    }

    private Dictionary<string, string> BuildEventTokens(
        string? user = null,
        string? uniqueId = null,
        string? comment = null,
        string? giftName = null,
        string? count = null,
        string? amount = null,
        string? total = null,
        string? host = null,
        string? viewers = null,
        string? viewerCount = null,
        string? likes = null,
        string? likeCount = null,
        string? roomId = null,
        string? profile = null,
        string? displayName = null,
        string? followers = null,
        string? followerCount = null,
        string? change = null,
        string? changeCount = null,
        string? updated = null)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["live"] = "LIVE",
            ["user"] = user ?? string.Empty,
            ["unique_id"] = uniqueId ?? string.Empty,
            ["message"] = comment ?? string.Empty,
            ["gift"] = giftName ?? string.Empty,
            ["count"] = count ?? string.Empty,
            ["amount"] = amount ?? string.Empty,
            ["total"] = total ?? string.Empty,
            ["host"] = host ?? _activeHostUserName,
            ["viewers"] = viewers ?? FormatCount(_viewerCount, Settings.CompactViewerCount),
            ["viewer_count"] = viewerCount ?? _viewerCount.ToString(CultureInfo.InvariantCulture),
            ["likes"] = likes ?? FormatCount(_likeTotal, Settings.CompactLikeCount),
            ["like_count"] = likeCount ?? _likeTotal.ToString(CultureInfo.InvariantCulture),
            ["room"] = roomId ?? _roomId,
            ["profile"] = profile ?? _activeProfileUserName,
            ["display_name"] = displayName ?? _profileDisplayName,
            ["followers"] = followers ?? (_profileFollowerCount >= 0 ? FormatCount(_profileFollowerCount, Settings.CompactViewerCount) : string.Empty),
            ["follower_count"] = followerCount ?? (_profileFollowerCount >= 0 ? _profileFollowerCount.ToString(CultureInfo.InvariantCulture) : string.Empty),
            ["change"] = change ?? string.Empty,
            ["change_count"] = changeCount ?? string.Empty,
            ["updated"] = updated ?? (_profileFetchedAtUtc == DateTime.MinValue ? string.Empty : _profileFetchedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture))
        };
    }

    private static bool TryParseProfileSnapshot(string html, string requestedUserName, out TikTokProfileSnapshot profile)
    {
        profile = default;
        Match followerMatch = FollowerCountRegex.Match(html);
        if (!followerMatch.Success
            || !long.TryParse(followerMatch.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long followerCount))
        {
            return false;
        }

        string uniqueId = ReadJsonString(UniqueIdRegex.Match(html).Groups["value"].Value);
        if (string.IsNullOrWhiteSpace(uniqueId))
            uniqueId = requestedUserName;

        string displayName = ReadJsonString(NicknameRegex.Match(html).Groups["value"].Value);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = uniqueId;

        profile = new TikTokProfileSnapshot(uniqueId, displayName, followerCount);
        return true;
    }

    private static string ReadJsonString(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return string.Empty;

        try
        {
            return WebUtility.HtmlDecode(JsonSerializer.Deserialize<string>($"\"{encoded}\"") ?? string.Empty).Trim();
        }
        catch (JsonException)
        {
            return WebUtility.HtmlDecode(Regex.Unescape(encoded)).Trim();
        }
    }

    private static string RenderTemplate(string? template, IReadOnlyDictionary<string, string> tokens)
    {
        string rendered = template ?? string.Empty;

        foreach (var token in tokens)
            rendered = rendered.Replace($"{{{token.Key}}}", token.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        rendered = rendered.Replace("\\n", "\n", StringComparison.Ordinal);
        rendered = MultiSpaceRegex.Replace(rendered, " ");
        return rendered.Trim();
    }

    private static string Truncate(string? value, int maxLength)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLength)
            return text;

        if (maxLength <= 3)
            return text.Substring(0, maxLength);

        return $"{text.Substring(0, maxLength - 3)}...";
    }

    private static string FormatCount(long value, bool compact)
    {
        if (!compact)
            return value.ToString(CultureInfo.InvariantCulture);

        if (value >= 1_000_000_000)
            return $"{value / 1_000_000_000d:0.#}B";
        if (value >= 1_000_000)
            return $"{value / 1_000_000d:0.#}M";
        if (value >= 1_000)
            return $"{value / 1_000d:0.#}K";

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string SummarizeExceptionMessage(Exception exception)
    {
        string message = exception.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
            return "TikTok Live connection failed.";

        return Truncate(message.Replace(Environment.NewLine, " ", StringComparison.Ordinal), 120);
    }

    private readonly record struct TikTokProfileSnapshot(string UniqueId, string DisplayName, long FollowerCount);

    private sealed record UiSnapshot(
        bool IsRunning,
        bool IsConnecting,
        bool IsConnected,
        bool IsLive,
        bool IsProfileRefreshing,
        string ProfileStatusText,
        string ProfileDisplay,
        string FollowerCountDisplay,
        string ProfilePreview,
        string ProfileEventDisplay,
        string StatusText,
        string ErrorText,
        string HostDisplay,
        string RoomIdDisplay,
        string ViewerCountDisplay,
        string LikeCountDisplay,
        string LastEventDisplay,
        string SummaryPreview,
        string OutputPreview);
}
