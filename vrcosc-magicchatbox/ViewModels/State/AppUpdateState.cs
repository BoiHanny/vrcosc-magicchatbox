using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Runtime state for application version checking and update UI.
/// Written by VersionService.CompareVersions(), UpdateApp.
/// Read by MainWindow, AppOptionsSection, ApplicationError, StartUp.
/// </summary>
public partial class AppUpdateState : ObservableObject
{
    [ObservableProperty] private string _versionTxt = "Check for updates";
    [ObservableProperty] private string _versionTxtColor = "#FF8F80B9";
    [ObservableProperty] private bool _versionTxtUnderLine;
    [ObservableProperty] private bool _canUpdate;
    [ObservableProperty] private bool _canUpdateLabel;
    [ObservableProperty] private string _updateStatustxt = string.Empty;
    [ObservableProperty] private string _updateURL = string.Empty;
    [ObservableProperty] private string _latestReleaseURL = string.Empty;
    [ObservableProperty] private string _preReleaseURL = string.Empty;
    [ObservableProperty] private string _tagURL = string.Empty;
    [ObservableProperty] private string _appLocation = string.Empty;
    [ObservableProperty] private bool _rollBackUpdateAvailable;
    [ObservableProperty] private System.Version _rollBackVersion = new(0, 0, 0, 0);

    private Models.Version _appVersion;
    public Models.Version AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    private Models.Version _gitHubVersion;
    public Models.Version GitHubVersion
    {
        get => _gitHubVersion;
        set => SetProperty(ref _gitHubVersion, value);
    }

    private Models.Version _preReleaseVersion;
    public Models.Version PreReleaseVersion
    {
        get => _preReleaseVersion;
        set => SetProperty(ref _preReleaseVersion, value);
    }

    private Models.Version _latestReleaseVersion;
    public Models.Version LatestReleaseVersion
    {
        get => _latestReleaseVersion;
        set => SetProperty(ref _latestReleaseVersion, value);
    }
}
