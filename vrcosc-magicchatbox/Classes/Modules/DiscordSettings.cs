using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the Discord voice channel integration.
/// Template placeholders: {channel}, {count}, {speaking}, {speaking_count}, {mute_emoji}, {mute_state}, {voice_state}
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
    [property: JsonIgnore]
    [ObservableProperty] private bool _voiceClientIdEditing = false;

    private string _voiceClientIdEncrypted = string.Empty;
    private string _voiceClientId = Core.Constants.DiscordClientId;

    // --- OSC parameters ---
    [ObservableProperty] private bool _sendMuteDeafenOsc = false;
    [ObservableProperty] private bool _sendVoiceStateOsc = false;

    // --- Rich Presence ---
    /// <summary>Enable Discord Rich Presence showing VRChat world info.</summary>
    [ObservableProperty] private bool _enableRichPresence = false;
    /// <summary>Details line template. Placeholders: {world}, {count}, {type}, {region}</summary>
    [ObservableProperty] private string _richPresenceDetails = "In {world}";
    /// <summary>State line template. Same placeholders.</summary>
    [ObservableProperty] private string _richPresenceState = "{count} players • {type}";
    /// <summary>Show a "Join" button in Rich Presence for public instances.</summary>
    [ObservableProperty] private bool _richPresenceShowJoinButton = false;
    /// <summary>Large image tooltip text.</summary>
    [ObservableProperty] private string _richPresenceLargeText = "VRChat";
    [ObservableProperty] private string _richPresenceLargeImageKey = "vrchat_logo";
    [ObservableProperty] private string _richPresenceSmallImageKey = "magicchatbox";
    [ObservableProperty] private string _richPresenceSmallText = "MagicChatbox";
    /// <summary>Show elapsed time since joining current world.</summary>
    [ObservableProperty] private bool _richPresenceShowElapsed = true;
    [ObservableProperty] private bool _richPresenceShowVrDesktopMode = true;
    [ObservableProperty] private string _richPresenceJoinButtonLabel = "Join World";

    [JsonIgnore]
    public string VoiceClientId
    {
        get => _voiceClientId;
        set
        {
            if (SetProperty(ref _voiceClientId, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _voiceClientId, ref _voiceClientIdEncrypted, true);
                OnPropertyChanged(nameof(VoiceClientIdEncrypted));
            }
        }
    }

    public string VoiceClientIdEncrypted
    {
        get => _voiceClientIdEncrypted;
        set
        {
            if (SetProperty(ref _voiceClientIdEncrypted, value ?? string.Empty))
            {
                EncryptionMethods.TryProcessToken(ref _voiceClientIdEncrypted, ref _voiceClientId, false);
                if (string.IsNullOrWhiteSpace(_voiceClientId))
                    _voiceClientId = Core.Constants.DiscordClientId;

                OnPropertyChanged(nameof(VoiceClientId));
            }
        }
    }

    [JsonProperty("VoiceClientId")]
    private string LegacyVoiceClientId
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                VoiceClientId = value;
        }
    }

    private bool ShouldSerializeLegacyVoiceClientId() => false;

    // Encrypted OAuth access token (same DPAPI pattern as PulsoidModuleSettings)
    private string _accessTokenEncrypted = string.Empty;
    private string _accessToken = string.Empty;

    // Encrypted OAuth refresh token for token renewal
    private string _refreshTokenEncrypted = string.Empty;
    private string _refreshToken = string.Empty;

    // Token expiry (UTC ticks) — 0 means unknown/never
    [ObservableProperty] private long _tokenExpiresAtUtcTicks;

    /// <summary>
    /// Whether the OAuth token was granted with the 'rpc' scope. Defaults to true so
    /// legacy v0.9.181 implicit-grant tokens keep authenticating after the setting is added.
    /// </summary>
    [ObservableProperty] private bool _hasRpcScope = true;

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
        ("Speaker Count", "🎙️ {speaking_count} speaking • {count} in VC"),
        ("Count Only",    "🔊 {channel} — {count} users"),
        ("Multi-line",    "🔊 {channel} ({count})\\n🎙️ {speaking} {mute_emoji}"),
        ("Speaker Focus", "🎙️ {speaking}\\n🔊 {channel} {mute_emoji}"),
        ("Status Bar",    "{mute_emoji} {channel} 👥{count}"),
        ("Emoji Rich",    "🎧 {channel} | 👥 {count} | 🎙️ {speaking} | {mute_emoji}"),
    ];

    /// <summary>
    /// Built-in Rich Presence detail presets.
    /// Variables: {world}, {count}, {type}, {region}, {status}, {mode}, {time}, {media}, {unique}, {peak}, {worlds}
    /// </summary>
    public static readonly (string Name, string Details, string State)[] RichPresencePresets =
    [
        ("World Info",    "In {world}",               "{count} players • {type}"),
        ("Minimal",       "Exploring VRChat",          "{mode} • {count} players"),
        ("Region",        "In {world} ({region})",     "{type} • {count} players"),
        ("Social",        "Hanging out in {world}",    "With {count} people"),
        ("World Host",    "Hosting {world}",           "{unique} unique visitors • Peak: {peak}"),
        ("Host Stats",    "{world} — {worlds} worlds",  "{unique} unique • {count} now"),
        ("Event Host",    "🎉 {world}",                "👥 {count} here • {unique} total visitors"),
        ("Now Playing",   "{media}",                   "In {world} • {count} players"),
        ("Full Status",   "{status}",                  "{world} • {count} players"),
    ];
}
