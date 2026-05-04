using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public enum SpotifyPauseOutputMode
{
    Hide,
    PauseText,
    LastTrack
}

public enum SpotifyMediaLinkCoexistence
{
    [Description("Ask me once")]
    Ask,
    [Description("Prefer dedicated Spotify")]
    PreferSpotify,
    [Description("Allow both outputs")]
    AllowBoth
}

public enum SpotifyWidgetMode
{
    Compact,
    Detailed
}

public enum SpotifyProgressDisplayMode
{
    None,
    Text,
    SmallNumbers,
    Seekbar
}

/// <summary>
/// Persisted settings for the first-class Spotify Web API integration.
/// OAuth tokens are encrypted with the same DPAPI pattern used by other integrations.
/// </summary>
public partial class SpotifySettings : VersionedSettings
{
    [ObservableProperty] private string _clientId = string.Empty;

    private string _accessTokenEncrypted = string.Empty;
    private string _accessToken = string.Empty;
    private string _refreshTokenEncrypted = string.Empty;
    private string _refreshToken = string.Empty;

    [ObservableProperty] private long _tokenExpiresAtUtcTicks;
    [ObservableProperty] private bool _autoConnectOnStartup = true;
    [ObservableProperty] private int _pollingIntervalSeconds = 5;
    [ObservableProperty] private int _idlePollingIntervalSeconds = 30;
    [ObservableProperty] private SpotifyPauseOutputMode _pauseOutputMode = SpotifyPauseOutputMode.PauseText;
    [ObservableProperty] private SpotifyMediaLinkCoexistence _mediaLinkCoexistence = SpotifyMediaLinkCoexistence.Ask;

    [ObservableProperty] private SpotifyWidgetMode _widgetMode = SpotifyWidgetMode.Detailed;
    [ObservableProperty] private bool _showWidgetProgress = true;
    [ObservableProperty] private bool _showWidgetDevice = true;
    [ObservableProperty] private bool _showWidgetControls = true;
    [ObservableProperty] private bool _showWidgetVolume = true;

    [ObservableProperty] private bool _allowTrackTitleInOutput = true;
    [ObservableProperty] private bool _allowArtistInOutput = true;
    [ObservableProperty] private bool _allowAlbumInOutput = true;
    [ObservableProperty] private bool _allowDeviceInOutput = true;
    [ObservableProperty] private bool _allowPlaybackStateInOutput = true;
    [ObservableProperty] private bool _privacyChoicesCompleted;
    [ObservableProperty] private bool _privacyMode;

    [ObservableProperty] private bool _showTitle = true;
    [ObservableProperty] private bool _showArtist = true;
    [ObservableProperty] private bool _showAlbum = false;
    [ObservableProperty] private bool _showDevice = false;
    [ObservableProperty] private bool _showProgress = false;
    [ObservableProperty] private SpotifyProgressDisplayMode _progressDisplayMode = SpotifyProgressDisplayMode.SmallNumbers;
    [ObservableProperty] private bool _autoDowngradeProgress = true;
    [ObservableProperty] private int _progressBarLength = 8;
    [ObservableProperty] private bool _progressShowTime = true;
    [ObservableProperty] private bool _progressShowTimeInSuperscript = true;
    [ObservableProperty] private bool _progressBarOnTop;
    [ObservableProperty] private bool _progressSpaceAroundObjects = true;
    [ObservableProperty] private bool _progressSpaceBetweenPreSuffixAndTime = false;
    [ObservableProperty] private bool _progressTimePreSuffixOnTheInside = true;
    [ObservableProperty] private string _progressFilledCharacter = "▒";
    [ObservableProperty] private string _progressMiddleCharacter = "▓";
    [ObservableProperty] private string _progressNonFilledCharacter = "░";
    [ObservableProperty] private string _progressTimePrefix = string.Empty;
    [ObservableProperty] private string _progressTimeSuffix = string.Empty;
    [ObservableProperty] private int _selectedSeekbarStyleId = 1;
    [ObservableProperty] private bool _showExplicit = true;
    [ObservableProperty] private bool _showLiked = true;
    [ObservableProperty] private bool _showShuffle = true;
    [ObservableProperty] private bool _showRepeat = true;
    [ObservableProperty] private bool _partyModeEnabled = true;

    [ObservableProperty] private string _outputTemplate = "{play_icon} {artist} - {title} {liked_icon} {explicit_icon}";
    [ObservableProperty] private string _partyTemplate = "{play_icon} DJ: {title} - {artist} {queue}";
    [ObservableProperty] private string _disconnectedText = "Spotify: connect account";
    [ObservableProperty] private string _emptyText = "Spotify: nothing playing";
    [ObservableProperty] private string _pausedText = "Spotify paused";
    [ObservableProperty] private string _privacyHiddenText = "Hidden";

    [ObservableProperty] private string _iconPlaying = "▶";
    [ObservableProperty] private string _iconPaused = "⏸";
    [ObservableProperty] private string _iconExplicit = "🅴";
    [ObservableProperty] private string _iconLiked = "♥";
    [ObservableProperty] private string _iconUnliked = "♡";
    [ObservableProperty] private string _iconShuffleOn = "🔀";
    [ObservableProperty] private string _iconShuffleOff = "";
    [ObservableProperty] private string _iconRepeatOff = "";
    [ObservableProperty] private string _iconRepeatContext = "🔁";
    [ObservableProperty] private string _iconRepeatTrack = "🔂";
    [ObservableProperty] private string _separator = " - ";

    [JsonIgnore]
    public string AccessToken
    {
        get => _accessToken;
        set
        {
            if (_accessToken != value)
            {
                _accessToken = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _accessToken, ref _accessTokenEncrypted, true);
                OnPropertyChanged(nameof(AccessToken));
                OnPropertyChanged(nameof(AccessTokenEncrypted));
            }
        }
    }

    public string AccessTokenEncrypted
    {
        get => _accessTokenEncrypted;
        set
        {
            if (_accessTokenEncrypted != value)
            {
                _accessTokenEncrypted = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _accessTokenEncrypted, ref _accessToken, false);
                OnPropertyChanged(nameof(AccessTokenEncrypted));
                OnPropertyChanged(nameof(AccessToken));
            }
        }
    }

    [JsonIgnore]
    public string RefreshToken
    {
        get => _refreshToken;
        set
        {
            if (_refreshToken != value)
            {
                _refreshToken = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _refreshToken, ref _refreshTokenEncrypted, true);
                OnPropertyChanged(nameof(RefreshToken));
                OnPropertyChanged(nameof(RefreshTokenEncrypted));
            }
        }
    }

    public string RefreshTokenEncrypted
    {
        get => _refreshTokenEncrypted;
        set
        {
            if (_refreshTokenEncrypted != value)
            {
                _refreshTokenEncrypted = value ?? string.Empty;
                EncryptionMethods.TryProcessToken(ref _refreshTokenEncrypted, ref _refreshToken, false);
                OnPropertyChanged(nameof(RefreshTokenEncrypted));
                OnPropertyChanged(nameof(RefreshToken));
            }
        }
    }

    [JsonIgnore]
    public bool HasSavedToken => !string.IsNullOrWhiteSpace(AccessToken) || !string.IsNullOrWhiteSpace(RefreshToken);

    [JsonIgnore]
    public DateTime TokenExpiresAtUtc
    {
        get => TokenExpiresAtUtcTicks > 0 ? new DateTime(TokenExpiresAtUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
        set => TokenExpiresAtUtcTicks = value <= DateTime.MinValue ? 0 : value.ToUniversalTime().Ticks;
    }

    public static readonly (string Name, string Template)[] TemplatePresets =
    [
        ("Compact", "{play_icon} {artist} - {title}"),
        ("Rich", "{play_icon} {title} by {artist} {liked_icon} {explicit_icon}"),
        ("MediaLink style", "{play_icon} {title} ᵇʸ {artist}\\n{seekbar}"),
        ("Compact seekbar", "{play_icon} {title} {seekbar}"),
        ("Album", "{title}\\n{artist} - {album}"),
        ("Controls", "{shuffle_icon} {repeat_icon} {device}"),
        ("Party/DJ", "{play_icon} DJ: {title} - {artist} {queue}"),
        ("Minimal", "{title}")
    ];
}
