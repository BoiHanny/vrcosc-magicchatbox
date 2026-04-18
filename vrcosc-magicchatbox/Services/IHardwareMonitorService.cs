using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstracts hardware monitoring (LibreHardwareMonitor + kernel32) behind a testable interface.
/// Call <see cref="UpdateAll"/> once per tick, then read cached sensor values via getters.
/// </summary>
public interface IHardwareMonitorService : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void Close();

    /// <summary>
    /// Polls all hardware sensors once. Call this at the start of each tick cycle,
    /// then read cached values from the getters below (zero-cost reads).
    /// </summary>
    void UpdateAll();

    float? GetCpuLoad();
    float? GetCpuTemperature();
    float? GetCpuPower();
    string GetCpuName();

    float? GetGpuLoad(string gpuName, string sensorName);
    float? GetGpuTemperature(string gpuName);
    float? GetGpuHotspotTemperature(string gpuName);
    float? GetGpuPower(string gpuName);
    float? GetGpuVramUsed(string gpuName, string sensorName);
    float? GetGpuVramTotal(string gpuName, string sensorName);
    string GetGpuName(string gpuName);

    float? GetRamUsed();
    float? GetRamAvailable();

    /// <summary>RAM via kernel32 GlobalMemoryStatusEx (sub-microsecond). Returns (totalGiB, usedGiB).</summary>
    (double totalGiB, double usedGiB)? GetWindowsMemoryInfo();

    IReadOnlyList<string> GetAvailableGpus();

    /// <summary>DDR memory version string (e.g. "DDR5") via WMI (called once, not per-tick).</summary>
    string GetDdrVersion();

    /// <summary>
    /// CPU load percentage without loading the kernel driver — uses PerformanceCounter.
    /// Returns null if the counter is unavailable. The first call may return 0 (priming read).
    /// </summary>
    float? GetCpuLoadBasic();

    /// <summary>GPU fan speed in RPM. Returns null if not available.</summary>
    float? GetGpuFanSpeed(string gpuName);

    /// <summary>GPU core clock in MHz. Returns null if not available.</summary>
    float? GetGpuCoreClock(string gpuName);

    /// <summary>GPU memory clock in MHz. Returns null if not available.</summary>
    float? GetGpuMemoryClock(string gpuName);

    /// <summary>GPU memory/VRAM die temperature in °C. Returns null if not available.</summary>
    float? GetGpuMemoryTemperature(string gpuName);

    /// <summary>GPU memory controller load as a percentage. Returns null if not available.</summary>
    float? GetGpuMemoryLoad(string gpuName);

    /// <summary>Highest loaded CPU core as a percentage. Returns null if not available.</summary>
    float? GetCpuMaxCoreLoad();
}
