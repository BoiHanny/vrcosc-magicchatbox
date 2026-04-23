using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the Discord voice channel integration.
/// Template placeholders: {channel}, {count}, {speaking}, {mute_emoji}
/// </summary>
public partial class DiscordSettings : VersionedSettings
{
    // --- Output template ---
    [ObservableProperty] private string _template = "🔊 {channel} ({count}) | 🎙️ {speaking}";
    [ObservableProperty] private string _emptySpeakingText = "Quiet...";
    [ObservableProperty] private string _notInVcText = "";
    [ObservableProperty] private int _maxSpeakingUsersToShow = 2;

    // --- Mute / Deafen display ---
    [ObservableProperty] private bool _showMuteDeafenEmoji = true;
    [ObservableProperty] private string _muteEmoji = "ᵐᵘᵗᵉᵈ";
    [ObservableProperty] private string _deafenEmoji = "ᵈᵉᵃᶠᵉⁿ";

    // --- Behaviour ---
    [ObservableProperty] private bool _autoConnectOnStartup = false;
    [ObservableProperty] private bool _hideSelfFromSpeakers = false;
    [ObservableProperty] private bool _showUserCountOnly = false;
    [ObservableProperty] private int _speakerDebounceMs = 500;

    // --- OSC parameters ---
    [ObservableProperty] private bool _sendMuteDeafenOsc = false;
    [ObservableProperty] private bool _sendVoiceStateOsc = false;

    // Encrypted OAuth access token (same DPAPI pattern as PulsoidModuleSettings)
    private string _accessTokenEncrypted = string.Empty;
    private string _accessToken = string.Empty;

    // Encrypted OAuth refresh token for token renewal
    private string _refreshTokenEncrypted = string.Empty;
    private string _refreshToken = string.Empty;

    // Token expiry (UTC ticks) — 0 means unknown/never
    [ObservableProperty] private long _tokenExpiresAtUtcTicks;

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

    /// <summary>
    /// Built-in template presets. Not persisted — UI only.
    /// </summary>
    public static readonly (string Name, string Value)[] TemplatePresets =
    [
        ("Detailed",      "🔊 {channel} ({count}) | 🎙️ {speaking} {mute_emoji}"),
        ("Compact",       "🔊 {channel} ({count}) {mute_emoji}"),
        ("Minimal",       "🎙️ {speaking}"),
        ("Count Only",    "🔊 {channel} — {count} users"),
        ("Multi-line",    "🔊 {channel} ({count})\\n🎙️ {speaking} {mute_emoji}"),
        ("Speaker Focus", "🎙️ {speaking}\\n🔊 {channel} {mute_emoji}"),
        ("Status Bar",    "{mute_emoji} {channel} 👥{count}"),
        ("Emoji Rich",    "🎧 {channel} | 👥 {count} | 🎙️ {speaking} | {mute_emoji}"),
    ];
}
