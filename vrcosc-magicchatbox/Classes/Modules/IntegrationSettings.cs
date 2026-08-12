using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

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
    [ObservableProperty] private bool _intgrTikTokLive = false;
    [ObservableProperty] private bool _intgrDiscord = false;
    [ObservableProperty] private bool _intgrSpotify = false;
    [ObservableProperty] private bool _intgrVrcRadar = false;
    [ObservableProperty] private bool _intgrTrackerBattery = false;
    [ObservableProperty] private bool _intgrVrPerformance = false;
    // IntgrLyrics is the master: is the lyrics module running at all. Which players it follows is
    // the two flags below, one per card, so switching lyrics off on MediaLink leaves Spotify alone.
    // Both default false and are reconciled against the master on load - see LyricsSourceSelection.
    [ObservableProperty] private bool _intgrLyrics = false;
    [ObservableProperty] private bool _intgrLyrics_Spotify = false;
    [ObservableProperty] private bool _intgrLyrics_MediaLink = false;
    [ObservableProperty] private bool _intgrLyrics_VR = true;
    [ObservableProperty] private bool _intgrLyrics_DESKTOP = true;

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

    [ObservableProperty] private bool _intgrTikTokLive_VR = true;
    [ObservableProperty] private bool _intgrTikTokLive_DESKTOP = true;

    [ObservableProperty] private bool _intgrDiscord_VR = true;
    [ObservableProperty] private bool _intgrDiscord_DESKTOP = true;

    [ObservableProperty] private bool _intgrSpotify_VR = true;
    [ObservableProperty] private bool _intgrSpotify_DESKTOP = true;

    [ObservableProperty] private bool _intgrVrcRadar_VR = true;
    [ObservableProperty] private bool _intgrVrcRadar_DESKTOP = true;

    [ObservableProperty]
    private ObservableCollection<string> _savedSortOrder = new(IntegrationDisplayState.DefaultSortOrder);

    // Tiles the user has hidden from the Integrations page. Purely visual: nothing here starts or stops an
    // integration. Stored verbatim so an unrecognised key from another build survives a round trip;
    // IntegrationTileCatalog.ResolveHidden filters at the point of use.
    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<string> _hiddenTiles = new();

    [ObservableProperty] private bool _tileHideHintShown = false;

    // Collapses the hidden-tiles strip down to a single pill.
    [ObservableProperty] private bool _hiddenStripCollapsed = false;

    [JsonIgnore]
    [ObservableProperty] private bool _intgrScanForce = true;
}
