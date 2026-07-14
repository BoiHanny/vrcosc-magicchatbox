using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Runtime UI state for the Spotify integration card, widget, and options preview.
/// </summary>
public partial class SpotifyDisplayState : ObservableObject
{
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private bool _needsReconnect;
    [ObservableProperty] private bool _hasPlayback;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isLiked;
    [ObservableProperty] private bool _isShuffleOn;
    [ObservableProperty] private bool _isExplicit;
    [ObservableProperty] private string _repeatState = "off";
    [ObservableProperty] private string _trackId = string.Empty;
    [ObservableProperty] private string _trackUri = string.Empty;
    [ObservableProperty] private string _externalUrl = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string _deviceName = string.Empty;
    [ObservableProperty] private bool _hasVolume;
    [ObservableProperty] private int _volumePercent;
    [ObservableProperty] private int _progressMs;
    [ObservableProperty] private DateTime _progressUpdatedUtc = DateTime.MinValue;
    [ObservableProperty] private int _durationMs;
    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private string _queuePreview = string.Empty;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private string _lastSyncDisplay = "Last sync: Never";
    [ObservableProperty] private string _outputPreview = string.Empty;
    [ObservableProperty] private DateTime _lastSyncUtc = DateTime.MinValue;

    public bool HasTrack => !string.IsNullOrWhiteSpace(TrackId);
    public bool CanOpenSpotify => !string.IsNullOrWhiteSpace(ExternalUrl);
    public bool IsPaused => HasPlayback && !IsPlaying;
    public int LiveProgressMs
    {
        get
        {
            if (!HasPlayback)
                return 0;

            int progress = Math.Max(0, ProgressMs);
            if (IsPlaying && ProgressUpdatedUtc > DateTime.MinValue)
            {
                var elapsed = DateTime.UtcNow - ProgressUpdatedUtc;
                if (elapsed > TimeSpan.Zero)
                    progress += (int)Math.Min(elapsed.TotalMilliseconds, int.MaxValue - progress);
            }

            return DurationMs > 0
                ? Math.Clamp(progress, 0, DurationMs)
                : progress;
        }
    }

    public double ProgressPercent => DurationMs <= 0 ? 0 : Math.Clamp((double)LiveProgressMs / DurationMs * 100d, 0d, 100d);
    public string ProgressDisplay => DurationMs <= 0 ? string.Empty : $"{FormatMs(LiveProgressMs)} / {FormatMs(DurationMs)}";
    public string LikedIcon => IsLiked ? "♥" : "♡";
    public string ShuffleIcon => IsShuffleOn ? "🔀" : string.Empty;
    public string RepeatIcon => RepeatState switch
    {
        "context" => "🔁",
        "track" => "🔂",
        _ => string.Empty
    };

    partial void OnTrackIdChanged(string value)
    {
        OnPropertyChanged(nameof(HasTrack));
    }

    partial void OnExternalUrlChanged(string value)
    {
        OnPropertyChanged(nameof(CanOpenSpotify));
    }

    partial void OnHasPlaybackChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPaused));
        NotifyProgressDisplayChanged();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPaused));
        NotifyProgressDisplayChanged();
    }

    partial void OnIsLikedChanged(bool value)
    {
        OnPropertyChanged(nameof(LikedIcon));
    }

    partial void OnIsShuffleOnChanged(bool value)
    {
        OnPropertyChanged(nameof(ShuffleIcon));
    }

    partial void OnRepeatStateChanged(string value)
    {
        OnPropertyChanged(nameof(RepeatIcon));
    }

    partial void OnProgressMsChanged(int value)
    {
        NotifyProgressDisplayChanged();
    }

    partial void OnProgressUpdatedUtcChanged(DateTime value)
    {
        NotifyProgressDisplayChanged();
    }

    partial void OnDurationMsChanged(int value)
    {
        NotifyProgressDisplayChanged();
    }

    public void NotifyProgressDisplayChanged()
    {
        OnPropertyChanged(nameof(LiveProgressMs));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressDisplay));
    }

    public void ClearPlayback(string statusText)
    {
        HasPlayback = false;
        IsPlaying = false;
        IsLiked = false;
        IsShuffleOn = false;
        IsExplicit = false;
        RepeatState = "off";
        TrackId = string.Empty;
        TrackUri = string.Empty;
        ExternalUrl = string.Empty;
        Title = string.Empty;
        Artist = string.Empty;
        Album = string.Empty;
        DeviceName = string.Empty;
        HasVolume = false;
        VolumePercent = 0;
        ProgressMs = 0;
        ProgressUpdatedUtc = DateTime.MinValue;
        DurationMs = 0;
        QueuePreview = string.Empty;
        StatusText = statusText;
    }

    private static string FormatMs(int milliseconds)
    {
        if (milliseconds <= 0)
            return "0:00";

        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
