using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Services.Hardware;

public enum GpuVendor
{
    Unknown = 0,
    Amd,
    Nvidia,
    Intel,

    Microsoft,
}

public static class GpuVendors
{
    public const uint Amd = 0x1002;
    public const uint Nvidia = 0x10DE;
    public const uint Intel = 0x8086;
    public const uint Microsoft = 0x1414;

    public static GpuVendor FromVendorId(uint? vendorId) => vendorId switch
    {
        Amd => GpuVendor.Amd,
        Nvidia => GpuVendor.Nvidia,
        Intel => GpuVendor.Intel,
        Microsoft => GpuVendor.Microsoft,
        _ => GpuVendor.Unknown,
    };

    public static uint? ParseWmiVendorId(string? pnpDeviceId, string? adapterCompatibility)
    {
        var match = Regex.Match(
            pnpDeviceId ?? string.Empty, "VEN_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
        if (match.Success)
            return Convert.ToUInt32(match.Groups[1].Value, 16);

        string compatibility = adapterCompatibility ?? string.Empty;
        if (compatibility.Length == 0)
            return null;

        if (compatibility.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return Nvidia;
        if (compatibility.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) ||
            compatibility.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            compatibility.Contains("ATI ", StringComparison.OrdinalIgnoreCase))
            return Amd;
        if (compatibility.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return Intel;
        if (compatibility.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            return Microsoft;

        return null;
    }
}

public interface IGpuAdapter
{
    string Name { get; }

    uint AdapterIndex { get; }

    uint? VendorId { get; }

    ulong? DedicatedVideoMemoryBytes { get; }

    bool IsSoftwareAdapter { get; }
}

public static class GpuAdapterSelector
{
    public const ulong DiscreteVramThresholdBytes = 1UL << 30;
    public static T? SelectPrimary<T>(IReadOnlyList<T>? adapters) where T : class, IGpuAdapter
    {
        if (adapters == null || adapters.Count == 0)
            return null;

        var candidates = adapters
            .Where(a => a != null)
            .Where(a => !a.IsSoftwareAdapter)
            .Where(a => GpuVendors.FromVendorId(a.VendorId) != GpuVendor.Microsoft)
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderByDescending(a => a.DedicatedVideoMemoryBytes >= DiscreteVramThresholdBytes ? 1 : 0)
            .ThenByDescending(a => GpuVendors.FromVendorId(a.VendorId) is GpuVendor.Nvidia or GpuVendor.Amd ? 1 : 0)
            .ThenByDescending(a => GpuVendors.FromVendorId(a.VendorId) == GpuVendor.Intel ? 1 : 0)
            .ThenByDescending(a => a.DedicatedVideoMemoryBytes ?? 0)
            .ThenBy(a => a.AdapterIndex)
            .First();
    }
}
