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
    [Description("Hide the whole title")]
    Exclude,

    [Description("Only show titles that match")]
    Include,

    [Description("Cut out the matching words")]
    Remove
}
