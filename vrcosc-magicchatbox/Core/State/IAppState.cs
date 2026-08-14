using System.ComponentModel;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.State;

public interface IAppState : INotifyPropertyChanged
{
    bool MasterSwitch { get; set; }
    bool IsVRRunning { get; set; }
    bool BussyBoysMode { get; set; }
    bool Egg_Dev { get; set; }

    /// <summary>Derived from <see cref="PulsoidAuthState"/>; kept for existing bindings and callers.</summary>
    bool PulsoidAuthConnected { get; set; }

    /// <summary>The single source of truth for the Pulsoid sign-in.</summary>
    PulsoidAuthState PulsoidAuthState { get; set; }

    int MainWindowBlurEffect { get; set; }
}
