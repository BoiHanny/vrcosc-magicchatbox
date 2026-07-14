using System.ComponentModel;

namespace vrcosc_magicchatbox.Core.State;

/// <summary>
/// Cross-cutting application state used by modules and services.
/// Modules depend on this interface instead of ViewModel directly.
/// Kept intentionally slim — state containers are injected separately via DI.
/// For file paths, use IEnvironmentService instead.
/// </summary>
public interface IAppState : INotifyPropertyChanged
{
    bool MasterSwitch { get; set; }
    bool IsVRRunning { get; set; }
    bool BussyBoysMode { get; set; }
    bool Egg_Dev { get; set; }
    bool PulsoidAuthConnected { get; set; }
    int MainWindowBlurEffect { get; set; }
}
