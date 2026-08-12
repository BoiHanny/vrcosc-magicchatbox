using System;

namespace vrcosc_magicchatbox.Core.Osc;

public static class TransientWindow
{
    public static bool ShouldShow(
        bool onlyOnChange,
        DateTime lastChangeUtc,
        DateTime nowUtc,
        double durationSeconds)
    {
        if (!onlyOnChange)
            return true;

        if (lastChangeUtc == default || durationSeconds <= 0)
            return false;

        double elapsed = (nowUtc - lastChangeUtc).TotalSeconds;
        if (elapsed < 0)
            return true;

        return elapsed <= durationSeconds;
    }
}
