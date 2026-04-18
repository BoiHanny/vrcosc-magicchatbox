using System.Text;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Foreground window/app activity → OSC segment.
/// Reads focused window name from <see cref="ChatStatusDisplayState.FocusedWindow"/>.
/// </summary>
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

        var sb = new StringBuilder();
        if (context.IsVRRunning)
        {
            sb.Append(_waSettings.VrTitle);
            if (_intgr.IntgrScanForce)
                sb.Append($" {_waSettings.VrFocusTitle} {_chatStatus.FocusedWindow}");
        }
        else
        {
            sb.Append(_waSettings.DesktopTitle);
            if (_waSettings.ShowFocusedApp)
                sb.Append($" {_waSettings.DesktopFocusTitle} {_chatStatus.FocusedWindow}");
        }

        string text = sb.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : new OscSegment { Text = text };
    }
}
