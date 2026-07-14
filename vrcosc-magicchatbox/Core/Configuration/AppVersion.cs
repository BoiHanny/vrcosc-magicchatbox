using System;
using System.Reflection;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// Reads the running assembly version and provides numeric version comparison.
/// Kept static so it can be used from JsonSettingsProvider without DI.
/// </summary>
public static class AppVersion
{
    private static readonly Lazy<string> _current = new(() =>
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    });

    /// <summary>Assembly version string, e.g. "1.2.3.0".</summary>
    public static string Current => _current.Value;

    /// <summary>
    /// Returns -1 if a &lt; b, 0 if equal, +1 if a &gt; b.
    /// Handles variable segment counts ("1.2" vs "1.2.0.0") and leading zeros.
    /// Returns 0 on empty/null inputs.
    /// </summary>
    public static int Compare(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return -1;
        if (string.IsNullOrEmpty(b)) return 1;

        var partsA = a.Split('.');
        var partsB = b.Split('.');
        int len = Math.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < len; i++)
        {
            int segA = i < partsA.Length && int.TryParse(partsA[i], out int va) ? va : 0;
            int segB = i < partsB.Length && int.TryParse(partsB[i], out int vb) ? vb : 0;
            if (segA != segB) return segA.CompareTo(segB);
        }
        return 0;
    }

    /// <summary>True when <paramref name="version"/> is older than <paramref name="than"/>.</summary>
    public static bool IsOlderThan(string version, string than) => Compare(version, than) < 0;
}
