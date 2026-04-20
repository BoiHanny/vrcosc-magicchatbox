using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Runtime holder for all module instances. This is the single source of truth —
/// ViewModel forwards to this for XAML binding, services resolve this for module access.
/// </summary>
public partial class ModuleHost : ObservableObject, IModuleHost
{
    private readonly List<IModule> _modules = new();

    // ComponentStats is created eagerly (during ViewModel construction)
    [ObservableProperty] private ComponentStatsModule _componentStats;

    // These are created during staged startup by ModuleBootstrapper
    [ObservableProperty] private IntelliChatModule _intelliChat;
    [ObservableProperty] private TwitchModule _twitch;
    [ObservableProperty] private DiscordModule _discord;
    [ObservableProperty] private VrcLogModule _vrcRadar;
    [ObservableProperty] private PulsoidModule _pulsoid;
    [ObservableProperty] private SoundpadModule _soundpad;
    [ObservableProperty] private TrackerBatteryModule _trackerBattery;
    [ObservableProperty] private WhisperModule _whisper;
    [ObservableProperty] private AfkModule _afk;

    public IReadOnlyList<IModule> AllModules => _modules;

    public void RegisterModule(IModule module)
    {
        if (module != null && !_modules.Contains(module))
            _modules.Add(module);
    }
}
