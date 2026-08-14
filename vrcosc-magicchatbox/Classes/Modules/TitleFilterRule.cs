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

/// <summary>
/// What a title rule does when it matches. The descriptions are what the user reads, and they are
/// worded to match the per-app dropdown in the same section word for word - the two lists offer the
/// same three choices and used to name them differently.
/// </summary>
public enum FilterMode
{
    [Description("Hide the whole title")]
    Exclude,

    [Description("Only show titles that match")]
    Include,

    [Description("Cut out the matching words")]
    Remove
}
