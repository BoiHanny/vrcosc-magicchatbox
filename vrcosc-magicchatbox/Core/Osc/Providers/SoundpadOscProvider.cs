using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class SoundpadOscProvider : IOscProvider
{
    private const string Icon = "🎶";

    private const int IconCost = 3;

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

        string playingSong = _modules.Value.Soundpad?.GetPlayingSong() ?? string.Empty;
        if (string.IsNullOrEmpty(playingSong)) return null;

        string text = BuildSegment(playingSong, _app.PrefixIconSoundpad, context.RemainingCharsIf(string.Empty));
        if (string.IsNullOrEmpty(text)) return null;

        return new OscSegment { Text = text };
    }

    public static string BuildSegment(string? title, bool withIcon, int budget)
    {
        if (budget <= 0)
            return string.Empty;

        string clean = SegmentWriter.Tidy(title);
        if (clean.Length == 0)
            return string.Empty;

        return SegmentWriter.Fit(
            budget,
            () => Compose(clean, withIcon),
            () => Compose(SegmentWriter.Truncate(clean, TitleRoom(budget, withIcon)), withIcon),
            () => Compose(SegmentWriter.Truncate(clean, TitleRoom(budget, false)), false));
    }

    private static int TitleRoom(int budget, bool withIcon)
        => budget - QuoteCost - (withIcon ? IconCost : 0);

    private static string Compose(string? title, bool withIcon)
        => string.IsNullOrEmpty(title)
            ? string.Empty
            : new SegmentWriter()
                .Field(OscText.Raw(withIcon ? Icon : null), OscText.Value($"'{title}'"))
                .Text;
}
