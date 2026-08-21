using System.Collections.Generic;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;

namespace vrcosc_magicchatbox.Core.Services;

public interface IModuleHost : INotifyPropertyChanged
{
    ComponentStatsModule ComponentStats { get; set; }
    IntelliChatModule IntelliChat { get; set; }
    TwitchModule Twitch { get; set; }
    TikTokLiveModule TikTokLive { get; set; }
    DiscordModule Discord { get; set; }
    SpotifyModule Spotify { get; set; }
    VrcLogModule VrcRadar { get; set; }
    PulsoidModule Pulsoid { get; set; }
    SoundpadModule Soundpad { get; set; }
    VoicemodModule Voicemod { get; set; }
    TrackerBatteryModule TrackerBattery { get; set; }
    vrcosc_magicchatbox.Classes.Modules.Vr.VrPerformanceModule VrPerformance { get; set; }
    vrcosc_magicchatbox.Classes.Modules.Lyrics.LyricsModule Lyrics { get; set; }
    WhisperModule Whisper { get; set; }
    AfkModule Afk { get; set; }

    IReadOnlyList<vrcosc_magicchatbox.Services.IModule> AllModules { get; }

    void RegisterModule(vrcosc_magicchatbox.Services.IModule module);

    System.Threading.Tasks.Task StopAllAsync(System.TimeSpan perModuleTimeout);
}
