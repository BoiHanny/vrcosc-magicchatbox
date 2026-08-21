using System;

namespace vrcosc_magicchatbox.Core.Updates;

public sealed class ProgressThrottle
{
    private readonly TimeSpan _minimumInterval;
    private readonly double _minimumPercentStep;

    private bool _hasReported;
    private TimeSpan _lastReportedAt;
    private double _lastReportedPercent;

    public ProgressThrottle(TimeSpan? minimumInterval = null, double minimumPercentStep = 1d)
    {
        _minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(120);
        _minimumPercentStep = minimumPercentStep;
    }

    public bool ShouldReport(TimeSpan elapsed, double percent, bool force = false)
    {
        if (force || !_hasReported)
        {
            _hasReported = true;
            _lastReportedAt = elapsed;
            _lastReportedPercent = percent;
            return true;
        }

        bool longEnough = elapsed - _lastReportedAt >= _minimumInterval;
        bool movedEnough = Math.Abs(percent - _lastReportedPercent) >= _minimumPercentStep;

        if (!longEnough && !movedEnough)
        {
            return false;
        }

        _lastReportedAt = elapsed;
        _lastReportedPercent = percent;
        return true;
    }
}
