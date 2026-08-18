using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Core.Osc;

namespace vrcosc_magicchatbox.ViewModels.State;

public partial class OscDisplayState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string _oscToSent = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fill))]
    private int _oscMsgCount;

    [ObservableProperty] private string _oscMsgCountUI = string.Empty;

    public OscPreviewFill Fill => OscPreviewFillLevel.Classify(OscMsgCount);

    public bool HasPreview => !string.IsNullOrWhiteSpace(OscToSent);

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
