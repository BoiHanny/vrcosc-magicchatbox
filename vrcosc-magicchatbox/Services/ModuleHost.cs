using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
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

    /// <summary>Stops and disposes every registered module, one at a time.</summary>
    /// <remarks>
    /// Nothing used to do this. Modules were built outside the container, so container teardown
    /// could not reach them either, and every socket, websocket, timer and VR handle they held was
    /// simply abandoned when the process ended. Each module gets its own deadline: closing the app
    /// must not depend on a service that has stopped answering, and the process is going away
    /// regardless.
    /// </remarks>
    public async Task StopAllAsync(TimeSpan perModuleTimeout)
    {
        foreach (var module in AllModules)
        {
            string name = SafeName(module);

            try
            {
                Task stop = module.StopAsync();
                Task finished = await Task.WhenAny(stop, Task.Delay(perModuleTimeout)).ConfigureAwait(false);

                if (finished == stop)
                    await stop.ConfigureAwait(false);
                else
                    Logging.WriteInfo($"ModuleHost: '{name}' did not stop within {perModuleTimeout.TotalSeconds:0.#}s; carrying on without it.");
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"ModuleHost: stopping '{name}' failed: {ex.Message}");
            }

            try
            {
                module.Dispose();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"ModuleHost: disposing '{name}' failed: {ex.Message}");
            }
        }
    }

    private static string SafeName(IModule module)
    {
        try { return module.Name; }
        catch { return module.GetType().Name; }
    }
}
