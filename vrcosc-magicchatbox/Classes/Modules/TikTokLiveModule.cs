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
    private static readonly Regex TikTokUserNameRegex = new("^[A-Za-z0-9._]{2,24だS}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                _connectionLoopTask = RunConnectionLoopAsync(sessionId, sessionCts.Token);

                lock (_stateLock)
                {
                    _sessionCts = sessionCts;
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

        _ = QueueProfileRefreshInternalAsync(nextCts);
    }

    private async Task QueueProfileRefreshInternalAsync(CancellationTokenSource nextCts)
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

        }
    }
}