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

        int budget = context.RemainingCharsIf(string.Empty);
        if (budget <= 0)
            return null;

        string text = LyricSegmentFormatter.Build(_display.Cursor, _display.Position, budget, _settings);

        return string.IsNullOrWhiteSpace(text) ? null : new OscSegment { Text = text };
    }
}
