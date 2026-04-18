using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Runtime display state for the OSC output panel.
/// Owns the OSC preview string, message counts, and character limit display.
/// </summary>
public partial class OscDisplayState : ObservableObject
{
    [ObservableProperty] private string _oscToSent = string.Empty;
    [ObservableProperty] private int _oscMsgCount;
    [ObservableProperty] private string _oscMsgCountUI = string.Empty;
    [ObservableProperty] private string _charLimit = "Hidden";

    private DateTime _lastSwitchCycle = DateTime.Now;
    public DateTime LastSwitchCycle
    {
        get => _lastSwitchCycle;
        set
        {
            if (_lastSwitchCycle != value)
            {
                _lastSwitchCycle = value;
                OnPropertyChanged();
            }
        }
    }
}
