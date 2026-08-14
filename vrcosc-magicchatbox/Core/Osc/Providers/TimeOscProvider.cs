using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

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
        => _intgr.IntgrScanWindowTime && (isVR ? _intgr.IntgrCurrentTime_VR : _intgr.IntgrCurrentTime_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        // The clock starts out empty and is only ever assigned, so a null check never fired: with the
        // prefix on, the label went out on its own before the first scan filled the clock in.
        if (!_intgr.IntgrScanWindowTime || string.IsNullOrEmpty(_display.CurrentTime))
            return null;

        // Composed by the shared formatter so the Time settings preview shows this exact line.
        return new OscSegment { Text = TimeSegmentFormatter.Compose(_display.CurrentTime, _time.PrefixTime) };
    }
}
