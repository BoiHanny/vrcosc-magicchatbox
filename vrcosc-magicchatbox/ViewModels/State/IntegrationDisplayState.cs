using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Runtime display state for the Integrations page.
/// Owns all opacity indicators, integration display strings,
/// and the IntegrationSortOrder collection.
/// Written by OSCController, ScanLoopService, TrackerBatteryModule, WeatherService.
/// </summary>
public partial class IntegrationDisplayState : ObservableObject
{
    public static readonly IReadOnlyList<string> DefaultSortOrder = new[]
    {
        "Status", "Window", "Twitch", "HeartRate", "Component",
        "TrackerBattery", "Network", "Weather", "Time", "Soundpad",
        "MediaLink", "Spotify"
    };

    private ObservableCollection<string> _integrationSortOrder = new(DefaultSortOrder);
    public ObservableCollection<string> IntegrationSortOrder
    {
        get => _integrationSortOrder;
        set
        {
            _integrationSortOrder = NormalizeSortOrder(value);
            OnPropertyChanged();
        }
    }

    public static ObservableCollection<string> NormalizeSortOrder(IEnumerable<string> order)
    {
        var canonicalMap = DefaultSortOrder
            .ToDictionary(key => key, StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (order != null)
        {
            foreach (var item in order)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                if (canonicalMap.TryGetValue(item, out var canonical) && seen.Add(canonical))
                    normalized.Add(canonical);
            }
        }

        for (int defaultIndex = 0; defaultIndex < DefaultSortOrder.Count; defaultIndex++)
        {
            string key = DefaultSortOrder[defaultIndex];
            if (seen.Contains(key)) continue;
            int insertIndex = FindInsertIndex(normalized, defaultIndex);
            normalized.Insert(insertIndex, key);
            seen.Add(key);
        }

        return new ObservableCollection<string>(normalized);
    }

    private static int FindInsertIndex(List<string> current, int defaultIndex)
    {
        for (int i = defaultIndex - 1; i >= 0; i--)
        {
            int position = current.FindIndex(item =>
                string.Equals(item, DefaultSortOrder[i], StringComparison.OrdinalIgnoreCase));
            if (position >= 0) return position + 1;
        }

        for (int i = defaultIndex + 1; i < DefaultSortOrder.Count; i++)
        {
            int position = current.FindIndex(item =>
                string.Equals(item, DefaultSortOrder[i], StringComparison.OrdinalIgnoreCase));
            if (position >= 0) return position;
        }

        return current.Count;
    }

    [ObservableProperty] private string _statusOpacity = "1";
    [ObservableProperty] private string _windowOpacity = "1";
    [ObservableProperty] private string _timeOpacity = "1";
    [ObservableProperty] private string _weatherOpacity = "1";
    [ObservableProperty] private string _twitchOpacity = "1";
    [ObservableProperty] private string _spotifyOpacity = "1";
    [ObservableProperty] private string _heartRateOpacity = "1";
    [ObservableProperty] private string _trackerBatteryOpacity = "1";
    [ObservableProperty] private string _componentStatOpacity = "1";
    [ObservableProperty] private string _networkStatsOpacity = "1";
    [ObservableProperty] private string _soundpadOpacity = "1";
    [ObservableProperty] private string _mediaLinkOpacity = "1";

    [ObservableProperty] private string _currentTime = string.Empty;
    [ObservableProperty] private string _weatherLastSyncDisplay = "Last sync: Never";
    [ObservableProperty] private string _trackerBatteryDeviceSummary = "0/0 connected";
    [ObservableProperty] private string _trackerBatteryLastScanDisplay = "Last scan: Never";
    [ObservableProperty] private string _trackerBatteryPreview = string.Empty;
    [ObservableProperty] private string _componentStatCombined = string.Empty;
    [ObservableProperty] private DateTime _componentStatsLastUpdate = DateTime.Now;
    [ObservableProperty] private bool _componentStatsRunning;

    [ObservableProperty] private string _playingSongTitle = string.Empty;
    [ObservableProperty] private bool _spotifyActive;
    [ObservableProperty] private bool _spotifyPaused;
    [ObservableProperty] private DateTime _spotifyLastChangeUtc = DateTime.UtcNow;

    /// <summary>
    /// Dictionary-style opacity setter used by OSCController.
    /// </summary>
    public void SetOpacity(string controlName, string opacity)
    {
        switch (controlName)
        {
            case "Status": StatusOpacity = opacity; break;
            case "Window": WindowOpacity = opacity; break;
            case "Time": TimeOpacity = opacity; break;
            case "Weather": WeatherOpacity = opacity; break;
            case "Twitch": TwitchOpacity = opacity; break;
            case "Spotify": SpotifyOpacity = opacity; break;
            case "HeartRate": HeartRateOpacity = opacity; break;
            case "TrackerBattery": TrackerBatteryOpacity = opacity; break;
            case "ComponentStat": ComponentStatOpacity = opacity; break;
            case "NetworkStatistics": NetworkStatsOpacity = opacity; break;
            case "Soundpad": SoundpadOpacity = opacity; break;
            case "MediaLink": MediaLinkOpacity = opacity; break;
        }
    }

    /// <summary>
    /// Resets all opacity values to fully visible.
    /// </summary>
    public void ResetAllOpacity()
    {
        StatusOpacity = "1";
        WindowOpacity = "1";
        TimeOpacity = "1";
        WeatherOpacity = "1";
        TwitchOpacity = "1";
        SpotifyOpacity = "1";
        HeartRateOpacity = "1";
        TrackerBatteryOpacity = "1";
        ComponentStatOpacity = "1";
        NetworkStatsOpacity = "1";
        SoundpadOpacity = "1";
        MediaLinkOpacity = "1";
    }
}
