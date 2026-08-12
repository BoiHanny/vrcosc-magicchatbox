using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules.Vr;

public enum VrPerformanceDisplayMode
{
    [Description("Always")]
    Always = 0,

    [Description("Only when frames drop")]
    OnlyWhenDegraded = 1,

    [Description("Compact, expand on trouble")]
    CompactThenExpand = 2,
}

public partial class VrPerformanceSettings : VersionedSettings
{
    [ObservableProperty] private bool _showFps = true;
    [ObservableProperty] private bool _showTargetHz = false;
    [ObservableProperty] private bool _showReprojection = true;
    [ObservableProperty] private bool _showDroppedFrames = false;
    [ObservableProperty] private bool _showMotionSmoothing = false;
    [ObservableProperty] private bool _showAppGpuMs = false;
    [ObservableProperty] private bool _showCompositorGpuMs = false;
    [ObservableProperty] private bool _showHeadroom = false;
    [ObservableProperty] private bool _showCpuTiming = false;

    [ObservableProperty] private bool _useEmojisForVrPerf = true;

    [ObservableProperty] private bool _useSuperscriptUnits = true;

    [ObservableProperty] private string _statsSeparator = " ¦ ";
    [ObservableProperty] private bool _removeNumberTrailing = false;

    [ObservableProperty] private VrPerformanceDisplayMode _displayMode = VrPerformanceDisplayMode.Always;

    [ObservableProperty] private double _degradedReprojectionPercent = 10;

    [ObservableProperty] private double _degradedDroppedPerMinute = 5;

    [ObservableProperty] private double _degradedFpsPercentOfTarget = 90;

    [ObservableProperty] private int _degradedHysteresisSeconds = 5;
}
