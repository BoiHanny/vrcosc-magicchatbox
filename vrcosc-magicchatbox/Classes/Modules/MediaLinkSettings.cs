using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the MediaLink media-session display module.
/// </summary>
public partial class MediaLinkSettings : VersionedSettings
{
    public static IEnumerable<MediaLinkTimeSeekbar> AvailableTimeSeekbarStyles { get; } = Enum.GetValues(typeof(MediaLinkTimeSeekbar)).Cast<MediaLinkTimeSeekbar>().ToList();

    [ObservableProperty] private bool _showOnlyOnChange = false;
    [ObservableProperty] private string _iconPlay = "";
    [ObservableProperty] private string _iconPause = "⏸";
    [ObservableProperty] private string _iconStop = "⏹️";
    [ObservableProperty] private bool _showStopIcon = true;
    [ObservableProperty] private string _separator = " ᵇʸ ";
    [ObservableProperty] private string _textPlaying = "Listening to";
    [ObservableProperty] private string _textPaused = "Paused";
    [ObservableProperty] private bool _upperCase = false;
    [ObservableProperty] private bool _pauseIconMusic = true;
    [ObservableProperty] private MediaLinkTimeSeekbar _timeSeekStyle = MediaLinkTimeSeekbar.SmallNumbers;
    [ObservableProperty] private bool _autoDowngradeSeekbar = true;

    [ObservableProperty] private bool _autoSwitch = true;
    [ObservableProperty] private bool _autoSwitchSpawn = true;
    [ObservableProperty] private int _sessionTimeout = 3;

    // Legacy field kept for JSON deserialization backward-compat; no longer drives behavior.
    [ObservableProperty] private bool _disabled = false;

    private double _transientDuration = 25.0;
    public double TransientDuration
    {
        get => _transientDuration;
        set
        {
            if (value < 0) value = 0;
            if (SetProperty(ref _transientDuration, value)) { }
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public bool TimeSeekStyleIsNumbersAndSeekBar => TimeSeekStyle == MediaLinkTimeSeekbar.NumbersAndSeekBar;

    [Newtonsoft.Json.JsonIgnore]
    public bool TimeSeekStyleIsNone => TimeSeekStyle == MediaLinkTimeSeekbar.None;

    partial void OnTimeSeekStyleChanged(MediaLinkTimeSeekbar value)
    {
        OnPropertyChanged(nameof(TimeSeekStyleIsNumbersAndSeekBar));
        OnPropertyChanged(nameof(TimeSeekStyleIsNone));
    }
}
