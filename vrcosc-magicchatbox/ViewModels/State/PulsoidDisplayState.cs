using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

public enum PulsoidAuthState
{
    NoToken,

    Authenticated,

    Unverified,

    Unreachable,

    Rejected,

    Unreadable
}

public sealed partial class PulsoidDisplayState : ObservableObject
{
    private PulsoidAuthState _authState = PulsoidAuthState.NoToken;

    public PulsoidAuthState AuthState
    {
        get => _authState;
        set
        {
            if (_authState == value)
                return;

            _authState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AuthConnected));
            OnPropertyChanged(nameof(AuthStatusText));
        }
    }

    public bool AuthConnected
    {
        get => _authState is PulsoidAuthState.Authenticated
                          or PulsoidAuthState.Unverified
                          or PulsoidAuthState.Unreachable;
        set => AuthState = value ? PulsoidAuthState.Authenticated : PulsoidAuthState.NoToken;
    }

    public string AuthStatusText => _authState switch
    {
        PulsoidAuthState.Authenticated => "Signed in to Pulsoid.",
        PulsoidAuthState.Unverified => "Signed in to Pulsoid with your saved token.",
        PulsoidAuthState.Unreachable => "Can't reach Pulsoid right now — your sign-in is kept and will keep retrying.",
        PulsoidAuthState.Rejected => "Pulsoid rejected the saved token. Please reconnect.",
        PulsoidAuthState.Unreadable => "The saved Pulsoid token could not be decrypted on this Windows account. Please reconnect.",
        _ => "Not connected to Pulsoid."
    };
}
