using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;

namespace MagicChatbox.Tests.TestDoubles;

// A module host with nothing running. Every view model that reaches for a module already handles the
// null - the app is expected to render before its modules start - so this is the state a page has to
// survive, and the one a visual test can reach without standing up sixteen modules.
public sealed class BridgelessModuleHost : IModuleHost
{
    public ComponentStatsModule ComponentStats { get; set; } = null!;
    public IntelliChatModule IntelliChat { get; set; } = null!;
    public TwitchModule Twitch { get; set; } = null!;
    public TikTokLiveModule TikTokLive { get; set; } = null!;
    public DiscordModule Discord { get; set; } = null!;
    public SpotifyModule Spotify { get; set; } = null!;
    public VrcLogModule VrcRadar { get; set; } = null!;
    public PulsoidModule Pulsoid { get; set; } = null!;
    public SoundpadModule Soundpad { get; set; } = null!;
    public TrackerBatteryModule TrackerBattery { get; set; } = null!;
    public vrcosc_magicchatbox.Classes.Modules.Vr.VrPerformanceModule VrPerformance { get; set; } = null!;
    public vrcosc_magicchatbox.Services.Vrc.VrcBridgeModule VrcBridge { get; set; } = null!;
    public vrcosc_magicchatbox.Classes.Modules.Lyrics.LyricsModule Lyrics { get; set; } = null!;
    public WhisperModule Whisper { get; set; } = null!;
    public AfkModule Afk { get; set; } = null!;

    public IReadOnlyList<vrcosc_magicchatbox.Services.IModule> AllModules { get; } =
        Array.Empty<vrcosc_magicchatbox.Services.IModule>();

    public void RegisterModule(vrcosc_magicchatbox.Services.IModule module) { }

    public Task StopAllAsync(TimeSpan perModuleTimeout) => Task.CompletedTask;

    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
}
