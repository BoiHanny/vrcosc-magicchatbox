using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules.Twitch;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Module that polls the Twitch API for live stream status, viewer count, game, and channel info,
/// then formats the data for display in VRChat chat.
/// </summary>
public sealed partial class TwitchModule : ObservableObject, IModule
{
    private const int MinimumRefreshSeconds = 15;
    private const int MaximumRefreshSeconds = 3600;
    private static readonly TimeSpan TokenValidationInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FollowerRefreshInterval = TimeSpan.FromMinutes(2);

    private readonly TimeSettings _ts;
    private TimeSettings TS => _ts;

    private readonly ITwitchApiClient _apiClient;
    private readonly ISettingsProvider<TwitchSettings> _settingsProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IToastService? _toast;
    private volatile bool _twitchErrorShown;

    private bool refreshInProgress;
    private DateTime lastRefreshUtc = DateTime.MinValue;
    private DateTime lastTokenValidationUtc = DateTime.MinValue;
    private DateTime lastFollowerRefreshUtc = DateTime.MinValue;
    private string lastValidatedAccessToken = string.Empty;
    private string cachedBroadcasterId = string.Empty;
    private string lastUsedChannelName = string.Empty;
    private string validatedUserId = string.Empty;

    public TwitchSettings Settings => _settingsProvider.Value;
    public void SaveSettings() => _settingsProvider.Save();

    public string Name => "Twitch";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _integrationSettings.IntgrTwitch;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }

    [ObservableProperty]
    private string outputString = string.Empty;

    [ObservableProperty]
    private bool isLive;

    [ObservableProperty]
    private int viewerCount;

    [ObservableProperty]
    private int followerCount;

    [ObservableProperty]
    private string gameName = string.Empty;

    [ObservableProperty]
    private string streamTitle = string.Empty;

    [ObservableProperty]
    private bool connected;

    [ObservableProperty]
    private string statusMessage = "Not connected";

    [ObservableProperty]
    private string lastSyncDisplay = "Last sync: Never";

    [ObservableProperty]
    private string announcementStatusMessage = string.Empty;

    [ObservableProperty]
    private string shoutoutStatusMessage = string.Empty;

    [RelayCommand]
    private async Task SendAnnouncement() => await ExecuteSendAnnouncementAsync();

    [RelayCommand]
    private async Task SendShoutout() => await ExecuteSendShoutoutAsync();

    public TwitchModule(
        ISettingsProvider<TwitchSettings> settingsProvider,
        TimeSettings timeSettings,
        ITwitchApiClient apiClient,
        IntegrationSettings integrationSettings,
        IUiDispatcher dispatcher,
        IToastService? toast = null)
    {
        _settingsProvider = settingsProvider;
        _ts = timeSettings;
        _apiClient = apiClient;
        _integrationSettings = integrationSettings;
        _dispatcher = dispatcher;
        _toast = toast;
    }

    private async Task ExecuteSendAnnouncementAsync()
    {
        try
        {
            if (!Settings.AnnouncementsEnabled)
            {
                AnnouncementStatusMessage = "Announcements are disabled.";
                return;
            }

            string message = Settings.AnnouncementMessage?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                AnnouncementStatusMessage = "Enter a message first.";
                return;
            }

            AnnouncementStatusMessage = "Sending announcement...";
            var result = await SendAnnouncementAsync(message, Settings.AnnouncementColor);
            AnnouncementStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            AnnouncementStatusMessage = $"Error: {ex.Message}";
            Logging.WriteInfo($"Error sending Twitch announcement: {ex.Message}");
        }
    }

    private async Task ExecuteSendShoutoutAsync()
    {
        try
        {
            if (!Settings.ShoutoutsEnabled)
            {
                ShoutoutStatusMessage = "Shoutouts are disabled.";
                return;
            }

            string target = Settings.ShoutoutTarget?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
            {
                ShoutoutStatusMessage = "Enter a channel name.";
                return;
            }

            ShoutoutStatusMessage = "Sending shoutout...";
            var result = await SendShoutoutAsync(
                target,
                Settings.ShoutoutAlsoAnnounce,
                Settings.ShoutoutAnnouncementTemplate,
                Settings.ShoutoutAnnouncementColor);
            ShoutoutStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            ShoutoutStatusMessage = $"Error: {ex.Message}";
            Logging.WriteInfo($"Error sending Twitch shoutout: {ex.Message}");
        }
    }

    public void TriggerRefreshIfNeeded()
    {
        if (!_integrationSettings.IntgrTwitch || !HasConfiguration())
        {
            UpdateConnectionState(false, "Missing Twitch settings");
            return;
        }

        int intervalSeconds = GetRefreshIntervalSeconds();
        if (refreshInProgress || (DateTime.UtcNow - lastRefreshUtc).TotalSeconds < intervalSeconds)
        {
            return;
        }

        _ = RefreshAsync();
    }

    public void TriggerManualRefresh()
    {
        if (!HasConfiguration())
        {
            UpdateConnectionState(false, "Missing Twitch settings");
            return;
        }

        _ = RefreshAsync();
    }

    public string GetOutputString()
    {
        string output = BuildOutputString();
        if (!string.Equals(OutputString, output, StringComparison.Ordinal))
        {
            OutputString = output;
        }

        return output;
    }

    private bool HasConfiguration()
    {
        return !string.IsNullOrWhiteSpace(Settings.ChannelName) &&
               !string.IsNullOrWhiteSpace(Settings.ClientId) &&
               !string.IsNullOrWhiteSpace(Settings.AccessToken);
    }

    private int GetRefreshIntervalSeconds()
    {
        int interval = Settings.UpdateIntervalSeconds;
        if (interval < MinimumRefreshSeconds)
        {
            interval = MinimumRefreshSeconds;
        }
        else if (interval > MaximumRefreshSeconds)
        {
            interval = MaximumRefreshSeconds;
        }

        return interval;
    }

    private void ConfigureApi()
    {
        string clientId = Settings.ClientId?.Trim() ?? string.Empty;
        string accessToken = Settings.AccessToken?.Trim() ?? string.Empty;
        _apiClient.Configure(clientId, accessToken);
    }

    private async Task RefreshAsync()
    {
        if (refreshInProgress)
        {
            return;
        }

        refreshInProgress = true;
        lastRefreshUtc = DateTime.UtcNow;

        try
        {
            ConfigureApi();
            string channelName = Settings.ChannelName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(channelName))
            {
                UpdateSnapshot(false, 0, string.Empty, string.Empty);
                UpdateConnectionState(false, "Missing Twitch settings");
                return;
            }

            if (!await EnsureValidTokenAsync().ConfigureAwait(false))
            {
                return;
            }

            if (!await EnsureBroadcasterIdAsync(channelName).ConfigureAwait(false))
            {
                return;
            }

            var snapshot = await _apiClient.GetStreamInfoAsync(cachedBroadcasterId).ConfigureAwait(false);

            if (!snapshot.IsLive)
            {
                UpdateSnapshot(false, 0, string.Empty, string.Empty);
                UpdateConnectionState(true, "Offline");
                UpdateLastSync(DateTime.UtcNow);
                await RefreshFollowerCountAsync().ConfigureAwait(false);
                return;
            }

            UpdateSnapshot(true, snapshot.ViewerCount, snapshot.GameName, snapshot.Title);
            UpdateConnectionState(true, "Live");
            UpdateLastSync(DateTime.UtcNow);
            await RefreshFollowerCountAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            if (IsUnauthorized(ex))
            {
                cachedBroadcasterId = string.Empty;
                lastValidatedAccessToken = string.Empty;
                UpdateConnectionState(false, "Token invalid");
            }
            else
            {
                UpdateConnectionState(false, "Refresh failed");
            }
        }
        finally
        {
            refreshInProgress = false;
        }
    }

    private void UpdateSnapshot(bool live, int viewers, string game, string title)
    {
        if (_dispatcher.CheckAccess())
        {
            ApplySnapshot(live, viewers, game, title);
            return;
        }

        _dispatcher.Invoke(() => ApplySnapshot(live, viewers, game, title));
    }

    private void ApplySnapshot(bool live, int viewers, string game, string title)
    {
        IsLive = live;
        ViewerCount = viewers;
        GameName = game ?? string.Empty;
        StreamTitle = title ?? string.Empty;
        GetOutputString();
    }

    private async Task<bool> EnsureBroadcasterIdAsync(string channelName)
    {
        if (!string.Equals(lastUsedChannelName, channelName, StringComparison.OrdinalIgnoreCase))
        {
            cachedBroadcasterId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(cachedBroadcasterId))
        {
            return true;
        }

        string broadcasterId = await _apiClient.GetBroadcasterIdAsync(channelName).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            UpdateSnapshot(false, 0, string.Empty, string.Empty);
            UpdateConnectionState(false, "Channel not found");
            return false;
        }

        cachedBroadcasterId = broadcasterId;
        lastUsedChannelName = channelName;
        return true;
    }

    private bool ShouldFetchFollowerCount()
    {
        if (Settings.ShowFollowerCount)
        {
            return true;
        }

        string template = Settings.Template;
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        return template.IndexOf("{followers", StringComparison.OrdinalIgnoreCase) >= 0 ||
               template.IndexOf("{follower", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private async Task RefreshFollowerCountAsync()
    {
        if (!ShouldFetchFollowerCount())
        {
            return;
        }

        if ((DateTime.UtcNow - lastFollowerRefreshUtc) < FollowerRefreshInterval)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cachedBroadcasterId))
        {
            return;
        }

        var result = await _apiClient.GetFollowerCountAsync(cachedBroadcasterId, validatedUserId).ConfigureAwait(false);
        if (result.Success)
        {
            lastFollowerRefreshUtc = DateTime.UtcNow;
            UpdateFollowerCount(result.Count);
            return;
        }

        if (result.Unauthorized)
        {
            cachedBroadcasterId = string.Empty;
            lastValidatedAccessToken = string.Empty;
            validatedUserId = string.Empty;
            UpdateConnectionState(false, "Token invalid");
        }
        else if (result.Forbidden)
        {
            Logging.WriteInfo("Twitch: Follower count requires moderator:read:followers scope.");
        }
    }

    private void UpdateFollowerCount(int count)
    {
        if (_dispatcher.CheckAccess())
        {
            ApplyFollowerCount(count);
            return;
        }

        _dispatcher.Invoke(() => ApplyFollowerCount(count));
    }

    private void ApplyFollowerCount(int count)
    {
        FollowerCount = count;
        GetOutputString();
    }

    public async Task<(bool Success, string Message)> SendAnnouncementAsync(string message, TwitchAnnouncementColor color)
    {
        var prep = await PrepareChatActionAsync().ConfigureAwait(false);
        if (!prep.Success)
        {
            return (false, prep.Message);
        }

        string colorValue = MapAnnouncementColor(color);
        var result = await _apiClient.SendAnnouncementAsync(prep.BroadcasterId, prep.ModeratorId, message, colorValue).ConfigureAwait(false);

        if (!result.Success && result.Message.Contains("Token invalid", StringComparison.OrdinalIgnoreCase))
        {
            InvalidateSession();
        }

        return (result.Success, result.Message);
    }

    public async Task<(bool Success, string Message)> SendShoutoutAsync(
        string targetLogin,
        bool alsoAnnounce,
        string announcementTemplate,
        TwitchAnnouncementColor announcementColor)
    {
        var prep = await PrepareChatActionAsync().ConfigureAwait(false);
        if (!prep.Success)
        {
            return (false, prep.Message);
        }

        string normalizedLogin = NormalizeLogin(targetLogin);
        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            return (false, "Enter a channel name.");
        }

        string targetId = await _apiClient.ResolveUserIdAsync(normalizedLogin).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return (false, "Channel not found.");
        }

        var shoutoutResult = await _apiClient.SendShoutoutAsync(prep.BroadcasterId, targetId, prep.ModeratorId).ConfigureAwait(false);

        if (!shoutoutResult.Success)
        {
            if (shoutoutResult.Message.Contains("Token invalid", StringComparison.OrdinalIgnoreCase))
                InvalidateSession();

            return (false, shoutoutResult.Message);
        }

        if (alsoAnnounce)
        {
            string builtAnnouncement = BuildShoutoutAnnouncement(announcementTemplate, normalizedLogin);
            if (!string.IsNullOrWhiteSpace(builtAnnouncement))
            {
                string colorValue = MapAnnouncementColor(announcementColor);
                var announcementResult = await _apiClient.SendAnnouncementAsync(
                    prep.BroadcasterId,
                    prep.ModeratorId,
                    builtAnnouncement,
                    colorValue).ConfigureAwait(false);
                if (!announcementResult.Success)
                {
                    return (true, $"Shoutout sent. Announcement failed: {announcementResult.Message}");
                }
            }
        }

        return (true, "Shoutout sent!");
    }

    private void InvalidateSession()
    {
        cachedBroadcasterId = string.Empty;
        lastValidatedAccessToken = string.Empty;
        validatedUserId = string.Empty;
    }

    private async Task<(bool Success, string Message, string BroadcasterId, string ModeratorId)> PrepareChatActionAsync()
    {
        if (!HasConfiguration())
        {
            return (false, "Missing Twitch settings.", string.Empty, string.Empty);
        }

        ConfigureApi();
        string channelName = Settings.ChannelName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return (false, "Missing channel name.", string.Empty, string.Empty);
        }

        if (!await EnsureValidTokenAsync().ConfigureAwait(false))
        {
            return (false, "Token invalid.", string.Empty, string.Empty);
        }

        if (!await EnsureBroadcasterIdAsync(channelName).ConfigureAwait(false))
        {
            return (false, "Channel not found.", string.Empty, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(validatedUserId))
        {
            return (false, "Unable to resolve user id.", string.Empty, string.Empty);
        }

        return (true, string.Empty, cachedBroadcasterId, validatedUserId);
    }

    private static string NormalizeLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return string.Empty;
        }

        return login.Trim().TrimStart('@');
    }

    private static string BuildShoutoutAnnouncement(string template, string login)
    {
        string normalized = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        string messageTemplate = string.IsNullOrWhiteSpace(template)
            ? "Go follow {user} at twitch.tv/{user}"
            : template;

        return messageTemplate
            .Replace("{user}", normalized)
            .Replace("{url}", $"twitch.tv/{normalized}");
    }

    private static string MapAnnouncementColor(TwitchAnnouncementColor color)
    {
        return color switch
        {
            TwitchAnnouncementColor.Blue => "blue",
            TwitchAnnouncementColor.Green => "green",
            TwitchAnnouncementColor.Orange => "orange",
            TwitchAnnouncementColor.Purple => "purple",
            _ => "primary"
        };
    }

    private string BuildOutputString()
    {
        if (!IsLive)
        {
            return Settings.OfflineMessage ?? string.Empty;
        }

        TwitchTokens tokens = BuildTokens();
        string template = NormalizeTemplate(Settings.Template);
        if (!string.IsNullOrWhiteSpace(template))
        {
            string templated = ApplyTemplate(template, tokens);
            return string.IsNullOrWhiteSpace(templated) ? string.Empty : templated;
        }

        var parts = new List<string>
        {
            tokens.Live,
            tokens.GameWithLabel,
            tokens.ViewersWithLabel,
            tokens.FollowersWithLabel,
            tokens.TitleWithLabel,
            tokens.ChannelWithLabel
        };

        string separator = NormalizeSeparator(Settings.Separator);
        if (string.IsNullOrWhiteSpace(separator))
        {
            separator = " | ";
        }

        return string.Join(separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string FormatLastSync(DateTime utc)
    {
        if (utc == DateTime.MinValue)
        {
            return "Last sync: Never";
        }

        DateTime local = utc.ToLocalTime();
        string format = TS.Time24H ? "HH:mm" : "h:mm tt";
        return $"Last sync: {local.ToString(format, CultureInfo.CurrentCulture)}";
    }

    private void UpdateLastSync(DateTime utc)
    {
        string display = FormatLastSync(utc);
        if (_dispatcher.CheckAccess())
        {
            LastSyncDisplay = display;
            return;
        }

        _dispatcher.Invoke(() => LastSyncDisplay = display);
    }

    private void UpdateConnectionState(bool isConnected, string message)
    {
        if (isConnected)
        {
            _twitchErrorShown = false;
        }
        else if (_integrationSettings.IntgrTwitch && !_twitchErrorShown)
        {
            _twitchErrorShown = true;
            _toast?.Show("🎮 Twitch", message, ToastType.Warning, key: "twitch-error");
        }

        if (_dispatcher.CheckAccess())
        {
            Connected = isConnected;
            StatusMessage = message;
            return;
        }

        _dispatcher.Invoke(() =>
        {
            Connected = isConnected;
            StatusMessage = message;
        });
    }

    private TwitchTokens BuildTokens()
    {
        bool useSmallText = Settings.UseSmallText;

        string live = string.Empty;
        if (Settings.ShowLiveIndicator)
        {
            string livePrefix = string.IsNullOrWhiteSpace(Settings.LivePrefix) ? string.Empty : Settings.LivePrefix.Trim();
            live = useSmallText ? ToSmallTextPreserveEmoji(livePrefix) : livePrefix;
        }

        string game = Settings.ShowGameName ? GameName : string.Empty;
        string gameLabelSource = string.IsNullOrWhiteSpace(Settings.GamePrefix) ? string.Empty : Settings.GamePrefix.Trim();
        string gameLabel = useSmallText ? ToSmallTextPreserveEmoji(gameLabelSource) : gameLabelSource;
        string gameWithLabel = BuildLabeledValue(gameLabel, game);

        string viewerCount = Settings.ShowViewerCount ? FormatViewerCount(ViewerCount, Settings.ViewerCountCompact) : string.Empty;
        string viewerLabelSource = Settings.ShowViewerLabel && !string.IsNullOrWhiteSpace(Settings.ViewerLabel)
            ? Settings.ViewerLabel.Trim()
            : string.Empty;
        string viewerLabel = useSmallText ? ToSmallTextPreserveEmoji(viewerLabelSource) : viewerLabelSource;
        string viewersWithLabel = BuildLabeledValue(viewerLabel, viewerCount);

        string followerCount = Settings.ShowFollowerCount ? FormatViewerCount(FollowerCount, Settings.FollowerCountCompact) : string.Empty;
        string followerLabelSource = Settings.ShowFollowerLabel && !string.IsNullOrWhiteSpace(Settings.FollowerLabel)
            ? Settings.FollowerLabel.Trim()
            : string.Empty;
        string followerLabel = useSmallText ? ToSmallTextPreserveEmoji(followerLabelSource) : followerLabelSource;
        string followersWithLabel = BuildLabeledValue(followerLabel, followerCount);

        string title = Settings.ShowStreamTitle ? StreamTitle : string.Empty;
        string titleLabelSource = string.IsNullOrWhiteSpace(Settings.StreamTitlePrefix) ? string.Empty : Settings.StreamTitlePrefix.Trim();
        string titleLabel = useSmallText ? ToSmallTextPreserveEmoji(titleLabelSource) : titleLabelSource;
        string titleWithLabel = BuildLabeledValue(titleLabel, title);

        string channel = Settings.ShowChannelName ? Settings.ChannelName?.Trim() : string.Empty;
        string channelLabelSource = string.IsNullOrWhiteSpace(Settings.ChannelPrefix) ? string.Empty : Settings.ChannelPrefix.Trim();
        string channelLabel = useSmallText ? ToSmallTextPreserveEmoji(channelLabelSource) : channelLabelSource;
        string channelWithLabel = BuildLabeledValue(channelLabel, channel);

        return new TwitchTokens(
            live,
            game,
            gameWithLabel,
            viewerCount,
            viewerLabel,
            viewersWithLabel,
            followerCount,
            followerLabel,
            followersWithLabel,
            title,
            titleWithLabel,
            channel,
            channelWithLabel,
            IsLive ? "live" : "offline");
    }

    private static string BuildLabeledValue(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return value.Trim();
        }

        return $"{label.Trim()} {value.Trim()}";
    }

    private static string NormalizeSeparator(string separator)
    {
        if (string.IsNullOrWhiteSpace(separator))
        {
            return string.Empty;
        }

        return separator.Replace("\\n", "\n").Replace("\\r", "\r");
    }

    private static string NormalizeTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return template.Replace("\\n", "\n").Replace("\\r", "\r");
    }

    private static string ApplyTemplate(string template, TwitchTokens tokens)
    {
        return template
            .Replace("{live}", tokens.Live ?? string.Empty)
            .Replace("{game}", tokens.Game ?? string.Empty)
            .Replace("{gameWithLabel}", tokens.GameWithLabel ?? string.Empty)
            .Replace("{viewers}", tokens.ViewersWithLabel ?? string.Empty)
            .Replace("{viewerCount}", tokens.ViewerCount ?? string.Empty)
            .Replace("{viewerLabel}", tokens.ViewerLabel ?? string.Empty)
            .Replace("{followers}", tokens.FollowersWithLabel ?? string.Empty)
            .Replace("{followerCount}", tokens.FollowerCount ?? string.Empty)
            .Replace("{followerLabel}", tokens.FollowerLabel ?? string.Empty)
            .Replace("{followersWithLabel}", tokens.FollowersWithLabel ?? string.Empty)
            .Replace("{title}", tokens.Title ?? string.Empty)
            .Replace("{titleWithLabel}", tokens.TitleWithLabel ?? string.Empty)
            .Replace("{channel}", tokens.Channel ?? string.Empty)
            .Replace("{channelWithLabel}", tokens.ChannelWithLabel ?? string.Empty)
            .Replace("{status}", tokens.Status ?? string.Empty);
    }

    private static string FormatViewerCount(int viewers, bool compact)
    {
        if (!compact)
        {
            return viewers.ToString(CultureInfo.CurrentCulture);
        }

        double value = viewers;
        string suffix = string.Empty;
        if (viewers >= 1_000_000_000)
        {
            value = viewers / 1_000_000_000d;
            suffix = "B";
        }
        else if (viewers >= 1_000_000)
        {
            value = viewers / 1_000_000d;
            suffix = "M";
        }
        else if (viewers >= 1_000)
        {
            value = viewers / 1_000d;
            suffix = "K";
        }

        if (string.IsNullOrEmpty(suffix))
        {
            return viewers.ToString(CultureInfo.CurrentCulture);
        }

        string format = value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)}{suffix}";
    }

    private static string ToSmallTextPreserveEmoji(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (!char.IsLetterOrDigit(text[0]))
        {
            int spaceIndex = text.IndexOf(' ');
            if (spaceIndex > 0)
            {
                string prefix = text.Substring(0, spaceIndex);
                string rest = text.Substring(spaceIndex + 1);
                string smallRest = TextUtilities.TransformToSuperscript(rest);
                return string.IsNullOrWhiteSpace(smallRest) ? prefix : $"{prefix} {smallRest}";
            }

            return text;
        }

        return TextUtilities.TransformToSuperscript(text);
    }

    private sealed class TwitchTokens
    {
        public TwitchTokens(
            string live,
            string game,
            string gameWithLabel,
            string viewerCount,
            string viewerLabel,
            string viewersWithLabel,
            string followerCount,
            string followerLabel,
            string followersWithLabel,
            string title,
            string titleWithLabel,
            string channel,
            string channelWithLabel,
            string status)
        {
            Live = live;
            Game = game;
            GameWithLabel = gameWithLabel;
            ViewerCount = viewerCount;
            ViewerLabel = viewerLabel;
            ViewersWithLabel = viewersWithLabel;
            FollowerCount = followerCount;
            FollowerLabel = followerLabel;
            FollowersWithLabel = followersWithLabel;
            Title = title;
            TitleWithLabel = titleWithLabel;
            Channel = channel;
            ChannelWithLabel = channelWithLabel;
            Status = status;
        }

        public string Live { get; }
        public string Game { get; }
        public string GameWithLabel { get; }
        public string ViewerCount { get; }
        public string ViewerLabel { get; }
        public string ViewersWithLabel { get; }
        public string FollowerCount { get; }
        public string FollowerLabel { get; }
        public string FollowersWithLabel { get; }
        public string Title { get; }
        public string TitleWithLabel { get; }
        public string Channel { get; }
        public string ChannelWithLabel { get; }
        public string Status { get; }
    }

    private async Task<bool> EnsureValidTokenAsync()
    {
        string accessToken = Settings.AccessToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            UpdateSnapshot(false, 0, string.Empty, string.Empty);
            UpdateConnectionState(false, "Missing Twitch settings");
            return false;
        }

        bool tokenChanged = !string.Equals(lastValidatedAccessToken, accessToken, StringComparison.Ordinal);
        bool needsValidation = tokenChanged || (DateTime.UtcNow - lastTokenValidationUtc) >= TokenValidationInterval;
        if (!needsValidation)
        {
            return true;
        }

        var validation = await _apiClient.ValidateTokenAsync(accessToken).ConfigureAwait(false);
        lastTokenValidationUtc = DateTime.UtcNow;
        lastValidatedAccessToken = accessToken;

        if (!validation.IsValid)
        {
            cachedBroadcasterId = string.Empty;
            validatedUserId = string.Empty;
            UpdateSnapshot(false, 0, string.Empty, string.Empty);
            UpdateConnectionState(false, "Token invalid");
            return false;
        }

        validatedUserId = validation.UserId;
        return true;
    }

    private static bool IsUnauthorized(Exception ex)
    {
        if (ex == null)
        {
            return false;
        }

        string message = ex.Message ?? string.Empty;
        return message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
    }
}
