using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public enum LyricsMediaCoexistence
{
    [Description("Show both")]
    SideBySide = 0,

    [Description("Hide the song title while a lyric shows")]
    PreferLyrics = 1,
}

public partial class LyricsSettings : VersionedSettings
{
    // Only new installs feel this. An existing settings file already holds an explicit OffsetMs, so
    // anyone who had tuned it - or deliberately left it at zero - keeps what they chose.
    [ObservableProperty] private int _offsetMs = LyricsTuning.DefaultOffsetMs;
    [ObservableProperty] private bool _showNoteIcon = true;
    [ObservableProperty] private bool _showGapMarker = true;
    [ObservableProperty] private int _minimumCharacters = 24;
    [ObservableProperty] private LyricsMediaCoexistence _coexistence = LyricsMediaCoexistence.PreferLyrics;

    [ObservableProperty] private int _gapThresholdSeconds = 8;
    [ObservableProperty] private int _lineHoldSeconds = 6;

    [ObservableProperty] private bool _useLocalFiles = true;
    [ObservableProperty] private string _localLyricsFolder = string.Empty;
}
