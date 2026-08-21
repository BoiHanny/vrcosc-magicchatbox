using System.Collections.Generic;
using System.ComponentModel;

namespace vrcosc_magicchatbox.Classes.Modules.Voicemod;

public enum VoicemodConnectionState
{
    Disabled,
    PermissionRequired,
    NotConfigured,
    Disconnected,
    Connecting,
    Authorizing,
    Synchronizing,
    Connected,
    Reconnecting,
    Unauthorized,
    Faulted,
}

public enum VoicemodRandomVoiceMode
{
    [Description("All voices")]
    AllVoices,

    [Description("Free voices")]
    FreeVoices,

    [Description("Favorites")]
    FavoriteVoices,

    [Description("Custom voices")]
    CustomVoices,
}

public sealed record VoicemodVoice(
    string Id,
    string FriendlyName,
    bool Enabled,
    bool IsCustom,
    bool Favorited,
    bool IsNew,
    bool IsPurchased,
    string BitmapChecksum)
{
    public string DisplayName => Enabled ? FriendlyName : $"{FriendlyName} (unavailable)";
}

public sealed record VoicemodSound(
    string Id,
    string Name,
    bool Enabled,
    bool IsCustom,
    string PlaybackMode,
    bool Loop,
    bool MuteOtherSounds,
    bool MuteVoice,
    bool StopOtherSounds,
    bool ShowProLogo,
    string BitmapChecksum)
{
    public string DisplayName => Enabled ? Name : $"{Name} (unavailable)";
}

public sealed record VoicemodSoundboard(
    string Id,
    string Name,
    bool Enabled,
    bool IsCustom,
    bool ShowProLogo,
    IReadOnlyList<VoicemodSound> Sounds)
{
    public string DisplayName => Enabled ? Name : $"{Name} (unavailable)";
}

public sealed record VoicemodVoiceParameter(
    string Key,
    string Name,
    double DefaultValue,
    double Minimum,
    double Maximum,
    double Value,
    bool DisplayNormalized,
    int TypeController);
