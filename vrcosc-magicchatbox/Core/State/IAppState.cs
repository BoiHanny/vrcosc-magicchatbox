using System.ComponentModel;

namespace vrcosc_magicchatbox.Core.State;

public interface IAppState : INotifyPropertyChanged
{
    bool MasterSwitch { get; set; }
    bool IsVRRunning { get; set; }
    bool BussyBoysMode { get; set; }
    bool Egg_Dev { get; set; }
    bool PulsoidAuthConnected { get; set; }
    int MainWindowBlurEffect { get; set; }
}
