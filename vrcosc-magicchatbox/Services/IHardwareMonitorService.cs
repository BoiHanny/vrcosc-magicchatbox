using System;
using System.Collections.Generic;

namespace vrcosc_magicchatbox.Services;

public interface IHardwareMonitorService : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void Close();

    bool VendorGpuSensorsEnabled { get; set; }

    TimeSpan StatsTickInterval { get; set; }

    string GetHardwareMonitorStatusMessage();

    void UpdateAll();

    float? GetCpuLoad();
    string? GetCpuName();

    float? GetGpuLoad(string gpuName, string sensorName);
    float? GetGpuTemperature(string gpuName);
    float? GetGpuHotspotTemperature(string gpuName);
    float? GetGpuPower(string gpuName);
    float? GetGpuVramUsed(string gpuName, string sensorName);
    float? GetGpuVramTotal(string gpuName, string sensorName);
    string? GetGpuName(string gpuName);

    float? GetRamUsed();
    float? GetRamAvailable();

    (double totalGiB, double usedGiB)? GetWindowsMemoryInfo();

    IReadOnlyList<string> GetAvailableGpus();

    string? GetDdrVersion();

    float? GetCpuLoadBasic();

    float? GetGpuFanSpeed(string gpuName);

    float? GetGpuCoreClock(string gpuName);

    float? GetGpuMemoryClock(string gpuName);

    float? GetGpuMemoryTemperature(string gpuName);

    float? GetGpuMemoryLoad(string gpuName);

}
