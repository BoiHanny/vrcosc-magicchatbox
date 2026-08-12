using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class EncounterRecord : ObservableObject
{
    [ObservableProperty] private string _playerName = string.Empty;

    [ObservableProperty] private string _lastWorldName = string.Empty;

    [ObservableProperty] private int _timesSeenThisSession;

    [ObservableProperty] private double _totalTimeTogetherSeconds;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public bool IsCurrentlyPresent { get; set; }

    public DateTime? CurrentRoomJoinedAt { get; set; }

    public string TimeTogetherFormatted
    {
        get
        {
            var total = TotalTimeTogetherSeconds;
            if (IsCurrentlyPresent && CurrentRoomJoinedAt.HasValue)
                total += (DateTime.UtcNow - CurrentRoomJoinedAt.Value).TotalSeconds;

            var ts = TimeSpan.FromSeconds(total);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
