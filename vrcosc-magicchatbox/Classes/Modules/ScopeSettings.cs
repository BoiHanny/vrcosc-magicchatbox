using CommunityToolkit.Mvvm.ComponentModel;
using MagicChatbox.Scope;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class ScopeSettings : VersionedSettings
{
    [ObservableProperty] private bool _enabled = true;

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<ScopeRule> _rules = new();

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<AvatarGroup> _avatarGroups = new();

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<WorldGroup> _worldGroups = new();
}
