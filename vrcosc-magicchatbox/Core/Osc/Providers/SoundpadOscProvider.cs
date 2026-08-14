using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class SoundpadOscProvider : IOscProvider
{
    /// <summary>The icon the segment has always carried. Outside the basic plane, so it costs two.</summary>
    private const string Icon = "🎶";

    /// <summary>The icon plus the space the writer puts after it.</summary>
    private const int IconCost = 3;

    /// <summary>The two quotes around the title.</summary>
    private const int QuoteCost = 2;

    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;
    private readonly AppSettings _app;

    public SoundpadOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<AppSettings> appProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
        _app = appProvider.Value;
    }

    public string SortKey => "Soundpad";
    public string UiKey => "Soundpad";
    public int Priority => 75;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrSoundpad && (isVR ? _intgr.IntgrSoundpad_VR : _intgr.IntgrSoundpad_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrSoundpad) return null;

        string playingSong = _modules.Value.Soundpad?.GetPlayingSong();
        if (string.IsNullOrEmpty(playingSong)) return null;

        // A clip name is whatever the file was called, so the segment used to be however long that
        // was and the rest of the line paid for it.
        string text = BuildSegment(playingSong, _app.PrefixIconSoundpad, context.RemainingCharsIf(string.Empty));
        if (string.IsNullOrEmpty(text)) return null;

        return new OscSegment { Text = text };
    }

    /// <summary>
    /// The clip title in quotes, inside <paramref name="budget"/> characters.
    /// </summary>
    /// <remarks>
    /// The title is the value and stays full size. Longest rung first: the whole title, then the
    /// title cut to what is left, then the same with the icon given up so the title keeps its three
    /// characters. There is nothing else in this segment, so a title that will not fit shortens
    /// rather than taking Soundpad off the line.
    /// </remarks>
    public static string BuildSegment(string? title, bool withIcon, int budget)
    {
        if (budget <= 0)
            return string.Empty;

        // Tidied before it is measured, otherwise the room reserved for the title is spent on
        // whitespace the writer is about to collapse anyway.
        string clean = SegmentWriter.Tidy(title);
        if (clean.Length == 0)
            return string.Empty;

        return SegmentWriter.Fit(
            budget,
            Compose(clean, withIcon),
            Compose(SegmentWriter.Truncate(clean, TitleRoom(budget, withIcon)), withIcon),
            Compose(SegmentWriter.Truncate(clean, TitleRoom(budget, false)), false));
    }

    /// <summary>The icon and the quotes are fixed cost; what is left belongs to the title.</summary>
    private static int TitleRoom(int budget, bool withIcon)
        => budget - QuoteCost - (withIcon ? IconCost : 0);

    private static string Compose(string? title, bool withIcon)
        => string.IsNullOrEmpty(title)
            ? string.Empty
            : new SegmentWriter()
                .Field(OscText.Raw(withIcon ? Icon : null), OscText.Value($"'{title}'"))
                .Text;
}
