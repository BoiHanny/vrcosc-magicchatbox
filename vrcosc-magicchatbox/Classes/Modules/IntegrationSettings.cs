using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted integration-enable flags and per-integration VR/desktop visibility toggles.
/// </summary>
public partial class IntegrationSettings : VersionedSettings
{
    [ObservableProperty] private bool _intgrStatus = true;
    [ObservableProperty] private bool _intgrScanWindowActivity = false;
    [ObservableProperty] private bool _intgrScanSpotify_OLD = false;
    [ObservableProperty] private bool _intgrScanWindowTime = true;
    [ObservableProperty] private bool _applicationHookV2 = true;
    [ObservableProperty] private bool _intgrHeartRate = false;
    [ObservableProperty] private bool _intgrNetworkStatistics = false;
    [ObservableProperty] private bool _intgrScanMediaLink = true;
    [ObservableProperty] private bool _intgrComponentStats = false;
    [ObservableProperty] private bool _intgrSoundpad = false;
    [ObservableProperty] private bool _intgrTwitch = false;
    [ObservableProperty] private bool _intgrDiscord = false;
    [ObservableProperty] private bool _intgrVrcRadar = false;
    [ObservableProperty] private bool _intgrTrackerBattery = false;

    [ObservableProperty] private bool _intgrComponentStats_VR = true;
    [ObservableProperty] private bool _intgrComponentStats_DESKTOP = false;

    [ObservableProperty] private bool _intgrNetworkStatistics_VR = false;
    [ObservableProperty] private bool _intgrNetworkStatistics_DESKTOP = true;

    [ObservableProperty] private bool _intgrStatus_VR = true;
    [ObservableProperty] private bool _intgrStatus_DESKTOP = true;

    [ObservableProperty] private bool _intgrMediaLink_VR = true;
    [ObservableProperty] private bool _intgrMediaLink_DESKTOP = true;

    [ObservableProperty] private bool _intgrWindowActivity_VR = false;
    [ObservableProperty] private bool _intgrWindowActivity_DESKTOP = true;

    [ObservableProperty] private bool _intgrHeartRate_VR = true;
    [ObservableProperty] private bool _intgrHeartRate_DESKTOP = false;
    [ObservableProperty] private bool _intgrHeartRate_OSC = false;

    [ObservableProperty] private bool _intgrCurrentTime_VR = true;
    [ObservableProperty] private bool _intgrCurrentTime_DESKTOP = false;

    [ObservableProperty] private bool _intgrWeather_VR = true;
    [ObservableProperty] private bool _intgrWeather_DESKTOP = false;

    [ObservableProperty] private bool _intgrSpotifyStatus_VR = true;
    [ObservableProperty] private bool _intgrSpotifyStatus_DESKTOP = true;

    [ObservableProperty] private bool _intgrSoundpad_VR = false;
    [ObservableProperty] private bool _intgrSoundpad_DESKTOP = true;

    [ObservableProperty] private bool _intgrTwitch_VR = true;
    [ObservableProperty] private bool _intgrTwitch_DESKTOP = true;

    [ObservableProperty] private bool _intgrDiscord_VR = true;
    [ObservableProperty] private bool _intgrDiscord_DESKTOP = true;

    [ObservableProperty] private bool _intgrVrcRadar_VR = true;
    [ObservableProperty] private bool _intgrVrcRadar_DESKTOP = true;

    /// <summary>
    /// Persisted integration sort order — restored across restarts.
    /// At runtime this is copied into IntegrationDisplayState.IntegrationSortOrder.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _savedSortOrder = new(IntegrationDisplayState.DefaultSortOrder);

    [JsonIgnore]
    [ObservableProperty] private bool _intgrScanForce = true;
}
