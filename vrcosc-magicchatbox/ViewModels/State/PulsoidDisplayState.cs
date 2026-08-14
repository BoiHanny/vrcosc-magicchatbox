using CommunityToolkit.Mvvm.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Why the Pulsoid sign-in is (or is not) usable right now.
/// This enum is the single source of truth the UI and the OSC gate read; the older boolean
/// <see cref="PulsoidDisplayState.AuthConnected"/> is derived from it, so a transient outage
/// can no longer masquerade as "signed out".
/// </summary>
public enum PulsoidAuthState
{
    /// <summary>No token is stored: never connected, or the user pressed Disconnect.</summary>
    NoToken,

    /// <summary>A stored token that Pulsoid has accepted (validated, or a live socket).</summary>
    Authenticated,

    /// <summary>A stored token that has not been checked yet this session. Counts as signed in.</summary>
    Unverified,

    /// <summary>A stored token we could not check because Pulsoid was unreachable. Counts as signed in.</summary>
    Unreachable,

    /// <summary>Pulsoid definitively refused the stored token (HTTP 401). Re-authentication required.</summary>
    Rejected,

    /// <summary>The stored token exists on disk but DPAPI could not decrypt it on this account.</summary>
    Unreadable
}

public sealed partial class PulsoidDisplayState : ObservableObject
{
    private PulsoidAuthState _authState = PulsoidAuthState.NoToken;

    /// <summary>
    /// The one value that decides whether the user is signed in to Pulsoid. Everything else
    /// (button visibility, the OSC enable gate, the status line) is derived from it.
    /// </summary>
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

    /// <summary>
    /// True whenever a stored credential is believed good. Deliberately true for
    /// <see cref="PulsoidAuthState.Unverified"/> and <see cref="PulsoidAuthState.Unreachable"/>:
    /// failing to reach Pulsoid is not evidence that the user is signed out.
    /// </summary>
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
        PulsoidAuthState.Unverified => "Signed in to Pulsoid — checking your token…",
        PulsoidAuthState.Unreachable => "Can't reach Pulsoid right now — your sign-in is kept and will keep retrying.",
        PulsoidAuthState.Rejected => "Pulsoid rejected the saved token. Please reconnect.",
        PulsoidAuthState.Unreadable => "The saved Pulsoid token could not be decrypted on this Windows account. Please reconnect.",
        _ => "Not connected to Pulsoid."
    };
}
