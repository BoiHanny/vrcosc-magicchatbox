using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Services;

public partial class ModuleHost : ObservableObject, IModuleHost
{
    private readonly List<IModule> _modules = new();
    private readonly object _modulesLock = new();

    [ObservableProperty] private ComponentStatsModule _componentStats;

    [ObservableProperty] private IntelliChatModule _intelliChat;
    [ObservableProperty] private TwitchModule _twitch;
    [ObservableProperty] private TikTokLiveModule _tikTokLive;
    [ObservableProperty] private DiscordModule _discord;
    [ObservableProperty] private SpotifyModule _spotify;
    [ObservableProperty] private VrcLogModule _vrcRadar;
    [ObservableProperty] private PulsoidModule _pulsoid;
    [ObservableProperty] private SoundpadModule _soundpad;
    [ObservableProperty] private TrackerBatteryModule _trackerBattery;
    [ObservableProperty] private vrcosc_magicchatbox.Classes.Modules.Vr.VrPerformanceModule _vrPerformance;
    [ObservableProperty] private vrcosc_magicchatbox.Classes.Modules.Lyrics.LyricsModule _lyrics;
    [ObservableProperty] private WhisperModule _whisper;
    [ObservableProperty] private AfkModule _afk;

    public IReadOnlyList<IModule> AllModules
    {
        get
        {
            lock (_modulesLock)
                return _modules.ToArray();
        }
    }

    public void RegisterModule(IModule module)
    {
        if (module == null)
            return;

        lock (_modulesLock)
        {
            if (!_modules.Contains(module))
                _modules.Add(module);
        }
    }
}
