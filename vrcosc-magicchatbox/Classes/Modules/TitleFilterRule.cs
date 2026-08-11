using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class TitleFilterRule : ObservableObject
{
    [ObservableProperty] private string _pattern = string.Empty;

    [ObservableProperty] private FilterMode _mode = FilterMode.Exclude;

    [ObservableProperty] private bool _isEnabled = true;

    public static FilterMode[] FilterModes { get; } = Enum.GetValues<FilterMode>();
}

public enum FilterMode
{
    [Description("Exclude (hide when matches)")]
    Exclude,

    [Description("Include (show only when matches)")]
    Include,

    [Description("Remove matches")]
    Remove
}
