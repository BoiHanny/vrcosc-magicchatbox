using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class LyricsOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly LyricsSettings _settings;
    private readonly LyricsDisplayState _display;

    public LyricsOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<LyricsSettings> settingsProvider,
        LyricsDisplayState display)
    {
        _intgr = intgrProvider.Value;
        _settings = settingsProvider.Value;
        _display = display;
    }

    public string SortKey => "Lyrics";
    public string UiKey => "Lyrics";
    public int Priority => 22;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrLyrics && (isVR ? _intgr.IntgrLyrics_VR : _intgr.IntgrLyrics_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrLyrics || !_display.IsShowingLine)
            return null;

        string line = _display.CurrentLine;
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // The formatter applies MinimumCharacters itself, and only to a lyric line. A break marker
        // is complete at one character.
        //
        // RemainingCharsIf already joins the candidate onto the segments collected so far, so the
        // separator is in that figure. Subtracting it again cost the lyric a few characters on
        // every line.
        int budget = context.RemainingCharsIf(string.Empty);
        if (budget <= 0)
            return null;

        string text = LyricSegmentFormatter.Build(_display.Cursor, _display.Position, budget, _settings);

        return string.IsNullOrWhiteSpace(text) ? null : new OscSegment { Text = text };
    }
}
