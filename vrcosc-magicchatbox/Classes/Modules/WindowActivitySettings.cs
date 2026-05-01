using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Settings for the window activity module, including title display, private app names, and focus tracking.
/// </summary>
public partial class WindowActivitySettings : VersionedSettings
{
    [ObservableProperty] private bool _autoShowTitleOnNewApp = false;
    [ObservableProperty] private bool _titleScan = true;
    [ObservableProperty] private int _maxShowTitleCount = 35;
    [ObservableProperty] private bool _limitTitleOnApp = true;
    [ObservableProperty] private bool _titleOnAppVR = false;
    [ObservableProperty] private string _privateName = "\U0001f512 App";
    [ObservableProperty] private string _privateNameVR = "\U0001f512 App";

    [ObservableProperty] private string _vrTitle = "In VR";
    [ObservableProperty] private string _vrFocusTitle = "\u1da0\u1d52\u1d9c\u1d58\u02e2\u02e2\u2071\u207f\u1d4d \u2071\u207f";
    [ObservableProperty] private string _desktopTitle = "On desktop";
    [ObservableProperty] private string _desktopFocusTitle = "\u2071\u207f";
    [ObservableProperty] private bool _showFocusedApp = true;
    [ObservableProperty] private bool _applicationHookV2 = true;

    /// <summary>Show the custom regex column in the scanned apps list.</summary>
    [ObservableProperty] private bool _showRegexColumn = false;

    /// <summary>Apply a global regex to ALL window titles before per-app regex.</summary>
    [ObservableProperty] private bool _useGlobalRegex = true;

    /// <summary>Global regex pattern applied to every window title. First capture group becomes the title.</summary>
    [ObservableProperty] private string _globalRegex = @"^(.+?)(?:\s*[-–—]\s*[^-–—]+)?$";
}
