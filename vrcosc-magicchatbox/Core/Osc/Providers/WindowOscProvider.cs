using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class WindowOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly WindowActivitySettings _waSettings;
    private readonly ChatStatusDisplayState _chatStatus;

    public WindowOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<WindowActivitySettings> waProvider,
        ChatStatusDisplayState chatStatus)
    {
        _intgr = intgrProvider.Value;
        _waSettings = waProvider.Value;
        _chatStatus = chatStatus;
    }

    public string SortKey => "Window";
    public string UiKey => "Window";
    public int Priority => 30;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrScanWindowActivity && (isVR ? _intgr.IntgrWindowActivity_VR : _intgr.IntgrWindowActivity_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrScanWindowActivity || _chatStatus.FocusedWindow.Length == 0)
            return null;

        // The user's own wording, already styled the way they typed it - VR and desktop both ship a
        // raised focus word - so it is placed as-is rather than raised a second time.
        string heading = context.IsVRRunning ? _waSettings.VrTitle : _waSettings.DesktopTitle;
        string focusWord = context.IsVRRunning ? _waSettings.VrFocusTitle : _waSettings.DesktopFocusTitle;
        bool showFocus = context.IsVRRunning ? _intgr.IntgrScanForce : _waSettings.ShowFocusedApp;

        string Compose(string? word, string? app)
            => new SegmentWriter()
                .Field(OscText.Raw(heading), OscText.Raw(word), OscText.Value(app))
                .Text;

        int budget = context.RemainingCharsIf(string.Empty);
        string app = _chatStatus.FocusedWindow;

        // A browser tab title can outrun the whole line on its own, so the app name is cut to what
        // is left rather than the builder having to delete the segment - or everyone else's.
        string text = showFocus
            ? SegmentWriter.Fit(
                budget,
                Compose(focusWord, app),
                Compose(null, app),
                Compose(null, SegmentWriter.Truncate(app, budget - (Compose(null, "x").Length - 1))),
                Compose(null, null))
            : SegmentWriter.Fit(budget, Compose(null, null));

        return text.Length == 0 ? null : new OscSegment { Text = text };
    }
}
