using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

public sealed class StatePersistenceCoordinator : IStatePersistenceCoordinator
{
    // How long any one module gets to stop, and how long the whole set gets between them. Closing
    // the app must not wait on a service that has stopped answering.
    private static readonly TimeSpan ModuleStopTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ModuleShutdownBudget = TimeSpan.FromSeconds(8);

    private readonly Lazy<IOscSender> _oscSender;
    private readonly ISettingsProvider<IntegrationSettings> _intSettingsProvider;
    private readonly ISettingsProvider<TrackerBatterySettings> _trkSettingsProvider;
    private readonly ISettingsProvider<MediaLinkSettings> _mediaLinkSettingsProvider;
    private readonly ISettingsProvider<OscSettings> _oscSettingsProvider;
    private readonly ISettingsProvider<ChatSettings> _chatSettingsProvider;
    private readonly ISettingsProvider<OpenAISettings> _openAISettingsProvider;
    private readonly ISettingsProvider<TimeSettings> _timeSettingsProvider;
    private readonly ISettingsProvider<TtsSettings> _ttsSettingsProvider;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly TrackerDisplayState _trackerDisplay;
    private readonly Lazy<IModuleHost> _modules;
    private readonly Lazy<IAppHistoryService> _appHistorySvc;
    private readonly Lazy<IChatHistoryService> _chatHistorySvc;
    private readonly Lazy<IMediaLinkPersistenceService> _mediaLinkSvc;
    private readonly HotkeyManagement _hotkeyMgmt;
    private readonly IWindowActivityService _windowActivity;
    private readonly IWeatherService _weatherSvc;
    private readonly IStatusListService _statusListSvc;

    public StatePersistenceCoordinator(
        Lazy<IOscSender> oscSender,
        ISettingsProvider<IntegrationSettings> intSettingsProvider,
        ISettingsProvider<TrackerBatterySettings> trkSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        ISettingsProvider<OscSettings> oscSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider,
        ISettingsProvider<OpenAISettings> openAISettingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        ISettingsProvider<TtsSettings> ttsSettingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IntegrationDisplayState integrationDisplay,
        TrackerDisplayState trackerDisplay,
        Lazy<IModuleHost> modules,
        Lazy<IAppHistoryService> appHistorySvc,
        Lazy<IChatHistoryService> chatHistorySvc,
        Lazy<IMediaLinkPersistenceService> mediaLinkSvc,
        HotkeyManagement hotkeyMgmt,
        IWindowActivityService windowActivity,
        IWeatherService weatherSvc,
        IStatusListService statusListSvc)
    {
        _oscSender = oscSender;
        _intSettingsProvider = intSettingsProvider;
        _trkSettingsProvider = trkSettingsProvider;
        _mediaLinkSettingsProvider = mediaLinkSettingsProvider;
        _oscSettingsProvider = oscSettingsProvider;
        _chatSettingsProvider = chatSettingsProvider;
        _openAISettingsProvider = openAISettingsProvider;
        _timeSettingsProvider = timeSettingsProvider;
        _ttsSettingsProvider = ttsSettingsProvider;
        _appSettingsProvider = appSettingsProvider;
        _integrationDisplay = integrationDisplay;
        _trackerDisplay = trackerDisplay;
        _modules = modules;
        _appHistorySvc = appHistorySvc;
        _chatHistorySvc = chatHistorySvc;
        _mediaLinkSvc = mediaLinkSvc;
        _hotkeyMgmt = hotkeyMgmt;
        _windowActivity = windowActivity;
        _weatherSvc = weatherSvc;
        _statusListSvc = statusListSvc;
    }

    private static void SafeRun(string stepName, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            Logging.WriteInfo($"StatePersistenceCoordinator: '{stepName}' failed: {ex.Message}");
        }
    }

    public void PersistAllState()
    {
        SafeRun("IntegrationSettings", () =>
        {
            _intSettingsProvider.Value.SavedSortOrder = _integrationDisplay.IntegrationSortOrder;
            _intSettingsProvider.FlushPendingSave();
        });

        SafeRun("TrackerBatterySettings", () =>
        {
            _trkSettingsProvider.Value.SavedDevices = _trackerDisplay.TrackerDevices;
            _trkSettingsProvider.FlushPendingSave();
        });

        SafeRun("AppHistory", () => _appHistorySvc.Value.SaveAppHistory());
        SafeRun("ChatHistory", () => _chatHistorySvc.Value.SaveChatHistory());
        SafeRun("MediaSessions", () => _mediaLinkSvc.Value.SaveMediaSessions());
        SafeRun("SeekbarStyles", () => _mediaLinkSvc.Value.SaveSeekbarStyles());
        SafeRun("Hotkeys", () => _hotkeyMgmt.SaveHotkeyConfigurations());
        SafeRun("WindowActivity", () => _windowActivity.SaveSettings());
        SafeRun("Weather", () => _weatherSvc.SaveSettings());

        SafeRun("MediaLinkSettings", () => _mediaLinkSettingsProvider.FlushPendingSave());
        SafeRun("OscSettings", () => _oscSettingsProvider.FlushPendingSave());
        SafeRun("ChatSettings", () => _chatSettingsProvider.FlushPendingSave());
        SafeRun("OpenAISettings", () => _openAISettingsProvider.FlushPendingSave());
        SafeRun("TimeSettings", () => _timeSettingsProvider.FlushPendingSave());
        SafeRun("TtsSettings", () => _ttsSettingsProvider.FlushPendingSave());
        SafeRun("AppSettings", () => _appSettingsProvider.FlushPendingSave());

        SafeRun("StatusList", () => _statusListSvc.SaveStatusList());

        IModuleHost? moduleHost = null;
        SafeRun("ModuleHostResolve", () => moduleHost = _modules.Value);

        if (moduleHost != null)
        {
            foreach (var module in moduleHost.AllModules)
            {
                try { module.SaveSettings(); }
                catch (Exception ex) { Logging.WriteInfo($"Error saving {module.Name}: {ex.Message}"); }
            }

            SafeRun("Whisper.OnApplicationClosing", () => moduleHost.Whisper?.OnApplicationClosing());
            SafeRun("Afk.OnApplicationClosing", () => moduleHost.Afk?.OnApplicationClosing());
        }
    }

    public async Task PrepareForShutdownAsync()
    {
        try
        {
            await _oscSender.Value.SentClearMessage(1500);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            Logging.WriteInfo($"StatePersistenceCoordinator: OSC clear failed during shutdown: {ex.Message}");
        }

        try
        {
            PersistAllState();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true, exitapp: true);
        }

        // After the settings are safely written, not before: stopping a module can clear the state
        // it would otherwise have saved.
        try
        {
            IModuleHost moduleHost = _modules.Value;
            Task stopAll = moduleHost.StopAllAsync(ModuleStopTimeout);

            if (await Task.WhenAny(stopAll, Task.Delay(ModuleShutdownBudget)).ConfigureAwait(false) != stopAll)
                Logging.WriteInfo(
                    $"StatePersistenceCoordinator: modules did not all stop within {ModuleShutdownBudget.TotalSeconds:0.#}s; closing anyway.");
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"StatePersistenceCoordinator: stopping modules failed: {ex.Message}");
        }
    }
}
