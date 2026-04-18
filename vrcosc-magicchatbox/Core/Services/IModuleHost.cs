using System.Collections.Generic;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Single source of truth for all runtime module instances.
/// Modules are assigned during startup in a defined order by ModuleBootstrapper.
/// Services depend on this interface instead of ViewModel for module access.
/// </summary>
public interface IModuleHost : INotifyPropertyChanged
{
    ComponentStatsModule ComponentStats { get; set; }
    IntelliChatModule IntelliChat { get; set; }
    TwitchModule Twitch { get; set; }
    PulsoidModule Pulsoid { get; set; }
    SoundpadModule Soundpad { get; set; }
    TrackerBatteryModule TrackerBattery { get; set; }
    WhisperModule Whisper { get; set; }
    AfkModule Afk { get; set; }

    /// <summary>
    /// All registered modules for enumeration (save-all, stop-all, etc.).
    /// </summary>
    IReadOnlyList<vrcosc_magicchatbox.Services.IModule> AllModules { get; }

    /// <summary>
    /// Register a module into the AllModules collection.
    /// Called by ModuleBootstrapper during startup.
    /// </summary>
    void RegisterModule(vrcosc_magicchatbox.Services.IModule module);
}
