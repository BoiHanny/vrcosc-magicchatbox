using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class WindowActivitySettings : VersionedSettings
{
    public const string LegacyDefaultGlobalRegex = @"^(.+?)(?:\s*[-–—]\s*[^-–—]+)?$";
    public const string DefaultGlobalRegex = @"^(.+?)(?:\s*[-–—]\s*[^-–—]+\s*[-–—]\s*(.+)|\s*[-–—]\s*[^-–—]+)?$ => $1 $2";

    [ObservableProperty] private bool _autoShowTitleOnNewApp = false;
    [ObservableProperty] private bool _titleScan = true;
    [ObservableProperty] private int _maxShowTitleCount = 35;
    [ObservableProperty] private bool _limitTitleOnApp = true;
    [ObservableProperty] private bool _titleOnAppVR = false;
    [ObservableProperty] private string _privateName = "\U0001f512 App";
    [ObservableProperty] private string _privateNameVR = "\U0001f512 App";
    [ObservableProperty] private bool _hideOutputWhenPrivateApp = false;

    [ObservableProperty] private string _vrTitle = "In VR";
    [ObservableProperty] private string _vrFocusTitle = "\u1da0\u1d52\u1d9c\u1d58\u02e2\u02e2\u2071\u207f\u1d4d \u2071\u207f";
    [ObservableProperty] private string _desktopTitle = "On desktop";
    [ObservableProperty] private string _desktopFocusTitle = "\u2071\u207f";
    [ObservableProperty] private bool _showFocusedApp = true;
    [ObservableProperty] private bool _applicationHookV2 = true;

    [ObservableProperty] private bool _showRegexColumn = false;

    [ObservableProperty] private bool _useGlobalRegex = true;

    [ObservableProperty] private string _globalRegex = DefaultGlobalRegex;

    [ObservableProperty] private bool _enableTitleFilters = false;

    public ObservableCollection<TitleFilterRule> TitleFilters { get; set; } = new();
}
