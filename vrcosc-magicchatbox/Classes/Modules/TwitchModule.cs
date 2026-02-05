using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TwitchLib.Api;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

public sealed partial class TwitchModule : ObservableObject
{
    private const int MinimumRefreshSeconds = 15;
    private const int MaximumRefreshSeconds = 3600;
    private static readonly TimeSpan TokenValidationInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FollowerRefreshInterval = TimeSpan.FromMinutes(2);
    private static readonly HttpClient HelixClient = new HttpClient
    {
        BaseAddress = new Uri("https://api.twitch.tv/helix/"),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly TwitchAPI api = new TwitchAPI();
    private bool refreshInProgress;
    private DateTime lastRefreshUtc = DateTime.MinValue;
    private DateTime lastTokenValidationUtc = DateTime.MinValue;
    private DateTime lastFollowerRefreshUtc = DateTime.MinValue;
    private string lastValidatedAccessToken = string.Empty;
    private string lastConfiguredClientId = string.Empty;
    private string lastConfiguredAccessToken = string.Empty;
    private string cachedBroadcasterId = string.Empty;
    private string lastUsedChannelName = string.Empty;
    private string validatedUserId = string.Empty;

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

    public void TriggerRefreshIfNeeded()
    {
        if (!ViewModel.Instance.IntgrTwitch || !HasConfiguration())
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
        return !string.IsNullOrWhiteSpace(ViewModel.Instance.TwitchChannelName) &&
               !string.IsNullOrWhiteSpace(ViewModel.Instance.TwitchClientId) &&
               !string.IsNullOrWhiteSpace(ViewModel.Instance.TwitchAccessToken);
    }

    private int GetRefreshIntervalSeconds()
    {
        int interval = ViewModel.Instance.TwitchUpdateIntervalSeconds;
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
        string clientId = ViewModel.Instance.TwitchClientId?.Trim() ?? string.Empty;
        string accessToken = ViewModel.Instance.TwitchAccessToken?.Trim() ?? string.Empty;

        if (!string.Equals(api.Settings.ClientId, clientId, StringComparison.Ordinal))
        {
            api.Settings.ClientId = clientId;
        }

        if (!string.Equals(api.Settings.AccessToken, accessToken, StringComparison.Ordinal))
        {
            api.Settings.AccessToken = accessToken;
        }

        if (!string.Equals(lastConfiguredClientId, clientId, StringComparison.Ordinal) ||
            !string.Equals(lastConfiguredAccessToken, accessToken, StringComparison.Ordinal))
        {
            lastConfiguredClientId = clientId;
            lastConfiguredAccessToken = accessToken;
            cachedBroadcasterId = string.Empty;
            validatedUserId = string.Empty;
            lastValidatedAccessToken = string.Empty;
        }
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
            string channelName = ViewModel.Instance.TwitchChannelName?.Trim() ?? string.Empty;
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

            var streamsResponse = await api.Helix.Streams.GetStreamsAsync(userIds: new List<string> { cachedBroadcasterId }).ConfigureAwait(false);
            var stream = streamsResponse?.Streams?.FirstOrDefault();

            if (stream == null)
            {
                UpdateSnapshot(false, 0, string.Empty, string.Empty);
                UpdateConnectionState(true, "Offline");
                UpdateLastSync(DateTime.UtcNow);
                await RefreshFollowerCountAsync().ConfigureAwait(false);
                return;
            }

            UpdateSnapshot(true, stream.ViewerCount, stream.GameName, stream.Title);
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
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ApplySnapshot(live, viewers, game, title);
            return;
        }

        dispatcher.BeginInvoke(new Action(() => ApplySnapshot(live, viewers, game, title)));
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

        var usersResponse = await api.Helix.Users.GetUsersAsync(logins: new List<string> { channelName }).ConfigureAwait(false);
        var user = usersResponse?.Users?.FirstOrDefault();
        if (user == null)
        {
            UpdateSnapshot(false, 0, string.Empty, string.Empty);
            UpdateConnectionState(false, "Channel not found");
            return false;
        }

        cachedBroadcasterId = user.Id;
        lastUsedChannelName = channelName;
        return true;
    }

    private bool ShouldFetchFollowerCount()
    {
        var vm = ViewModel.Instance;
        if (vm.TwitchShowFollowerCount)
        {
            return true;
        }

        string template = vm.TwitchTemplate;
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

        var result = await TryGetFollowerCountAsync(cachedBroadcasterId).ConfigureAwait(false);
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
    }

    private async Task<(bool Success, int Count, bool Unauthorized, string Message)> TryGetFollowerCountAsync(string broadcasterId)
    {
        using var request = CreateHelixRequest(HttpMethod.Get, $"channels/followers?broadcaster_id={broadcasterId}");
        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return (false, 0, true, "Unauthorized");
        }

        if (!response.IsSuccessStatusCode)
        {
            return (false, 0, false, $"Followers request failed ({(int)response.StatusCode})");
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var total))
        {
            return (true, total, false, string.Empty);
        }

        return (false, 0, false, "Followers total missing");
    }

    private void UpdateFollowerCount(int count)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ApplyFollowerCount(count);
            return;
        }

        dispatcher.BeginInvoke(new Action(() => ApplyFollowerCount(count)));
    }

    private void ApplyFollowerCount(int count)
    {
        FollowerCount = count;
        GetOutputString();
    }

    private HttpRequestMessage CreateHelixRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Add("Client-Id", api.Settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.Settings.AccessToken);
        return request;
    }

    public async Task<(bool Success, string Message)> SendAnnouncementAsync(string message, TwitchAnnouncementColor color)
    {
        var prep = await PrepareChatActionAsync().ConfigureAwait(false);
        if (!prep.Success)
        {
            return (false, prep.Message);
        }

        return await SendAnnouncementInternalAsync(prep.BroadcasterId, prep.ModeratorId, message, color).ConfigureAwait(false);
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

        string targetId = await ResolveUserIdAsync(normalizedLogin).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return (false, "Channel not found.");
        }

        using var request = CreateHelixRequest(
            HttpMethod.Post,
            $"chat/shoutouts?from_broadcaster_id={prep.BroadcasterId}&to_broadcaster_id={targetId}&moderator_id={prep.ModeratorId}");

        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            cachedBroadcasterId = string.Empty;
            lastValidatedAccessToken = string.Empty;
            validatedUserId = string.Empty;
            return (false, "Token invalid.");
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, "Missing shoutout permission or scope.");
            }

            if ((int)response.StatusCode == 429)
            {
                return (false, "Rate limited. Try again soon.");
            }

            return (false, $"Shoutout failed ({(int)response.StatusCode}).");
        }

        if (alsoAnnounce)
        {
            string builtAnnouncement = BuildShoutoutAnnouncement(announcementTemplate, normalizedLogin);
            if (!string.IsNullOrWhiteSpace(builtAnnouncement))
            {
                var announcementResult = await SendAnnouncementInternalAsync(
                    prep.BroadcasterId,
                    prep.ModeratorId,
                    builtAnnouncement,
                    announcementColor).ConfigureAwait(false);
                if (!announcementResult.Success)
                {
                    return (true, $"Shoutout sent. Announcement failed: {announcementResult.Message}");
                }
            }
        }

        return (true, "Shoutout sent!");
    }

    private async Task<(bool Success, string Message, string BroadcasterId, string ModeratorId)> PrepareChatActionAsync()
    {
        if (!HasConfiguration())
        {
            return (false, "Missing Twitch settings.", string.Empty, string.Empty);
        }

        ConfigureApi();
        string channelName = ViewModel.Instance.TwitchChannelName?.Trim() ?? string.Empty;
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

    private async Task<(bool Success, string Message)> SendAnnouncementInternalAsync(
        string broadcasterId,
        string moderatorId,
        string message,
        TwitchAnnouncementColor color)
    {
        string trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return (false, "Announcement message is empty.");
        }

        string colorValue = MapAnnouncementColor(color);
        var payload = new
        {
            message = trimmed,
            color = colorValue
        };

        using var request = CreateHelixRequest(
            HttpMethod.Post,
            $"chat/announcements?broadcaster_id={broadcasterId}&moderator_id={moderatorId}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await HelixClient.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            cachedBroadcasterId = string.Empty;
            lastValidatedAccessToken = string.Empty;
            validatedUserId = string.Empty;
            return (false, "Token invalid.");
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return (false, "Missing announcement permission or scope.");
            }

            if ((int)response.StatusCode == 429)
            {
                return (false, "Rate limited. Try again soon.");
            }

            return (false, $"Announcement failed ({(int)response.StatusCode}).");
        }

        return (true, "Announcement sent!");
    }

    private async Task<string> ResolveUserIdAsync(string login)
    {
        string normalized = NormalizeLogin(login);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var usersResponse = await api.Helix.Users.GetUsersAsync(logins: new List<string> { normalized }).ConfigureAwait(false);
        var user = usersResponse?.Users?.FirstOrDefault();
        return user?.Id ?? string.Empty;
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
            return ViewModel.Instance.TwitchOfflineMessage ?? string.Empty;
        }

        TwitchTokens tokens = BuildTokens();
        string template = NormalizeTemplate(ViewModel.Instance.TwitchTemplate);
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

        string separator = NormalizeSeparator(ViewModel.Instance.TwitchSeparator);
        if (string.IsNullOrWhiteSpace(separator))
        {
            separator = " | ";
        }

        return string.Join(separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatLastSync(DateTime utc)
    {
        if (utc == DateTime.MinValue)
        {
            return "Last sync: Never";
        }

        DateTime local = utc.ToLocalTime();
        string format = ViewModel.Instance.Time24H ? "HH:mm" : "h:mm tt";
        return $"Last sync: {local.ToString(format, CultureInfo.CurrentCulture)}";
    }

    private void UpdateLastSync(DateTime utc)
    {
        string display = FormatLastSync(utc);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ViewModel.Instance.TwitchLastSyncDisplay = display;
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            ViewModel.Instance.TwitchLastSyncDisplay = display;
        }));
    }

    private static void UpdateConnectionState(bool isConnected, string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ViewModel.Instance.TwitchConnected = isConnected;
            ViewModel.Instance.TwitchStatusMessage = message;
            return;
        }

        dispatcher.BeginInvoke(new Action(() =>
        {
            ViewModel.Instance.TwitchConnected = isConnected;
            ViewModel.Instance.TwitchStatusMessage = message;
        }));
    }

    private TwitchTokens BuildTokens()
    {
        var vm = ViewModel.Instance;
        bool useSmallText = vm.TwitchUseSmallText;

        string live = string.Empty;
        if (vm.TwitchShowLiveIndicator)
        {
            string livePrefix = string.IsNullOrWhiteSpace(vm.TwitchLivePrefix) ? string.Empty : vm.TwitchLivePrefix.Trim();
            live = useSmallText ? ToSmallTextPreserveEmoji(livePrefix) : livePrefix;
        }

        string game = vm.TwitchShowGameName ? GameName : string.Empty;
        string gameLabelSource = string.IsNullOrWhiteSpace(vm.TwitchGamePrefix) ? string.Empty : vm.TwitchGamePrefix.Trim();
        string gameLabel = useSmallText ? ToSmallTextPreserveEmoji(gameLabelSource) : gameLabelSource;
        string gameWithLabel = BuildLabeledValue(gameLabel, game);

        string viewerCount = vm.TwitchShowViewerCount ? FormatViewerCount(ViewerCount, vm.TwitchViewerCountCompact) : string.Empty;
        string viewerLabelSource = vm.TwitchShowViewerLabel && !string.IsNullOrWhiteSpace(vm.TwitchViewerLabel)
            ? vm.TwitchViewerLabel.Trim()
            : string.Empty;
        string viewerLabel = useSmallText ? ToSmallTextPreserveEmoji(viewerLabelSource) : viewerLabelSource;
        string viewersWithLabel = BuildLabeledValue(viewerLabel, viewerCount);

        string followerCount = vm.TwitchShowFollowerCount ? FormatViewerCount(FollowerCount, vm.TwitchFollowerCountCompact) : string.Empty;
        string followerLabelSource = vm.TwitchShowFollowerLabel && !string.IsNullOrWhiteSpace(vm.TwitchFollowerLabel)
            ? vm.TwitchFollowerLabel.Trim()
            : string.Empty;
        string followerLabel = useSmallText ? ToSmallTextPreserveEmoji(followerLabelSource) : followerLabelSource;
        string followersWithLabel = BuildLabeledValue(followerLabel, followerCount);

        string title = vm.TwitchShowStreamTitle ? StreamTitle : string.Empty;
        string titleLabelSource = string.IsNullOrWhiteSpace(vm.TwitchStreamTitlePrefix) ? string.Empty : vm.TwitchStreamTitlePrefix.Trim();
        string titleLabel = useSmallText ? ToSmallTextPreserveEmoji(titleLabelSource) : titleLabelSource;
        string titleWithLabel = BuildLabeledValue(titleLabel, title);

        string channel = vm.TwitchShowChannelName ? vm.TwitchChannelName?.Trim() : string.Empty;
        string channelLabelSource = string.IsNullOrWhiteSpace(vm.TwitchChannelPrefix) ? string.Empty : vm.TwitchChannelPrefix.Trim();
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
                string smallRest = DataController.TransformToSuperscript(rest);
                return string.IsNullOrWhiteSpace(smallRest) ? prefix : $"{prefix} {smallRest}";
            }

            return text;
        }

        return DataController.TransformToSuperscript(text);
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
        string accessToken = ViewModel.Instance.TwitchAccessToken?.Trim() ?? string.Empty;
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

        var validation = await api.Auth.ValidateAccessTokenAsync(accessToken).ConfigureAwait(false);
        lastTokenValidationUtc = DateTime.UtcNow;
        lastValidatedAccessToken = accessToken;

        if (validation == null)
        {
            cachedBroadcasterId = string.Empty;
            validatedUserId = string.Empty;
            UpdateSnapshot(false, 0, string.Empty, string.Empty);
            UpdateConnectionState(false, "Token invalid");
            return false;
        }

        validatedUserId = validation.UserId ?? string.Empty;

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
