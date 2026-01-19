using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    private readonly TwitchAPI api = new TwitchAPI();
    private bool refreshInProgress;
    private DateTime lastRefreshUtc = DateTime.MinValue;

    [ObservableProperty]
    private string outputString = string.Empty;

    [ObservableProperty]
    private bool isLive;

    [ObservableProperty]
    private int viewerCount;

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
        api.Settings.ClientId = ViewModel.Instance.TwitchClientId?.Trim();
        api.Settings.AccessToken = ViewModel.Instance.TwitchAccessToken?.Trim();
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
            string channelName = ViewModel.Instance.TwitchChannelName.Trim();

            var usersResponse = await api.Helix.Users.GetUsersAsync(logins: new List<string> { channelName }).ConfigureAwait(false);
            var user = usersResponse?.Users?.FirstOrDefault();
            if (user == null)
            {
                UpdateSnapshot(false, 0, string.Empty, string.Empty);
                UpdateConnectionState(false, "Channel not found");
                return;
            }

            var streamsResponse = await api.Helix.Streams.GetStreamsAsync(userIds: new List<string> { user.Id }).ConfigureAwait(false);
            var stream = streamsResponse?.Streams?.FirstOrDefault();

            if (stream == null)
            {
                UpdateSnapshot(false, 0, string.Empty, string.Empty);
                UpdateConnectionState(true, "Offline");
                UpdateLastSync(DateTime.UtcNow);
                return;
            }

            UpdateSnapshot(true, stream.ViewerCount, stream.GameName, stream.Title);
            UpdateConnectionState(true, "Live");
            UpdateLastSync(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            UpdateConnectionState(false, "Refresh failed");
        }
        finally
        {
            refreshInProgress = false;
        }
    }

    private void UpdateSnapshot(bool live, int viewers, string game, string title)
    {
        IsLive = live;
        ViewerCount = viewers;
        GameName = game ?? string.Empty;
        StreamTitle = title ?? string.Empty;
        GetOutputString();
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
        public string Title { get; }
        public string TitleWithLabel { get; }
        public string Channel { get; }
        public string ChannelWithLabel { get; }
        public string Status { get; }
    }
}
