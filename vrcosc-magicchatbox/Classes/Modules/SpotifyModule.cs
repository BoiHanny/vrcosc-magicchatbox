using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules.Spotify;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Runtime coordinator for Spotify Web API polling, token refresh, and playback controls.
/// </summary>
public sealed partial class SpotifyModule : ObservableObject, IModule
{
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DefaultRateLimitBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRateLimitBackoff = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LiveProgressTickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan EndOfTrackRefreshWindow = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan QueueRefreshInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan TokenRefreshLockTimeout = TimeSpan.FromSeconds(15);
    private const int StalePlaybackFailureThreshold = 3;

    private readonly ISettingsProvider<SpotifySettings> _settingsProvider;
    private readonly SpotifyDisplayState _display;
    private readonly MediaLinkDisplayState _mediaLinkDisplay;
    private readonly ISpotifyApiClient _apiClient;
    private readonly SpotifyOAuthHandler _oauth;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IUiDispatcher _dispatcher;
    private readonly IToastService _toast;

    // Single-flight refresh-token gate. Concurrent callers wait for the in-flight
    // refresh and reuse its result, avoiding parallel refresh-token rotation
    // (which can invalidate refresh tokens on Spotify's side).
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

    // Cache: IsTrackSaved by TrackId. Invalidated when the track changes or
    // when the user toggles like via ToggleLikeAsync.
    private string _likedCacheTrackId = string.Empty;
    private bool _likedCacheValue;
    private bool _likedCacheValid;

    // Queue refresh throttle — independent of playback polling so heavy
    // queue fetches don't run every poll when party/{queue} is enabled.
    private DateTime _lastQueueRefreshUtc = DateTime.MinValue;
    private string _lastQueuePreview = string.Empty;

    private int _refreshInProgress;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private DateTime _lastProfileRefreshUtc = DateTime.MinValue;
    private DateTime _nextApiCallAllowedUtc = DateTime.MinValue;
    private int _consecutiveRefreshFailures;
    private Timer? _progressTimer;
    private int _progressTickQueued;
    private static readonly TimeSpan ForcedRefreshWaitTimeout = TimeSpan.FromSeconds(3);

    public SpotifySettings Settings => _settingsProvider.Value;
    public SpotifyDisplayState Display => _display;

    public string Name => "Spotify";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _integrationSettings.IntgrSpotify && _display.IsConnected;

    public SpotifyModule(
        ISettingsProvider<SpotifySettings> settingsProvider,
        SpotifyDisplayState display,
        MediaLinkDisplayState mediaLinkDisplay,
        ISpotifyApiClient apiClient,
        SpotifyOAuthHandler oauth,
        IntegrationSettings integrationSettings,
        IUiDispatcher dispatcher,
        IToastService toast)
    {
        _settingsProvider = settingsProvider;
        _display = display;
        _mediaLinkDisplay = mediaLinkDisplay;
        _apiClient = apiClient;
        _oauth = oauth;
        _integrationSettings = integrationSettings;
        _dispatcher = dispatcher;
        _toast = toast;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default)
    {
        StartLiveProgressTimer();

        if (Settings.HasSavedToken)
        {
            _display.IsConnected = true;
            _display.StatusText = "Spotify ready";
            TriggerRefreshIfNeeded(force: true);
        }
        else
        {
            _display.IsConnected = false;
            _display.StatusText = "Connect Spotify";
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        StopLiveProgressTimer();
        _display.ClearPlayback("Spotify stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopLiveProgressTimer();
        _tokenRefreshLock.Dispose();
    }

    public void SaveSettings() => _settingsProvider.Save();

    public void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IntegrationSettings.IntgrSpotify))
            return;

        if (_integrationSettings.IntgrSpotify)
            TriggerRefreshIfNeeded(force: true);
        else
            _display.ClearPlayback("Spotify disabled");
    }

    public Task<SpotifyTokenResult?> AuthenticateAsync()
        => _oauth.AuthenticateAsync(Settings.ClientId);

    public async Task ApplyTokenResultAsync(SpotifyTokenResult token)
    {
        Settings.AccessToken = token.AccessToken;
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            Settings.RefreshToken = token.RefreshToken;
        if (token.ExpiresIn > 0)
            Settings.TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

        Settings.PrivacyChoicesCompleted = true;
        _settingsProvider.Save();
        _nextApiCallAllowedUtc = DateTime.MinValue;
        Interlocked.Exchange(ref _consecutiveRefreshFailures, 0);

        await _dispatcher.InvokeAsync(() =>
        {
            _display.IsConnected = true;
            _display.NeedsReconnect = false;
            _display.ErrorText = string.Empty;
            _display.StatusText = "Spotify connected";
        });

        TriggerRefreshIfNeeded(force: true);
    }

    public async Task DisconnectAsync()
    {
        Settings.AccessToken = string.Empty;
        Settings.RefreshToken = string.Empty;
        Settings.AccessTokenEncrypted = string.Empty;
        Settings.RefreshTokenEncrypted = string.Empty;
        Settings.TokenExpiresAtUtcTicks = 0;
        _settingsProvider.Save();
        _nextApiCallAllowedUtc = DateTime.MinValue;
        Interlocked.Exchange(ref _consecutiveRefreshFailures, 0);
        InvalidateLikedCache();
        _lastQueueRefreshUtc = DateTime.MinValue;
        _lastQueuePreview = string.Empty;

        await _dispatcher.InvokeAsync(() =>
        {
            _display.IsConnected = false;
            _display.NeedsReconnect = false;
            _display.ProfileName = string.Empty;
            _display.ErrorText = string.Empty;
            _display.ClearPlayback("Not connected");
        });
    }

    private async Task HandleSpotifyResultFailureAsync<T>(SpotifyApiResult<T> result)
    {
        if (result.RateLimited)
            await ApplyRateLimitAsync(result).ConfigureAwait(false);

        if (result.Transient || result.RateLimited)
        {
            await HandleTransientFailureAsync(result.Message).ConfigureAwait(false);
            return;
        }

        await HandleApiFailureAsync(result.Unauthorized, result.Message).ConfigureAwait(false);
        if (result.Unauthorized)
            _nextApiCallAllowedUtc = DateTime.UtcNow.AddMinutes(5);
    }

    private async Task HandleTransientFailureAsync(string message)
    {
        int failures = Interlocked.Increment(ref _consecutiveRefreshFailures);
        await _dispatcher.InvokeAsync(() =>
        {
            _display.ErrorText = message;
            _display.StatusText = failures >= StalePlaybackFailureThreshold
                ? "Spotify sync paused"
                : "Spotify sync retrying";

            if (failures >= StalePlaybackFailureThreshold)
            {
                _display.ClearPlayback("Spotify sync paused");
                _display.OutputPreview = BuildOutputString(useSample: true);
            }
        });
    }

    private async Task ApplyRateLimitAsync<T>(SpotifyApiResult<T> result)
    {
        TimeSpan delay = result.RetryAfter.GetValueOrDefault(DefaultRateLimitBackoff);
        if (delay <= TimeSpan.Zero)
            delay = DefaultRateLimitBackoff;
        if (delay > MaxRateLimitBackoff)
            delay = MaxRateLimitBackoff;

        _nextApiCallAllowedUtc = DateTime.UtcNow.Add(delay);
        await _dispatcher.InvokeAsync(() =>
        {
            _display.ErrorText = $"Spotify rate limited MagicChatbox. Retrying in {delay.TotalSeconds:0}s.";
            _display.StatusText = "Spotify rate limited";
        });
    }

    private bool IsApiBackoffActive(out TimeSpan wait)
    {
        wait = _nextApiCallAllowedUtc - DateTime.UtcNow;
        return wait > TimeSpan.Zero;
    }

    private bool ShouldFetchQueue()
        => Settings.PartyModeEnabled
           && (Settings.OutputTemplate.Contains("{queue}", StringComparison.OrdinalIgnoreCase)
               || Settings.PartyTemplate.Contains("{queue}", StringComparison.OrdinalIgnoreCase));

    private bool ShouldRefreshNearTrackEnd()
        => _display.HasPlayback
           && _display.IsPlaying
           && _display.DurationMs > 0
           && _display.DurationMs - _display.LiveProgressMs <= EndOfTrackRefreshWindow.TotalMilliseconds;

    private void StartLiveProgressTimer()
    {
        if (_progressTimer != null)
            return;

        _progressTimer = new Timer(
            _ => QueueLiveProgressTick(),
            null,
            LiveProgressTickInterval,
            LiveProgressTickInterval);
    }

    private void StopLiveProgressTimer()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
        Interlocked.Exchange(ref _progressTickQueued, 0);
    }

    private void QueueLiveProgressTick()
    {
        if (!_display.HasPlayback || !_display.IsPlaying)
            return;

        if (Interlocked.CompareExchange(ref _progressTickQueued, 1, 0) == 1)
            return;

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (_display.HasPlayback && _display.IsPlaying)
                {
                    _display.NotifyProgressDisplayChanged();
                    _display.OutputPreview = BuildOutputString();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                Interlocked.Exchange(ref _progressTickQueued, 0);
            }
        });
    }

    public void TriggerRefreshIfNeeded(bool force = false)
    {
        if (!_integrationSettings.IntgrSpotify && !force)
            return;

        int intervalSeconds = _display.HasPlayback
            ? Math.Clamp(Settings.PollingIntervalSeconds, 2, 120)
            : Math.Clamp(Settings.IdlePollingIntervalSeconds, 5, 600);
        if (!force && ShouldRefreshNearTrackEnd())
            intervalSeconds = 1;

        if (!force && (DateTime.UtcNow - _lastRefreshUtc).TotalSeconds < intervalSeconds)
            return;

        _lastRefreshUtc = DateTime.UtcNow;
        _ = RefreshAsync(force);
    }

    public Task TriggerManualRefreshAsync() => RefreshAsync(force: true);

    public async Task TogglePlayPauseAsync()
    {
        if (_display.IsPlaying)
            await ExecuteControlAsync((token, ct) => _apiClient.PauseAsync(token, ct));
        else
            await ExecuteControlAsync((token, ct) => _apiClient.PlayAsync(token, ct));
    }

    public Task NextAsync() => ExecuteControlAsync((token, ct) => _apiClient.NextAsync(token, ct));

    public Task PreviousAsync() => ExecuteControlAsync((token, ct) => _apiClient.PreviousAsync(token, ct));

    public Task ToggleLikeAsync()
    {
        string trackId = _display.TrackId;
        if (string.IsNullOrWhiteSpace(trackId))
            return Task.CompletedTask;

        // Invalidate the liked cache so the next refresh re-queries authoritative state.
        InvalidateLikedCache();

        return ExecuteControlAsync((token, ct) => _display.IsLiked
            ? _apiClient.RemoveTrackAsync(token, trackId, ct)
            : _apiClient.SaveTrackAsync(token, trackId, ct));
    }

    public Task ToggleShuffleAsync()
        => ExecuteControlAsync((token, ct) => _apiClient.SetShuffleAsync(token, !_display.IsShuffleOn, ct));

    public Task CycleRepeatAsync()
    {
        string next = _display.RepeatState switch
        {
            "off" => "context",
            "context" => "track",
            _ => "off"
        };
        return ExecuteControlAsync((token, ct) => _apiClient.SetRepeatAsync(token, next, ct));
    }

    public Task SetVolumeAsync(int volumePercent)
        => ExecuteControlAsync((token, ct) => _apiClient.SetVolumeAsync(token, volumePercent, ct));

    public string BuildOutputString(OscBuildContext? context = null, bool useSample = false)
    {
        var values = useSample ? BuildSampleValues() : BuildCurrentValues();
        string template = Settings.PartyModeEnabled && !string.IsNullOrWhiteSpace(values["queue"])
            ? Settings.PartyTemplate
            : Settings.OutputTemplate;

        string text = ApplyTemplate(template, values);
        if (context == null || context.WouldFit(text))
            return text;

        if (Settings.AutoDowngradeProgress && TryGetProgressTimes(useSample, out TimeSpan current, out TimeSpan full))
        {
            foreach (var mode in ProgressFallbackModes(Settings.ProgressDisplayMode))
            {
                ApplyProgressTokens(values, current, full, mode);
                text = ApplyTemplate(template, values);
                if (context.WouldFit(text))
                    return text;
            }
        }

        foreach (string token in TrimOrder())
        {
            values[token] = string.Empty;
            text = ApplyTemplate(template, values);
            if (context.WouldFit(text))
                return text;
        }

        return text;
    }

    private async Task RefreshAsync(bool force = false)
    {
        if (IsApiBackoffActive(out var wait))
        {
            await _dispatcher.InvokeAsync(() =>
            {
                _display.ErrorText = $"Spotify rate limited MagicChatbox. Retrying in {wait.TotalSeconds:0}s.";
                _display.StatusText = "Spotify rate limited";
            });
            return;
        }

        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) == 1)
        {
            if (!force)
                return;

            var waitUntil = DateTime.UtcNow + ForcedRefreshWaitTimeout;
            while (Volatile.Read(ref _refreshInProgress) == 1 && DateTime.UtcNow < waitUntil)
                await Task.Delay(100).ConfigureAwait(false);

            if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) == 1)
                return;
        }

        using var timeout = new CancellationTokenSource(RefreshTimeout);
        try
        {
            string? token = await EnsureAccessTokenAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _display.IsConnected = false;
                    _display.ClearPlayback("Connect Spotify");
                    _display.OutputPreview = BuildOutputString(useSample: true);
                });
                return;
            }

            if ((DateTime.UtcNow - _lastProfileRefreshUtc) > TimeSpan.FromMinutes(10) || string.IsNullOrWhiteSpace(_display.ProfileName))
                await RefreshProfileAsync(token, timeout.Token).ConfigureAwait(false);

            var playback = await _apiClient.GetPlaybackAsync(token, timeout.Token).ConfigureAwait(false);
            if (!playback.Success && playback.Unauthorized)
            {
                token = await EnsureAccessTokenAsync(forceRefresh: true, cancellationToken: timeout.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                    playback = await _apiClient.GetPlaybackAsync(token, timeout.Token).ConfigureAwait(false);
            }

            if (!playback.Success)
            {
                await HandleSpotifyResultFailureAsync(playback).ConfigureAwait(false);
                return;
            }

            bool liked = false;
            string? trackId = playback.Value?.Track?.Id;
            if (Settings.ShowLiked && !string.IsNullOrWhiteSpace(trackId))
            {
                if (TryGetCachedLiked(trackId!, out bool cached))
                {
                    liked = cached;
                }
                else
                {
                    var likedResult = await _apiClient.IsTrackSavedAsync(token, trackId!, timeout.Token).ConfigureAwait(false);
                    if (likedResult.Success)
                    {
                        liked = likedResult.Value;
                        SetCachedLiked(trackId!, liked);
                    }
                    else if (likedResult.RateLimited)
                    {
                        await ApplyRateLimitAsync(likedResult).ConfigureAwait(false);
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(trackId))
            {
                // Track gone — drop the cache so re-acquiring the same id later re-queries.
                InvalidateLikedCache();
            }

            string queuePreview = _lastQueuePreview;
            if (ShouldFetchQueue())
            {
                bool throttled = (DateTime.UtcNow - _lastQueueRefreshUtc) < QueueRefreshInterval;
                if (force || !throttled)
                {
                    var queue = await _apiClient.GetQueueAsync(token, timeout.Token).ConfigureAwait(false);
                    if (queue.Success)
                    {
                        _lastQueueRefreshUtc = DateTime.UtcNow;
                        queuePreview = queue.Value?.UpcomingTracks.Count > 0
                            ? "Next: " + string.Join(" / ", queue.Value.UpcomingTracks)
                            : string.Empty;
                        _lastQueuePreview = queuePreview;
                    }
                    else if (queue.RateLimited)
                    {
                        await ApplyRateLimitAsync(queue).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                queuePreview = string.Empty;
                _lastQueuePreview = string.Empty;
            }

            await ApplyPlaybackAsync(playback.Value!, liked, queuePreview).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            Logging.WriteException(ex, MSGBox: false);
            string message = ex is JsonException or InvalidOperationException
                ? "Spotify returned an unexpected response. Retrying shortly."
                : "Spotify refresh timed out.";
            await HandleTransientFailureAsync(message).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
        }
    }

    private async Task RefreshProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        var profile = await _apiClient.GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);
        if (!profile.Success || profile.Value == null)
            return;

        _lastProfileRefreshUtc = DateTime.UtcNow;
        await _dispatcher.InvokeAsync(() =>
        {
            _display.ProfileName = string.IsNullOrWhiteSpace(profile.Value.DisplayName)
                ? profile.Value.Id
                : profile.Value.DisplayName;
        });
    }

    private async Task ApplyPlaybackAsync(SpotifyPlaybackSnapshot playback, bool liked, string queuePreview)
    {
        Interlocked.Exchange(ref _consecutiveRefreshFailures, 0);
        _nextApiCallAllowedUtc = DateTime.MinValue;

        await _dispatcher.InvokeAsync(() =>
        {
            _display.IsConnected = true;
            _display.NeedsReconnect = false;
            _display.ErrorText = string.Empty;
            _display.LastSyncUtc = DateTime.UtcNow;
            _display.LastSyncDisplay = $"Last sync: {DateTime.Now:HH:mm:ss}";

            if (!playback.HasPlayback || playback.Track == null)
            {
                _display.ClearPlayback("No active Spotify playback");
                _display.IsConnected = true;
                _display.OutputPreview = BuildOutputString(useSample: true);
                return;
            }

            _display.HasPlayback = true;
            _display.IsPlaying = playback.IsPlaying;
            _display.IsShuffleOn = playback.ShuffleState;
            _display.RepeatState = playback.RepeatState;
            _display.DeviceName = playback.DeviceName;
            _display.HasVolume = playback.HasVolume;
            _display.VolumePercent = playback.VolumePercent;
            _display.DurationMs = playback.Track.DurationMs;
            _display.ProgressMs = _display.DurationMs > 0
                ? Math.Clamp(playback.ProgressMs, 0, _display.DurationMs)
                : Math.Max(0, playback.ProgressMs);
            _display.ProgressUpdatedUtc = playback.ProgressCapturedAtUtc;
            _display.TrackId = playback.Track.Id;
            _display.TrackUri = playback.Track.Uri;
            _display.ExternalUrl = playback.Track.ExternalUrl;
            _display.Title = playback.Track.Title;
            _display.Artist = playback.Track.Artist;
            _display.Album = playback.Track.Album;
            _display.IsExplicit = playback.Track.Explicit;
            _display.IsLiked = liked;
            _display.QueuePreview = queuePreview;
            _display.StatusText = playback.IsPlaying ? "Playing" : "Paused";
            _display.OutputPreview = BuildOutputString();
        });
    }

    private async Task HandleApiFailureAsync(bool unauthorized, string message)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            _display.ErrorText = message;
            _display.StatusText = unauthorized ? "Reconnect Spotify" : message;
            _display.NeedsReconnect = unauthorized;
            if (unauthorized)
                _display.IsConnected = false;
        });
    }

    private async Task ExecuteControlAsync(Func<string, CancellationToken, Task<SpotifyApiResult<bool>>> action)
    {
        using var timeout = new CancellationTokenSource(ControlTimeout);
        try
        {
            if (IsApiBackoffActive(out var wait))
            {
                _toast.Show("Spotify", $"Spotify rate limited controls. Try again in {wait.TotalSeconds:0}s.", ToastType.Warning, key: "spotify-control-rate-limited");
                return;
            }

            string? token = await EnsureAccessTokenAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                _toast.Show("Spotify", "Connect Spotify before using controls.", ToastType.Warning, key: "spotify-no-token");
                return;
            }

            var result = await action(token, timeout.Token).ConfigureAwait(false);
            if (!result.Success && result.Unauthorized)
            {
                token = await EnsureAccessTokenAsync(forceRefresh: true, cancellationToken: timeout.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                    result = await action(token, timeout.Token).ConfigureAwait(false);
            }

            if (!result.Success)
            {
                if (result.RateLimited)
                    await ApplyRateLimitAsync(result).ConfigureAwait(false);

                _toast.Show("Spotify control", result.Message, ToastType.Warning, key: "spotify-control-failed");
                await HandleApiFailureAsync(result.Unauthorized, result.Message).ConfigureAwait(false);
                return;
            }

            await RefreshAsync(force: true).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("Spotify control", "Spotify did not respond in time. Try again shortly.", ToastType.Warning, key: "spotify-control-timeout");
            await HandleTransientFailureAsync("Spotify control timed out.").ConfigureAwait(false);
        }
    }

    private async Task<string?> EnsureAccessTokenAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Settings.AccessToken) && string.IsNullOrWhiteSpace(Settings.RefreshToken))
            return null;

        // Fast path — current token is still valid.
        if (!forceRefresh
            && !string.IsNullOrWhiteSpace(Settings.AccessToken)
            && Settings.TokenExpiresAtUtc > DateTime.UtcNow.Add(TokenRefreshSkew))
            return Settings.AccessToken;

        if (string.IsNullOrWhiteSpace(Settings.RefreshToken))
        {
            await _dispatcher.InvokeAsync(() =>
            {
                _display.NeedsReconnect = true;
                _display.StatusText = "Reconnect Spotify";
            });
            return null;
        }

        // Single-flight gate: only one refresh-token rotation may be in flight.
        // Concurrent callers wait and then re-check the cached token before issuing
        // a duplicate refresh that could invalidate the rotated refresh token.
        bool acquired = false;
        try
        {
            acquired = await _tokenRefreshLock.WaitAsync(TokenRefreshLockTimeout, cancellationToken).ConfigureAwait(false);
            if (!acquired)
            {
                Logging.WriteInfo("Spotify token refresh timed out waiting for single-flight lock.");
                return string.IsNullOrWhiteSpace(Settings.AccessToken) ? null : Settings.AccessToken;
            }

            // Re-check after acquiring the lock — another caller may have just refreshed.
            if (!forceRefresh
                && !string.IsNullOrWhiteSpace(Settings.AccessToken)
                && Settings.TokenExpiresAtUtc > DateTime.UtcNow.Add(TokenRefreshSkew))
                return Settings.AccessToken;

            if (string.IsNullOrWhiteSpace(Settings.RefreshToken))
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _display.NeedsReconnect = true;
                    _display.StatusText = "Reconnect Spotify";
                });
                return null;
            }

            var token = await _oauth.RefreshTokenAsync(Settings.ClientId, Settings.RefreshToken, cancellationToken).ConfigureAwait(false);
            if (token == null)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _display.NeedsReconnect = true;
                    _display.IsConnected = false;
                    _display.StatusText = "Reconnect Spotify";
                });
                return null;
            }

            Settings.AccessToken = token.AccessToken;
            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
                Settings.RefreshToken = token.RefreshToken;
            if (token.ExpiresIn > 0)
                Settings.TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            _settingsProvider.Save();
            _nextApiCallAllowedUtc = DateTime.MinValue;

            return Settings.AccessToken;
        }
        finally
        {
            if (acquired)
                _tokenRefreshLock.Release();
        }
    }

    private bool TryGetCachedLiked(string trackId, out bool liked)
    {
        if (_likedCacheValid && string.Equals(_likedCacheTrackId, trackId, StringComparison.Ordinal))
        {
            liked = _likedCacheValue;
            return true;
        }
        liked = false;
        return false;
    }

    private void SetCachedLiked(string trackId, bool liked)
    {
        _likedCacheTrackId = trackId;
        _likedCacheValue = liked;
        _likedCacheValid = true;
    }

    private void InvalidateLikedCache()
    {
        _likedCacheTrackId = string.Empty;
        _likedCacheValue = false;
        _likedCacheValid = false;
    }

    private Dictionary<string, string> BuildCurrentValues()
    {
        if (!_display.IsConnected)
            return BuildStateValues(Settings.DisconnectedText);

        if (!_display.HasPlayback)
            return BuildStateValues(Settings.EmptyText);

        if (!_display.IsPlaying && Settings.PauseOutputMode == SpotifyPauseOutputMode.Hide)
            return BuildStateValues(string.Empty);

        if (!_display.IsPlaying && Settings.PauseOutputMode == SpotifyPauseOutputMode.PauseText)
            return BuildStateValues(Settings.PausedText);

        return BuildTrackValues(
            _display.Title,
            _display.Artist,
            _display.Album,
            _display.DeviceName,
            _display.HasVolume,
            _display.VolumePercent,
            _display.LiveProgressMs,
            _display.DurationMs,
            _display.QueuePreview,
            _display.IsPlaying,
            _display.IsExplicit,
            _display.IsLiked,
            _display.IsShuffleOn,
            _display.RepeatState);
    }

    private Dictionary<string, string> BuildSampleValues()
        => BuildTrackValues(
            "Starlight",
            "Magic DJ",
            "VR Nights",
            "Quest Headset",
            true,
            57,
            83000,
            225000,
            "Next: Neon Dreams / Moon Loop",
            true,
            true,
            true,
            true,
            "context");

    private Dictionary<string, string> BuildTrackValues(
        string title,
        string artist,
        string album,
        string device,
        bool hasVolume,
        int volumePercent,
        int progressMs,
        int durationMs,
        string queue,
        bool isPlaying,
        bool isExplicit,
        bool isLiked,
        bool shuffle,
        string repeat)
    {
        bool hideText = Settings.PrivacyMode;
        string hidden = Settings.PrivacyHiddenText;
        var current = TimeSpan.FromMilliseconds(Math.Max(0, progressMs));
        var full = TimeSpan.FromMilliseconds(Math.Max(0, durationMs));
        var remaining = full > current ? full - current : TimeSpan.Zero;
        string percent = full > TimeSpan.Zero
            ? $"{Math.Clamp(current.TotalMilliseconds / full.TotalMilliseconds * 100d, 0d, 100d):0}%"
            : string.Empty;
        string progress = BuildProgressToken(current, full, Settings.ProgressDisplayMode);
        string seekbar = Settings.ProgressDisplayMode == SpotifyProgressDisplayMode.None
            ? string.Empty
            : BuildSeekbarToken(current, full);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["play_icon"] = Settings.AllowPlaybackStateInOutput ? (isPlaying ? Settings.IconPlaying : Settings.IconPaused) : string.Empty,
            ["title"] = Settings.ShowTitle && Settings.AllowTrackTitleInOutput ? (hideText ? hidden : title) : string.Empty,
            ["artist"] = Settings.ShowArtist && Settings.AllowArtistInOutput ? (hideText ? hidden : artist) : string.Empty,
            ["album"] = Settings.ShowAlbum && Settings.AllowAlbumInOutput ? (hideText ? hidden : album) : string.Empty,
            ["device"] = Settings.ShowDevice && Settings.AllowDeviceInOutput ? (hideText ? hidden : device) : string.Empty,
            ["volume"] = Settings.ShowVolume && Settings.AllowVolumeInOutput && hasVolume ? $"{Math.Clamp(volumePercent, 0, 100)}%" : string.Empty,
            ["progress"] = Settings.ShowProgress ? progress : string.Empty,
            ["seekbar"] = Settings.ShowProgress ? seekbar : string.Empty,
            ["elapsed"] = Settings.ShowProgress ? SeekbarUtilities.FormatTimeSpan(current) : string.Empty,
            ["duration"] = Settings.ShowProgress ? SeekbarUtilities.FormatTimeSpan(full) : string.Empty,
            ["remaining"] = Settings.ShowProgress ? SeekbarUtilities.FormatTimeSpan(remaining) : string.Empty,
            ["percent"] = Settings.ShowProgress ? percent : string.Empty,
            ["explicit_icon"] = Settings.ShowExplicit && isExplicit ? Settings.IconExplicit : string.Empty,
            ["liked_icon"] = Settings.ShowLiked ? (isLiked ? Settings.IconLiked : Settings.IconUnliked) : string.Empty,
            ["shuffle_icon"] = Settings.ShowShuffle ? (shuffle ? Settings.IconShuffleOn : Settings.IconShuffleOff) : string.Empty,
            ["repeat_icon"] = Settings.ShowRepeat ? ResolveRepeatIcon(repeat) : string.Empty,
            ["queue"] = Settings.PartyModeEnabled ? queue : string.Empty,
            ["separator"] = Settings.Separator
        };
    }

    private static Dictionary<string, string> BuildStateValues(string status)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["play_icon"] = string.Empty,
            ["title"] = status,
            ["artist"] = string.Empty,
            ["album"] = string.Empty,
            ["device"] = string.Empty,
            ["volume"] = string.Empty,
            ["progress"] = string.Empty,
            ["seekbar"] = string.Empty,
            ["elapsed"] = string.Empty,
            ["duration"] = string.Empty,
            ["remaining"] = string.Empty,
            ["percent"] = string.Empty,
            ["explicit_icon"] = string.Empty,
            ["liked_icon"] = string.Empty,
            ["shuffle_icon"] = string.Empty,
            ["repeat_icon"] = string.Empty,
            ["queue"] = string.Empty,
            ["separator"] = " "
        };

    private string ResolveRepeatIcon(string repeat) => repeat switch
    {
        "context" => Settings.IconRepeatContext,
        "track" => Settings.IconRepeatTrack,
        _ => Settings.IconRepeatOff
    };

    private bool TryGetProgressTimes(bool useSample, out TimeSpan current, out TimeSpan full)
    {
        if (!useSample && !_display.HasPlayback)
        {
            current = TimeSpan.Zero;
            full = TimeSpan.Zero;
            return false;
        }

        current = useSample
            ? TimeSpan.FromMilliseconds(83000)
            : TimeSpan.FromMilliseconds(Math.Max(0, _display.LiveProgressMs));
        full = useSample
            ? TimeSpan.FromMilliseconds(225000)
            : TimeSpan.FromMilliseconds(Math.Max(0, _display.DurationMs));

        return full > TimeSpan.Zero;
    }

    private void ApplyProgressTokens(
        IDictionary<string, string> values,
        TimeSpan current,
        TimeSpan full,
        SpotifyProgressDisplayMode mode)
    {
        if (!Settings.ShowProgress)
        {
            values["progress"] = string.Empty;
            values["seekbar"] = string.Empty;
            return;
        }

        string fallback = BuildProgressToken(current, full, mode);
        values["progress"] = fallback;
        values["seekbar"] = mode == SpotifyProgressDisplayMode.Seekbar
            ? BuildSeekbarToken(current, full)
            : fallback;
    }

    private IEnumerable<SpotifyProgressDisplayMode> ProgressFallbackModes(SpotifyProgressDisplayMode mode)
    {
        if (mode == SpotifyProgressDisplayMode.Seekbar)
            yield return SpotifyProgressDisplayMode.SmallNumbers;
        if (mode == SpotifyProgressDisplayMode.Text)
            yield return SpotifyProgressDisplayMode.SmallNumbers;
        if (mode != SpotifyProgressDisplayMode.None)
            yield return SpotifyProgressDisplayMode.None;
    }

    private string BuildProgressToken(TimeSpan current, TimeSpan full, SpotifyProgressDisplayMode mode)
    {
        if (full <= TimeSpan.Zero)
            return string.Empty;

        return mode switch
        {
            SpotifyProgressDisplayMode.None => string.Empty,
            SpotifyProgressDisplayMode.Seekbar => BuildSeekbarToken(current, full),
            SpotifyProgressDisplayMode.SmallNumbers => SeekbarUtilities.CreateSmallNumbers(current, full),
            _ => $"{SeekbarUtilities.FormatTimeSpan(current)} / {SeekbarUtilities.FormatTimeSpan(full)}"
        };
    }

    private string BuildSeekbarToken(TimeSpan current, TimeSpan full)
    {
        if (full <= TimeSpan.Zero)
            return string.Empty;

        double pct = Math.Clamp(current.TotalMilliseconds / full.TotalMilliseconds * 100d, 0d, 100d);
        return SeekbarUtilities.CreateProgressBar(pct, current, full, BuildSeekbarStyleOptions());
    }

    private SeekbarStyleOptions BuildSeekbarStyleOptions()
    {
        var style = ResolveSeekbarStyle();
        if (style != null)
        {
            return new SeekbarStyleOptions
            {
                DisplayTime = style.DisplayTime,
                FilledCharacter = style.FilledCharacter,
                MiddleCharacter = style.MiddleCharacter,
                NonFilledCharacter = style.NonFilledCharacter,
                ProgressBarLength = style.ProgressBarLength,
                ShowTimeInSuperscript = style.ShowTimeInSuperscript,
                SpaceAgainObjects = style.SpaceAgainObjects,
                SpaceBetweenPreSuffixAndTime = style.SpaceBetweenPreSuffixAndTime,
                TimePrefix = style.TimePrefix,
                TimePreSuffixOnTheInside = style.TimePreSuffixOnTheInside,
                TimeSuffix = style.TimeSuffix
            };
        }

        return new SeekbarStyleOptions
        {
            DisplayTime = Settings.ProgressShowTime,
            FilledCharacter = Settings.ProgressFilledCharacter,
            MiddleCharacter = Settings.ProgressMiddleCharacter,
            NonFilledCharacter = Settings.ProgressNonFilledCharacter,
            ProgressBarLength = Settings.ProgressBarLength,
            ShowTimeInSuperscript = Settings.ProgressShowTimeInSuperscript,
            SpaceAgainObjects = Settings.ProgressSpaceAroundObjects,
            SpaceBetweenPreSuffixAndTime = Settings.ProgressSpaceBetweenPreSuffixAndTime,
            TimePrefix = Settings.ProgressTimePrefix,
            TimePreSuffixOnTheInside = Settings.ProgressTimePreSuffixOnTheInside,
            TimeSuffix = Settings.ProgressTimeSuffix
        };
    }

    /// <summary>Resolve seekbar style by Spotify-specific ID, thread-safe snapshot.</summary>
    private MediaLinkModule.MediaLinkStyle? ResolveSeekbarStyle()
    {
        var styles = _mediaLinkDisplay.MediaLinkSeekbarStyles;
        if (styles == null || styles.Count == 0)
            return null;

        int targetId = Settings.SelectedSeekbarStyleId;
        try
        {
            foreach (var s in styles)
            {
                if (s.ID == targetId)
                    return s;
            }
        }
        catch
        {
            // Collection may change during enumeration from UI thread; fall through to fallback
        }

        return null;
    }

    private static IEnumerable<string> TrimOrder()
    {
        yield return "queue";
        yield return "volume";
        yield return "device";
        yield return "album";
        yield return "seekbar";
        yield return "progress";
        yield return "remaining";
        yield return "elapsed";
        yield return "duration";
        yield return "percent";
        yield return "liked_icon";
        yield return "explicit_icon";
        yield return "shuffle_icon";
        yield return "repeat_icon";
        yield return "artist";
    }

    private static string ApplyTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        string text = (template ?? string.Empty).Replace("\\n", "\n");
        foreach (var pair in values)
            text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);

        return string.Join("\n", text.Split('\n').Select(line => line.Trim())).Trim();
    }
}
