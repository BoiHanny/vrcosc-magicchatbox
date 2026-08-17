using System.ComponentModel;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace MagicChatbox.Tests.TestDoubles;

// Plain settable state, so a test can watch what something did to it rather than standing up the real
// view model. It raises change notifications because the interface promises them and a subject under
// test is allowed to subscribe.
public sealed class FakeAppState : IAppState
{
    private bool _masterSwitch = true;
    private bool _isVrRunning;
    private bool _bussyBoysMode;
    private bool _eggDev;
    private bool _pulsoidAuthConnected;
    private PulsoidAuthState _pulsoidAuthState;
    private int _mainWindowBlurEffect;

    public bool MasterSwitch
    {
        get => _masterSwitch;
        set => Set(ref _masterSwitch, value, nameof(MasterSwitch));
    }

    public bool IsVRRunning
    {
        get => _isVrRunning;
        set => Set(ref _isVrRunning, value, nameof(IsVRRunning));
    }

    public bool BussyBoysMode
    {
        get => _bussyBoysMode;
        set => Set(ref _bussyBoysMode, value, nameof(BussyBoysMode));
    }

    public bool Egg_Dev
    {
        get => _eggDev;
        set => Set(ref _eggDev, value, nameof(Egg_Dev));
    }

    public bool PulsoidAuthConnected
    {
        get => _pulsoidAuthConnected;
        set => Set(ref _pulsoidAuthConnected, value, nameof(PulsoidAuthConnected));
    }

    public PulsoidAuthState PulsoidAuthState
    {
        get => _pulsoidAuthState;
        set => Set(ref _pulsoidAuthState, value, nameof(PulsoidAuthState));
    }

    public int MainWindowBlurEffect
    {
        get => _mainWindowBlurEffect;
        set => Set(ref _mainWindowBlurEffect, value, nameof(MainWindowBlurEffect));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, string name)
    {
        if (Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
