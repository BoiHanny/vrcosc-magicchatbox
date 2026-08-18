using CommunityToolkit.Mvvm.ComponentModel;
using System;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;

namespace vrcosc_magicchatbox.ViewModels.State;

public sealed partial class LyricsDisplayState : ObservableObject
{
    [ObservableProperty] private bool _hasTrack;
    [ObservableProperty] private bool _isShowingLine;
    [ObservableProperty] private string _currentLine = string.Empty;
    [ObservableProperty] private string _nowPlaying = string.Empty;
    [ObservableProperty] private string _providerName = string.Empty;
    [ObservableProperty] private string _statusText = "Not started";
    [ObservableProperty] private string _positionSource = string.Empty;
    [ObservableProperty] private int _lineCount;
    [ObservableProperty] private DateTime _lastLookupUtc = DateTime.MinValue;
    [ObservableProperty] private bool _showOnMediaLinkCard;
    [ObservableProperty] private bool _showOnSpotifyCard;

    public void Attach(LyricsCardPlacement placement)
    {
        ShowOnMediaLinkCard = placement.OnMediaLinkCard;
        ShowOnSpotifyCard = placement.OnSpotifyCard;
    }

    public bool SuppressMediaTitle { get; set; }

    private object _cursor = LyricCursor.None;

    public LyricCursor Cursor
    {
        get => (LyricCursor)System.Threading.Volatile.Read(ref _cursor);
        set => System.Threading.Volatile.Write(ref _cursor, value);
    }

    public TimeSpan Position { get; set; }

    public void Reset(string statusText)
    {
        HasTrack = false;
        IsShowingLine = false;
        Cursor = LyricCursor.None;
        Position = TimeSpan.Zero;
        CurrentLine = string.Empty;
        NowPlaying = string.Empty;
        ProviderName = string.Empty;
        PositionSource = string.Empty;
        LineCount = 0;
        SuppressMediaTitle = false;
        StatusText = statusText;
        Attach(LyricsCardPlacement.Nowhere);
    }
}
