using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns tracker device lists for the tracker battery display.
/// Extracted from ViewModel to isolate tracker runtime display concerns.
/// </summary>
public sealed partial class TrackerDisplayState : ObservableObject
{
    private ObservableCollection<TrackerDevice> _trackerDevices = new();
    public ObservableCollection<TrackerDevice> TrackerDevices
    {
        get => _trackerDevices;
        set
        {
            _trackerDevices = value ?? new ObservableCollection<TrackerDevice>();
            TrackerBatteryModule.NormalizeLegacyIcons(_trackerDevices);
            OnPropertyChanged();
        }
    }

    private ObservableCollection<TrackerDevice> _trackerBatteryActiveDevices = new();
    public ObservableCollection<TrackerDevice> TrackerBatteryActiveDevices
    {
        get => _trackerBatteryActiveDevices;
        set
        {
            _trackerBatteryActiveDevices = value ?? new ObservableCollection<TrackerDevice>();
            OnPropertyChanged();
        }
    }
}
