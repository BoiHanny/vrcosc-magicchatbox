namespace vrcosc_magicchatbox.Services.Hardware;

/// <summary>
/// One snapshot of a GPU's sensors from a vendor API. Every field is null when that particular
/// sensor is not exposed for this card, which is a different thing from "we failed to read it".
/// </summary>
public sealed record GpuSensorReadings
{
    /// <summary>Name of the hardware these readings came from.</summary>
    public string HardwareName { get; init; } = string.Empty;

    /// <summary>Vendor of the hardware these readings came from.</summary>
    public GpuVendor Vendor { get; init; }

    /// <summary>Core load, percent.</summary>
    public float? CoreLoad { get; init; }

    /// <summary>Direct3D 3D-engine load, percent. Used when the "3D hook" option is on.</summary>
    public float? D3DLoad { get; init; }

    /// <summary>Memory-controller load, percent.</summary>
    public float? MemoryLoad { get; init; }

    /// <summary>Core (edge) temperature, °C.</summary>
    public float? CoreTemperatureC { get; init; }

    /// <summary>Hot-spot / junction temperature, °C.</summary>
    public float? HotspotTemperatureC { get; init; }

    /// <summary>VRAM die temperature, °C.</summary>
    public float? MemoryTemperatureC { get; init; }

    /// <summary>Board power draw, watts.</summary>
    public float? PowerWatts { get; init; }

    /// <summary>Fan speed, percent of maximum.</summary>
    public float? FanPercent { get; init; }

    /// <summary>Core clock, MHz.</summary>
    public float? CoreClockMhz { get; init; }

    /// <summary>Memory clock, MHz.</summary>
    public float? MemoryClockMhz { get; init; }

    /// <summary>VRAM in use, MiB.</summary>
    public float? VramUsedMiB { get; init; }

    /// <summary>Total VRAM, MiB.</summary>
    public float? VramTotalMiB { get; init; }
}
