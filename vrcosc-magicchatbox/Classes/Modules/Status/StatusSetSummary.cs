using System;

namespace vrcosc_magicchatbox.Classes.Modules.Status;

public static class StatusSetSummary
{
    public static string Describe(int cyclingCount, bool cycleEnabled, int intervalSeconds)
    {
        if (cyclingCount <= 0)
            return "Nothing marked to cycle in here";

        if (!cycleEnabled)
            return cyclingCount == 1
                ? "1 message, cycling is off"
                : $"{cyclingCount} messages, cycling is off";

        string every = DescribeInterval(intervalSeconds);
        return cyclingCount == 1
            ? $"1 message{every}"
            : $"{cyclingCount} messages{every}";
    }

    private static string DescribeInterval(int seconds)
    {
        if (seconds <= 0)
            return " cycling";

        if (seconds < 60)
            return $" every {seconds}s";

        var span = TimeSpan.FromSeconds(seconds);
        int minutes = (int)span.TotalMinutes;
        int remainder = seconds % 60;

        return remainder == 0
            ? $" every {minutes}m"
            : $" every {minutes}m {remainder}s";
    }
}
