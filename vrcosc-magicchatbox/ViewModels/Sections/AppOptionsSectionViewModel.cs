using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Net.Http;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for App Options (general settings).
/// Owns OSC reset, rollback, update-by-zip, folder open, and favorites reset.
/// Complete binding surface for AppOptionsSection.xaml.
/// </summary>
public partial class AppOptionsSectionViewModel : ObservableObject
{
    private readonly IEnvironmentService _env;
    private readonly IAppHistoryService _appHistorySvc;
    private readonly AppUpdateState _updateState;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly Lazy<IStatusListService> _statusListSvc;
    private readonly IMenuNavigationService _menuNav;
    private readonly INavigationService _nav;

    public AppSettings AppSettings { get; }
    public TtsSettings TtsSettings { get; }
    public OscSettings OscSettings { get; }
    public AppUpdateState UpdateState => _updateState;
    public IEnvironmentService Environment => _env;
    public ISettingsProvider<IntegrationSettings> IntegrationSettingsProvider { get; }

    /// <summary>
    /// Initializes the app-options section ViewModel with navigation, update, settings,
    /// and all required module and service dependencies.
    /// </summary>
    public AppOptionsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TtsSettings> ttsSettingsProvider,
        ISettingsProvider<OscSettings> oscSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IEnvironmentService env,
        IAppHistoryService appHistorySvc,
        AppUpdateState updateState,
        IHttpClientFactory httpClientFactory,
        IUiDispatcher dispatcher,
        Lazy<IStatusListService> statusListSvc,
        IMenuNavigationService menuNav,
        INavigationService nav)
    {
        AppSettings = appSettingsProvider.Value;
        TtsSettings = ttsSettingsProvider.Value;
        OscSettings = oscSettingsProvider.Value;
        IntegrationSettingsProvider = integrationSettingsProvider;
        _env = env;
        _appHistorySvc = appHistorySvc;
        _updateState = updateState;
        _httpClientFactory = httpClientFactory;
        _dispatcher = dispatcher;
        _statusListSvc = statusListSvc;
        _menuNav = menuNav;
        _nav = nav;
    }

    [RelayCommand]
    private void ResetOscIp() => OscSettings.OscIP = "127.0.0.1";

    [RelayCommand]
    private void ResetOscPort() => OscSettings.OscPortOut = 9000;

    [RelayCommand]
    private void Rollback()
    {
        if (AppSettings.UseCustomProfile)
        {
            Logging.WriteException(new Exception("Cannot rollback while using a custom profile."), MSGBox: true);
            return;
        }
        new UpdateApp(_updateState, _httpClientFactory, _dispatcher, true).StartRollback();
    }

    [RelayCommand]
    private void UpdateByZip()
    {
        if (AppSettings.UseCustomProfile)
        {
            Logging.WriteException(new Exception("Cannot update by zip while using a custom profile."), MSGBox: true);
            return;
        }
        new UpdateApp(_updateState, _httpClientFactory, _dispatcher, true).SelectCustomZip();
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        _appHistorySvc.CreateIfMissing(_env.DataPath);
        _nav.OpenFolder(_env.DataPath);
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        _appHistorySvc.CreateIfMissing(_env.LogPath);
        _nav.OpenFolder(_env.LogPath);
    }

    [RelayCommand]
    private void ResetFavorites()
    {
        string dataPath = _env.DataPath;
        string json = Path.Combine(dataPath, "StatusList.json");
        string legacy = Path.Combine(dataPath, "StatusList.xml");
        if (File.Exists(json)) File.Delete(json);
        if (File.Exists(legacy)) File.Delete(legacy);
        _statusListSvc.Value.LoadStatusList();
        _menuNav.NavigateToPage(1); // Navigate to Status page
    }
}
