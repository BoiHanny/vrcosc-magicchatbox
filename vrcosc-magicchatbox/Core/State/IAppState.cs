using System.ComponentModel;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.State;

public interface IAppState : INotifyPropertyChanged
{
    bool MasterSwitch { get; set; }
    bool IsVRRunning { get; set; }
    bool BussyBoysMode { get; set; }
    bool Egg_Dev { get; set; }

    bool PulsoidAuthConnected { get; set; }

    PulsoidAuthState PulsoidAuthState { get; set; }

    int MainWindowBlurEffect { get; set; }
}
