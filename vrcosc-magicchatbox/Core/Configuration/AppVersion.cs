using System;
using System.Reflection;

namespace vrcosc_magicchatbox.Core.Configuration;

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

    public static string Current => _current.Value;

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

    public static bool IsOlderThan(string version, string than) => Compare(version, than) < 0;
}
