using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// OSC connection settings, including primary and secondary output addresses and ports.
/// </summary>
public partial class OscSettings : VersionedSettings
{
    [ObservableProperty] private string _oscIP = "127.0.0.1";
    [ObservableProperty] private int _oscPortOut = 9000;
    [ObservableProperty] private int _oscPortIn = 9001;
    [ObservableProperty] private bool _secOSC = false;
    [ObservableProperty] private string _secOSCIP = "127.0.0.1";
    [ObservableProperty] private int _secOSCPort = 9002;
    [ObservableProperty] private bool _thirdOSC = false;
    [ObservableProperty] private string _thirdOSCIP = "127.0.0.1";
    [ObservableProperty] private int _thirdOSCPort = 9003;
    [ObservableProperty] private bool _unmuteMainOutput = true;
    [ObservableProperty] private bool _unmuteSecOutput = false;
    [ObservableProperty] private bool _unmuteThirdOutput = false;
}
