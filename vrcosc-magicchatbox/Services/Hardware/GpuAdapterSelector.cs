using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace vrcosc_magicchatbox.Services.Hardware;

/// <summary>GPU silicon vendor, resolved from the PCI vendor id reported by DXGI.</summary>
public enum GpuVendor
{
    Unknown = 0,
    Amd,
    Nvidia,
    Intel,

    /// <summary>Microsoft Basic Render Driver / WARP — never a real GPU.</summary>
    Microsoft,
}

/// <summary>PCI vendor ids and the mapping to <see cref="GpuVendor"/>.</summary>
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

    /// <summary>
    /// Recovers the PCI vendor id from the strings WMI reports, preferring the <c>VEN_xxxx</c>
    /// token in the PNP device id and falling back to the vendor name.
    /// <para>
    /// <c>Win32_VideoController</c> has no vendor-id column, so without this every adapter on the
    /// WMI fallback path looks like <see cref="GpuVendor.Unknown"/> — which silently switches off
    /// nvidia-smi and the vendor sensor routing on exactly the machines where DXGI enumeration
    /// already failed and the fallback is the only source.
    /// </para>
    /// </summary>
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

/// <summary>The adapter facts <see cref="GpuAdapterSelector"/> needs to rank candidates.</summary>
public interface IGpuAdapter
{
    string Name { get; }

    /// <summary>DXGI enumeration order. Used only as a stable tie-break.</summary>
    uint AdapterIndex { get; }

    /// <summary>PCI vendor id, or null when it came from a source that doesn't report one (WMI).</summary>
    uint? VendorId { get; }

    /// <summary>Dedicated VRAM in bytes. The only reliable way to tell a dGPU from an iGPU.</summary>
    ulong? DedicatedVideoMemoryBytes { get; }

    /// <summary>True for software rasterizers (DXGI_ADAPTER_FLAG_SOFTWARE).</summary>
    bool IsSoftwareAdapter { get; }
}

/// <summary>
/// Picks the GPU whose stats the user actually wants.
/// <para>
/// This replaces a substring test for the literal word "integrated", which nothing real matches:
/// "AMD Radeon RX 6900 XT", "AMD Radeon(TM) Graphics" (a Ryzen iGPU) and "Intel(R) UHD Graphics 770"
/// all fail it, so selection degenerated to "whatever DXGI enumerated first".
/// </para>
/// </summary>
public static class GpuAdapterSelector
{
    /// <summary>Dedicated VRAM at or above this marks a discrete card.</summary>
    public const ulong DiscreteVramThresholdBytes = 1UL << 30; // 1 GiB

    /// <summary>Ranks candidates and returns the best one, or default when none qualify.</summary>
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
            // Dedicated VRAM must outrank vendor: on a Ryzen APU plus a Radeon dGPU both adapters
            // report vendor 0x1002, and only the VRAM size separates the 680M from the RX 6900 XT.
            .OrderByDescending(a => a.DedicatedVideoMemoryBytes >= DiscreteVramThresholdBytes ? 1 : 0)
            .ThenByDescending(a => GpuVendors.FromVendorId(a.VendorId) is GpuVendor.Nvidia or GpuVendor.Amd ? 1 : 0)
            .ThenByDescending(a => GpuVendors.FromVendorId(a.VendorId) == GpuVendor.Intel ? 1 : 0)
            .ThenByDescending(a => a.DedicatedVideoMemoryBytes ?? 0)
            .ThenBy(a => a.AdapterIndex)
            .First();
    }
}
