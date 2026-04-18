using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns Pulsoid connection status display.
/// Extracted from ViewModel to isolate heart-rate runtime display concerns.
/// </summary>
public sealed partial class PulsoidDisplayState : ObservableObject
{
    private bool _authConnected = false;
    public bool AuthConnected
    {
        get => _authConnected;
        set
        {
            if (_authConnected != value)
            {
                _authConnected = value;
                OnPropertyChanged();
            }
        }
    }
}
