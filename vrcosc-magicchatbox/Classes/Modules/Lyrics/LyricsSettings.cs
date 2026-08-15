using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    [ObservableProperty] private int _offsetMs = LyricsTuning.DefaultOffsetMs;
    [ObservableProperty] private bool _showNoteIcon = true;
    [ObservableProperty] private bool _showGapMarker = true;
    [ObservableProperty] private int _minimumCharacters = 24;
    [ObservableProperty] private LyricsMediaCoexistence _coexistence = LyricsMediaCoexistence.PreferLyrics;

    [ObservableProperty] private int _gapThresholdSeconds = 8;
    [ObservableProperty] private int _lineHoldSeconds = 6;

    [ObservableProperty] private bool _useLocalFiles = true;
    [ObservableProperty] private string _localLyricsFolder = string.Empty;

    [ObservableProperty] private LyricsInstrumentalMarker _instrumentalMarker = LyricsInstrumentalMarker.TrailingDots;

    [ObservableProperty] private bool _superscriptAsides = true;

    [ObservableProperty] private LyricsMatchStrictness _matchStrictness = LyricsMatchStrictness.Balanced;

    [ObservableProperty] private bool _broadenSearchWhenNoMatch = true;

    public static IEnumerable<LyricsInstrumentalMarker> AvailableInstrumentalMarkers { get; } =
        Enum.GetValues(typeof(LyricsInstrumentalMarker)).Cast<LyricsInstrumentalMarker>().ToList();

    public static IEnumerable<LyricsMatchStrictness> AvailableMatchStrictness { get; } =
        Enum.GetValues(typeof(LyricsMatchStrictness)).Cast<LyricsMatchStrictness>().ToList();
}
