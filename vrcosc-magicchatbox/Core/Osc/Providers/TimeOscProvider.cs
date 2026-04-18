using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Current local time → OSC segment.
/// Reads pre-formatted time from <see cref="IntegrationDisplayState.CurrentTime"/>.
/// </summary>
public sealed class TimeOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly IntegrationDisplayState _display;
    private readonly TimeSettings _time;

    public TimeOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        IntegrationDisplayState display,
        ISettingsProvider<TimeSettings> timeProvider)
    {
        _intgr = intgrProvider.Value;
        _display = display;
        _time = timeProvider.Value;
    }

    public string SortKey => "Time";
    public string UiKey => "Time";
    public int Priority => 90;

    public bool IsEnabledForCurrentMode(bool isVR)
        => isVR ? _intgr.IntgrCurrentTime_VR : _intgr.IntgrCurrentTime_DESKTOP;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrScanWindowTime || _display.CurrentTime == null)
            return null;

        string text = _time.PrefixTime
            ? "My time: " + _display.CurrentTime
            : _display.CurrentTime;

        return new OscSegment { Text = text };
    }
}
