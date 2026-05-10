using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// A rule that filters window title "extra info" text after regex extraction.
/// Supports include, hide, and remove modes.
/// </summary>
public partial class TitleFilterRule : ObservableObject
{
    /// <summary>The text pattern to match (case-insensitive substring).</summary>
    [ObservableProperty] private string _pattern = string.Empty;

    /// <summary>How this rule handles matching content.</summary>
    [ObservableProperty] private FilterMode _mode = FilterMode.Exclude;

    /// <summary>Whether this rule is currently active.</summary>
    [ObservableProperty] private bool _isEnabled = true;

    /// <summary>Static array of all FilterMode values for ComboBox binding.</summary>
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
