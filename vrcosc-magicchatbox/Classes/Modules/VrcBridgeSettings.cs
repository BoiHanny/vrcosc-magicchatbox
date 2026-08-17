using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class VrcBridgeSettings : VersionedSettings
{
    [ObservableProperty] private bool _enableBridge = false;

    [ObservableProperty] private bool _enableParameterInput = false;

    [ObservableProperty] private bool _mirrorToLegacyOsc = false;

    [ObservableProperty] private bool _enableAvatarConfig = false;

    [ObservableProperty] private int _oscReceivePort = 0;

    [ObservableProperty] private string _vrchatPeerPrefix = "VRChat-Client-";

    [ObservableProperty] private bool _muteInPublicInstances = false;

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<string> _mutedWorlds = new();

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<string> _blockedTerms = new();
}
