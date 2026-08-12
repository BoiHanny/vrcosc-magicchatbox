namespace vrcosc_magicchatbox.Services.Hardware;

public sealed record GpuSensorReadings
{
    public string HardwareName { get; init; } = string.Empty;

    public GpuVendor Vendor { get; init; }

    public float? CoreLoad { get; init; }

    public float? D3DLoad { get; init; }

    public float? MemoryLoad { get; init; }

    public float? CoreTemperatureC { get; init; }

    public float? HotspotTemperatureC { get; init; }

    public float? MemoryTemperatureC { get; init; }

    public float? PowerWatts { get; init; }

    public float? FanPercent { get; init; }

    public float? CoreClockMhz { get; init; }

    public float? MemoryClockMhz { get; init; }

    public float? VramUsedMiB { get; init; }

    public float? VramTotalMiB { get; init; }
}
