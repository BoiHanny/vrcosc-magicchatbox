using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class AvatarPresetSettings : VersionedSettings
{
    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<AvatarPreset> _presets = new();

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<AvatarPresetValue> _globals = new();

    [ObservableProperty] private bool _applyGlobalsOnAvatarChange;
}
