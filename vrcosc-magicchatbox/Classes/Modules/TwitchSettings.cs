using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the Twitch integration, including channel info, display options, and encrypted auth tokens.
/// </summary>
public partial class TwitchSettings : VersionedSettings
{
    [ObservableProperty] private string _channelName = string.Empty;
    [ObservableProperty] private bool _clientIdEditing = false;
    [ObservableProperty] private bool _accessTokenEditing = false;

    [ObservableProperty] private bool _showViewerCount = true;
    [ObservableProperty] private bool _showGameName = true;
    [ObservableProperty] private bool _showLiveIndicator = true;
    [ObservableProperty] private string _livePrefix = "LIVE";
    [ObservableProperty] private string _offlineMessage = string.Empty;
    [ObservableProperty] private bool _showStreamTitle = false;
    [ObservableProperty] private string _streamTitlePrefix = "title";
    [ObservableProperty] private bool _showChannelName = false;
    [ObservableProperty] private string _channelPrefix = "channel";
    [ObservableProperty] private string _gamePrefix = "playing";
    [ObservableProperty] private bool _showViewerLabel = true;
    [ObservableProperty] private string _viewerLabel = "viewers";
    [ObservableProperty] private bool _viewerCountCompact = false;
    [ObservableProperty] private bool _showFollowerCount = false;
    [ObservableProperty] private bool _showFollowerLabel = true;
    [ObservableProperty] private string _followerLabel = "followers";
    [ObservableProperty] private bool _followerCountCompact = false;
    [ObservableProperty] private bool _useSmallText = true;
    [ObservableProperty] private string _separator = " | ";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemplateHasValue))]
    private string _template = string.Empty;

    public bool TemplateHasValue => !string.IsNullOrWhiteSpace(Template);

    // Refresh interval with clamping (15–3600 seconds)
    private int _updateIntervalSeconds = 60;
    public int UpdateIntervalSeconds
    {
        get => _updateIntervalSeconds;
        set
        {
            value = Math.Clamp(value, 15, 3600);
            SetProperty(ref _updateIntervalSeconds, value);
        }
    }

    [ObservableProperty] private bool _announcementsEnabled = false;
    [ObservableProperty] private string _announcementMessage = string.Empty;
    [ObservableProperty] private TwitchAnnouncementColor _announcementColor = TwitchAnnouncementColor.Primary;

    [ObservableProperty] private bool _shoutoutsEnabled = false;
    [ObservableProperty] private string _shoutoutTarget = string.Empty;
    [ObservableProperty] private bool _shoutoutAlsoAnnounce = true;
    [ObservableProperty] private string _shoutoutAnnouncementTemplate = "Go follow {user} at twitch.tv/{user}";
    [ObservableProperty] private TwitchAnnouncementColor _shoutoutAnnouncementColor = TwitchAnnouncementColor.Purple;

    // Encrypted tokens — only encrypted forms are persisted
    private string _clientIdEncrypted = string.Empty;
    private string _clientId = string.Empty;

    [JsonIgnore]
    public string ClientId
    {
        get => _clientId;
        set
        {
            string v = value ?? string.Empty;
            if (SetProperty(ref _clientId, v))
            {
                EncryptionMethods.TryProcessToken(ref _clientId, ref _clientIdEncrypted, true);
                OnPropertyChanged(nameof(ClientIdEncrypted));
            }
        }
    }

    public string ClientIdEncrypted
    {
        get => _clientIdEncrypted;
        set
        {
            string v = value ?? string.Empty;
            if (SetProperty(ref _clientIdEncrypted, v))
            {
                EncryptionMethods.TryProcessToken(ref _clientIdEncrypted, ref _clientId, false);
                if (_clientId == null) _clientId = string.Empty;
                OnPropertyChanged(nameof(ClientId));
            }
        }
    }

    private string _accessTokenEncrypted = string.Empty;
    private string _accessToken = string.Empty;

    [JsonIgnore]
    public string AccessToken
    {
        get => _accessToken;
        set
        {
            string v = value ?? string.Empty;
            if (SetProperty(ref _accessToken, v))
            {
                EncryptionMethods.TryProcessToken(ref _accessToken, ref _accessTokenEncrypted, true);
                OnPropertyChanged(nameof(AccessTokenEncrypted));
            }
        }
    }

    public string AccessTokenEncrypted
    {
        get => _accessTokenEncrypted;
        set
        {
            string v = value ?? string.Empty;
            if (SetProperty(ref _accessTokenEncrypted, v))
            {
                EncryptionMethods.TryProcessToken(ref _accessTokenEncrypted, ref _accessToken, false);
                if (_accessToken == null) _accessToken = string.Empty;
                OnPropertyChanged(nameof(AccessToken));
            }
        }
    }

    [JsonIgnore]
    public IEnumerable<TwitchAnnouncementColor> AvailableAnnouncementColors { get; } =
        Enum.GetValues(typeof(TwitchAnnouncementColor)).Cast<TwitchAnnouncementColor>().ToList();
}
