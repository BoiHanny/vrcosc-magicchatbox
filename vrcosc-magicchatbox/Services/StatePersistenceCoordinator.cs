using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// God-save coordinator — knows about every service/module that needs saving.
/// Temporary: Phase 6C will replace this with IStateSaveParticipant auto-discovery.
/// </summary>
public sealed class StatePersistenceCoordinator : IStatePersistenceCoordinator
{
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
    private readonly Lazy<IMediaLinkPersistenceService> _mediaLinkSvc;
    private readonly HotkeyManagement _hotkeyMgmt;
    private readonly IWindowActivityService _windowActivity;
    private readonly IWeatherService _weatherSvc;

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
        Lazy<IMediaLinkPersistenceService> mediaLinkSvc,
        HotkeyManagement hotkeyMgmt,
        IWindowActivityService windowActivity,
        IWeatherService weatherSvc)
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
        _mediaLinkSvc = mediaLinkSvc;
        _hotkeyMgmt = hotkeyMgmt;
        _windowActivity = windowActivity;
        _weatherSvc = weatherSvc;
    }

    public void PersistAllState()
    {
        try
        {
            // Sync runtime state -> settings before saving
            _intSettingsProvider.Value.SavedSortOrder = _integrationDisplay.IntegrationSortOrder;
            _intSettingsProvider.Save();

            _trkSettingsProvider.Value.SavedDevices = _trackerDisplay.TrackerDevices;
            _trkSettingsProvider.Save();
            _appHistorySvc.Value.SaveAppHistory();
            _mediaLinkSvc.Value.SaveMediaSessions();
            _mediaLinkSvc.Value.SaveSeekbarStyles();
            _hotkeyMgmt.SaveHotkeyConfigurations();
            _windowActivity.SaveSettings();
            _weatherSvc.SaveSettings();
            _mediaLinkSettingsProvider.Save();
            _oscSettingsProvider.Save();
            _chatSettingsProvider.Save();
            _openAISettingsProvider.Save();
            _timeSettingsProvider.Save();
            _ttsSettingsProvider.Save();
            _appSettingsProvider.Save();
            _intSettingsProvider.Save();

            foreach (var module in _modules.Value.AllModules)
            {
                try { module.SaveSettings(); }
                catch (Exception ex) { Logging.WriteInfo($"Error saving {module.Name}: {ex.Message}"); }
            }

            _modules.Value.Whisper?.OnApplicationClosing();
            _modules.Value.Afk?.OnApplicationClosing();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false, exitapp: true);
        }
    }

    public async Task PrepareForShutdownAsync()
    {
        try
        {
            await _oscSender.Value.SentClearMessage(1500);
            PersistAllState();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: true, exitapp: true);
        }
    }
}
