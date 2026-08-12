using System;
using System.IO;
using Microsoft.Win32;

namespace vrcosc_magicchatbox.Services.Vr;

public enum XrRuntimeKind
{
    Unknown = 0,

    SteamVr,

    VirtualDesktopXr,

    Oculus,

    WindowsMixedReality,

    Other,
}

public sealed record OpenXrRuntimeInfo(string? Name, string? ManifestPath, XrRuntimeKind Kind)
{
    public bool SupportsFrameTiming => Kind == XrRuntimeKind.SteamVr;

    public string DescribeForUser() => Kind switch
    {
        XrRuntimeKind.SteamVr =>
            "Active OpenXR runtime: SteamVR.",

        XrRuntimeKind.VirtualDesktopXr =>
            "Active OpenXR runtime: VirtualDesktopXR (VDXR), which bypasses SteamVR. "
            + "Switch Virtual Desktop's OpenXR runtime to SteamVR to see performance stats.",

        XrRuntimeKind.Oculus =>
            "Active OpenXR runtime: Oculus. Performance stats need SteamVR, so launch VRChat "
            + "through SteamVR instead.",

        XrRuntimeKind.WindowsMixedReality =>
            "Active OpenXR runtime: Windows Mixed Reality. Performance stats need SteamVR.",

        XrRuntimeKind.Other =>
            $"Active OpenXR runtime: {Name ?? "unrecognised"}. Performance stats need SteamVR.",

        _ => "No OpenXR runtime is registered.",
    };
}

public static class OpenXrRuntimeDetector
{
    private const string ActiveRuntimeKey = @"SOFTWARE\Khronos\OpenXR\1";
    private const string ActiveRuntimeValue = "ActiveRuntime";

    public static OpenXrRuntimeInfo Detect()
    {
        string? manifestPath = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ActiveRuntimeKey);
            manifestPath = key?.GetValue(ActiveRuntimeValue) as string;
        }
        catch (Exception)
        {
        }

        string? manifestJson = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                manifestJson = File.ReadAllText(manifestPath);
        }
        catch (Exception)
        {
        }

        return Classify(manifestPath, manifestJson);
    }

    public static OpenXrRuntimeInfo Classify(string? manifestPath, string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            return new OpenXrRuntimeInfo(null, null, XrRuntimeKind.Unknown);

        string? name = ExtractName(manifestJson);

        string haystack = $"{name} {manifestPath}";

        XrRuntimeKind kind =
            Contains(haystack, "SteamVR") || Contains(haystack, "steamxr") ? XrRuntimeKind.SteamVr
            : Contains(haystack, "VirtualDesktop") ? XrRuntimeKind.VirtualDesktopXr
            : Contains(haystack, "Oculus") || Contains(haystack, "LibOVR") ? XrRuntimeKind.Oculus
            : Contains(haystack, "MixedReality") || Contains(haystack, "WMR") ? XrRuntimeKind.WindowsMixedReality
            : XrRuntimeKind.Other;

        return new OpenXrRuntimeInfo(name, manifestPath, kind);
    }

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string? ExtractName(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            return null;

        const string marker = "\"name\"";
        int at = manifestJson.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return null;

        int colon = manifestJson.IndexOf(':', at + marker.Length);
        if (colon < 0)
            return null;

        int open = manifestJson.IndexOf('"', colon + 1);
        if (open < 0)
            return null;

        int close = manifestJson.IndexOf('"', open + 1);
        if (close < 0)
            return null;

        string value = manifestJson.Substring(open + 1, close - open - 1).Trim();
        return value.Length == 0 ? null : value;
    }
}
