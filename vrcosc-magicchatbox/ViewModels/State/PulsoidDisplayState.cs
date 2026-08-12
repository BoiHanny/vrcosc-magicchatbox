using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

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
