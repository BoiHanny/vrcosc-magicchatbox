using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public enum TikTokLiveDisplayMode
{
    [Description("LIVE summary only")]
    SummaryOnly,
    [Description("LIVE events, then summary")]
    EventOverlay,
    [Description("LIVE events only")]
    TransientOnly
}

public enum TikTokOutputOrder
{
    [Description("Profile, then LIVE")]
    ProfileThenLive,

    [Description("LIVE, then profile")]
    LiveThenProfile
}

/// <summary>
/// Persisted settings for the TikTok module. The type name is kept for compatibility
/// with the first experimental live-only settings file.
/// Template placeholders:
///   Profile: {profile}, {display_name}, {followers}, {follower_count}, {change}, {change_count}, {updated}
///   Live summary: {live}, {host}, {viewers}, {viewer_count}, {likes}, {like_count}, {room}
///   Events:  {user}, {unique_id}, {message}, {gift}, {count}, {amount}, {viewers}, {total}, {host}
/// </summary>
[CurrentSchema(3)]
public partial class TikTokLiveSettings : VersionedSettings
{
    [ObservableProperty] private string _profileUserName = string.Empty;
    [ObservableProperty] private bool _showProfileSummary = true;
    [ObservableProperty] private string _profileTemplate = "TikTok @{profile} | {followers} followers";
    [ObservableProperty] private int _profileRefreshMinutes = 30;
    [ObservableProperty] private bool _showProfileFollowerChangeEvents = true;
    [ObservableProperty] private string _profileFollowerChangeTemplate = "TikTok +{change} followers | {followers} total";
    [ObservableProperty] private int _profileFollowerChangeDurationSeconds = 8;

    [ObservableProperty] private string _hostUserName = string.Empty;
    [ObservableProperty] private bool _enableLiveConnector = false;
    [ObservableProperty] private bool _experimentalEnabled = false;
    [ObservableProperty] private bool _autoConnectOnStartup = true;
    [ObservableProperty] private TikTokLiveDisplayMode _displayMode = TikTokLiveDisplayMode.EventOverlay;
    [ObservableProperty] private bool _combineProfileAndLive = true;
    [ObservableProperty] private TikTokOutputOrder _outputOrder = TikTokOutputOrder.ProfileThenLive;
    [ObservableProperty] private string _combinedOutputSeparator = " | ";

    [ObservableProperty] private bool _compactViewerCount = true;
    [ObservableProperty] private bool _compactLikeCount = true;
    [ObservableProperty] private string _summaryTemplate = "LIVE @{host} | {viewers} viewers | {likes} likes";

    [ObservableProperty] private bool _showFollowEvents = true;
    [ObservableProperty] private bool _showCommentEvents = false;
    [ObservableProperty] private bool _showGiftEvents = true;
    [ObservableProperty] private bool _showLikeEvents = false;
    [ObservableProperty] private bool _showViewerMilestones = false;

    [ObservableProperty] private string _followTemplate = "➕ {user} followed";
    [ObservableProperty] private string _commentTemplate = "💬 {user}: {message}";
    [ObservableProperty] private string _giftTemplate = "🎁 {user} sent {gift} x{count}";
    [ObservableProperty] private string _likeTemplate = "❤️ {user} +{count} likes";
    [ObservableProperty] private string _viewerMilestoneTemplate = "👀 {viewers} viewers";

    [ObservableProperty] private int _eventDurationSeconds = 6;
    [ObservableProperty] private int _viewerMilestoneDurationSeconds = 4;
    [ObservableProperty] private int _likeBurstThreshold = 25;
    [ObservableProperty] private int _viewerCountMilestoneStep = 100;
    [ObservableProperty] private int _reconnectDelaySeconds = 6;
    [ObservableProperty] private int _connectionTimeoutSeconds = 15;

    partial void OnProfileRefreshMinutesChanged(int value)
    {
        int clamped = Math.Clamp(value, 15, 720);
        if (value != clamped)
            ProfileRefreshMinutes = clamped;
    }

    partial void OnProfileFollowerChangeDurationSecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 2, 30);
        if (value != clamped)
            ProfileFollowerChangeDurationSeconds = clamped;
    }

    partial void OnEventDurationSecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 2, 30);
        if (value != clamped)
            EventDurationSeconds = clamped;
    }

    partial void OnViewerMilestoneDurationSecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 2, 30);
        if (value != clamped)
            ViewerMilestoneDurationSeconds = clamped;
    }

    partial void OnLikeBurstThresholdChanged(int value)
    {
        int clamped = Math.Clamp(value, 5, 5000);
        if (value != clamped)
            LikeBurstThreshold = clamped;
    }

    partial void OnViewerCountMilestoneStepChanged(int value)
    {
        int clamped = Math.Clamp(value, 10, 10000);
        if (value != clamped)
            ViewerCountMilestoneStep = clamped;
    }

    partial void OnReconnectDelaySecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 3, 60);
        if (value != clamped)
            ReconnectDelaySeconds = clamped;
    }

    partial void OnConnectionTimeoutSecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 5, 60);
        if (value != clamped)
            ConnectionTimeoutSeconds = clamped;
    }
}
