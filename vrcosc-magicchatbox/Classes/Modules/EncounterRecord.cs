using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Tracks a single player encounter within the current session.
/// Session-scoped: cleared when the radar session ends.
/// </summary>
public partial class EncounterRecord : ObservableObject
{
    /// <summary>VRChat display name of the player.</summary>
    [ObservableProperty] private string _playerName = string.Empty;

    /// <summary>World name where the player was last seen.</summary>
    [ObservableProperty] private string _lastWorldName = string.Empty;

    /// <summary>Number of times this player was seen joining (across worlds).</summary>
    [ObservableProperty] private int _timesSeenThisSession;

    /// <summary>Total seconds spent together (across all worlds in this session).</summary>
    [ObservableProperty] private double _totalTimeTogetherSeconds;

    /// <summary>When this player was first encountered this session.</summary>
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this player was last seen joining or present.</summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the player is currently in the same world.</summary>
    public bool IsCurrentlyPresent { get; set; }

    /// <summary>Timestamp when the player joined the current room (for calculating active time).</summary>
    public DateTime? CurrentRoomJoinedAt { get; set; }

    /// <summary>Formatted time together for display.</summary>
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
