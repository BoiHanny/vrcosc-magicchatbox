using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the network statistics display module.
/// </summary>
public partial class NetworkStatsSettings : VersionedSettings
{
    [ObservableProperty] private bool _showCurrentDown = true;
    [ObservableProperty] private bool _showCurrentUp = false;
    [ObservableProperty] private bool _showMaxDown = false;
    [ObservableProperty] private bool _showMaxUp = false;
    [ObservableProperty] private bool _showTotalDown = false;
    [ObservableProperty] private bool _showTotalUp = false;
    [ObservableProperty] private bool _showNetworkUtilization = true;
    [ObservableProperty] private bool _useInterfaceMaxSpeed = false;
    [ObservableProperty] private bool _styledCharacters = true;
}
