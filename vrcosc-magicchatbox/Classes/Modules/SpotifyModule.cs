using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    private readonly ISettingsProvider<SpotifySettings> _settingsProvider;
    private readonly SpotifyDisplayState _display;
    private readonly MediaLinkDisplayState _mediaLinkDisplay;
    private readonly ISpotifyApiClient _apiClient;
    private readonly SpotifyOAuthHandler _oauth;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IUiDispatcher _dispatcher;
    private readonly IToastService _toast;

    private bool _refreshInProgress;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private DateTime _lastProfileRefreshUtc = DateTime.MinValue;

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
        _display.ClearPlayback("Spotify stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
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

        await _dispatcher.InvokeAsync(() =>
        {
            _display.IsConnected = false;
            _display.NeedsReconnect = false;
            _display.ProfileName = string.Empty;
            _display.ErrorText = string.Empty;
            _display.ClearPlayback("Not connected");
        });
    }

    public void TriggerRefreshIfNeeded(bool force = false)
    {
        if (!_integrationSettings.IntgrSpotify && !force)
            return;

        int intervalSeconds = _display.HasPlayback
            ? Math.Clamp(Settings.PollingIntervalSeconds, 2, 120)
            : Math.Clamp(Settings.IdlePollingIntervalSeconds, 5, 600);

        if (!force && (DateTime.UtcNow - _lastRefreshUtc).TotalSeconds < intervalSeconds)
            return;

        _ = RefreshAsync();
    }

    public Task TriggerManualRefreshAsync() => RefreshAsync(force: true);

    public async Task TogglePlayPauseAsync()
    {
        if (_display.IsPlaying)
            await ExecuteControlAsync(token => _apiClient.PauseAsync(token));
        else
            await ExecuteControlAsync(token => _apiClient.PlayAsync(token));
    }

    public Task NextAsync() => ExecuteControlAsync(token => _apiClient.NextAsync(token));

    public Task PreviousAsync() => ExecuteControlAsync(token => _apiClient.PreviousAsync(token));

    public Task ToggleLikeAsync()
    {
        string trackId = _display.TrackId;
        if (string.IsNullOrWhiteSpace(trackId))
            return Task.CompletedTask;

        return ExecuteControlAsync(token => _display.IsLiked
            ? _apiClient.RemoveTrackAsync(token, trackId)
            : _apiClient.SaveTrackAsync(token, trackId));
    }

    public Task ToggleShuffleAsync()
        => ExecuteControlAsync(token => _apiClient.SetShuffleAsync(token, !_display.IsShuffleOn));

    public Task CycleRepeatAsync()
    {
        string next = _display.RepeatState switch
        {
            "off" => "context",
            "context" => "track",
            _ => "off"
        };
        return ExecuteControlAsync(token => _apiClient.SetRepeatAsync(token, next));
    }

    public Task SetVolumeAsync(int volumePercent)
        => ExecuteControlAsync(token => _apiClient.SetVolumeAsync(token, volumePercent));

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
        if (_refreshInProgress && !force)
            return;

        _refreshInProgress = true;
        try
        {
            _lastRefreshUtc = DateTime.UtcNow;
            string? token = await EnsureAccessTokenAsync().ConfigureAwait(false);
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
                await RefreshProfileAsync(token).ConfigureAwait(false);

            var playback = await _apiClient.GetPlaybackAsync(token).ConfigureAwait(false);
            if (!playback.Success)
            {
                await HandleApiFailureAsync(playback.Unauthorized, playback.Message).ConfigureAwait(false);
                return;
            }

            bool liked = false;
            if (!string.IsNullOrWhiteSpace(playback.Value?.Track?.Id))
            {
                var likedResult = await _apiClient.IsTrackSavedAsync(token, playback.Value.Track.Id).ConfigureAwait(false);
                liked = likedResult.Success && likedResult.Value;
            }

            string queuePreview = string.Empty;
            if (Settings.PartyModeEnabled)
            {
                var queue = await _apiClient.GetQueueAsync(token).ConfigureAwait(false);
                if (queue.Success && queue.Value?.UpcomingTracks.Count > 0)
                    queuePreview = "Next: " + string.Join(" / ", queue.Value.UpcomingTracks);
            }

            await ApplyPlaybackAsync(playback.Value!, liked, queuePreview).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            await _dispatcher.InvokeAsync(() =>
            {
                _display.ErrorText = ex.Message;
                _display.StatusText = "Spotify refresh failed";
            });
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task RefreshProfileAsync(string accessToken)
    {
        var profile = await _apiClient.GetProfileAsync(accessToken).ConfigureAwait(false);
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
            _display.VolumePercent = playback.VolumePercent;
            _display.ProgressMs = playback.ProgressMs;
            _display.DurationMs = playback.Track.DurationMs;
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

    private async Task ExecuteControlAsync(Func<string, Task<SpotifyApiResult<bool>>> action)
    {
        string? token = await EnsureAccessTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            _toast.Show("Spotify", "Connect Spotify before using controls.", ToastType.Warning, key: "spotify-no-token");
            return;
        }

        var result = await action(token).ConfigureAwait(false);
        if (!result.Success)
        {
            _toast.Show("Spotify control", result.Message, ToastType.Warning, key: "spotify-control-failed");
            await HandleApiFailureAsync(result.Unauthorized, result.Message).ConfigureAwait(false);
            return;
        }

        await RefreshAsync(force: true).ConfigureAwait(false);
    }

    private async Task<string?> EnsureAccessTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.AccessToken) && string.IsNullOrWhiteSpace(Settings.RefreshToken))
            return null;

        if (!string.IsNullOrWhiteSpace(Settings.AccessToken)
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

        var token = await _oauth.RefreshTokenAsync(Settings.ClientId, Settings.RefreshToken).ConfigureAwait(false);
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

        return Settings.AccessToken;
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
            _display.ProgressMs,
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
            : TimeSpan.FromMilliseconds(Math.Max(0, _display.ProgressMs));
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
