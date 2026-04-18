using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Wraps LibreHardwareMonitor Computer and kernel32 APIs.
/// Call <see cref="UpdateAll"/> once per tick, then read cached sensor values.
/// </summary>
public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private Computer _computer;
    private readonly object _lock = new();
    private List<string> _gpuCache;
    private PerformanceCounter _cpuCounter;

    public HardwareMonitorService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // prime — first read always returns 0
        }
        catch
        {
            _cpuCounter = null;
        }
    }

    // ── P/Invoke: GlobalMemoryStatusEx (sub-microsecond, replaces WMI) ──

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public bool IsOpen
    {
        get { lock (_lock) return _computer != null; }
    }

    public void Open()
    {
        lock (_lock)
        {
            if (_computer != null) return;
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                };
                _computer.Open();
                _gpuCache = null;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                _computer = null;
            }
        }
    }

    public void Close()
    {
        lock (_lock)
        {
            if (_computer == null) return;
            try { _computer.Close(); }
            catch (Exception ex) { Logging.WriteInfo($"Hardware monitor close error: {ex.Message}"); }
            _computer = null;
            _gpuCache = null;
        }
    }

    public void UpdateAll()
    {
        lock (_lock)
        {
            if (_computer == null) return;
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
            }
        }
    }

    // ── CPU (read cached sensors — no hw.Update() calls) ─────────

    public float? GetCpuLoad()
    {
        var hw = FindHardware(HardwareType.Cpu);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Load, "CPU Total")?.Value;
    }

    public float? GetCpuTemperature()
    {
        var hw = FindHardware(HardwareType.Cpu);
        if (hw == null) return null;
        return FindTemperatureSensor(hw)?.Value;
    }

    public float? GetCpuPower()
    {
        var hw = FindHardware(HardwareType.Cpu);
        if (hw == null) return null;
        return FindPowerSensor(hw)?.Value;
    }

    public string GetCpuName()
    {
        lock (_lock)
        {
            if (_computer == null) return null;
            return _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name;
        }
    }

    public float? GetGpuLoad(string gpuName, string sensorName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Load, sensorName)?.Value;
    }

    public float? GetGpuTemperature(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindTemperatureSensor(hw)?.Value;
    }

    public float? GetGpuHotspotTemperature(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Temperature &&
            (s.Name.Contains("Hot spot", StringComparison.OrdinalIgnoreCase) ||
             s.Name.Contains("Hotspot", StringComparison.OrdinalIgnoreCase)))?.Value;
    }

    public float? GetGpuPower(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindPowerSensor(hw)?.Value;
    }

    public float? GetGpuVramUsed(string gpuName, string sensorName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.SmallData, sensorName)?.Value;
    }

    public float? GetGpuVramTotal(string gpuName, string sensorName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.SmallData, sensorName)?.Value;
    }

    public string GetGpuName(string gpuName)
    {
        return ResolveGpu(gpuName)?.Name;
    }

    // ── RAM (LHM cached sensors) ─────────────────────────────────

    public float? GetRamUsed()
    {
        var hw = FindHardware(HardwareType.Memory);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Data, "Memory Used")?.Value;
    }

    public float? GetRamAvailable()
    {
        var hw = FindHardware(HardwareType.Memory);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Data, "Memory Available")?.Value;
    }

    /// <summary>
    /// Uses kernel32 GlobalMemoryStatusEx — ~0.001ms vs WMI's 50-200ms.
    /// </summary>
    public (double totalGiB, double usedGiB)? GetWindowsMemoryInfo()
    {
        try
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref memStatus))
                return null;

            const double bytesToGiB = 1024.0 * 1024.0 * 1024.0;
            double totalGiB = memStatus.ullTotalPhys / bytesToGiB;
            double usedGiB = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / bytesToGiB;
            return (totalGiB, Math.Max(0, usedGiB));
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public IReadOnlyList<string> GetAvailableGpus()
    {
        lock (_lock)
        {
            if (_computer == null) return Array.Empty<string>();
            _gpuCache ??= _computer.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                            h.HardwareType == HardwareType.GpuAmd ||
                            h.HardwareType == HardwareType.GpuIntel)
                .Select(h => h.Name)
                .ToList();
            return _gpuCache;
        }
    }

    /// <summary>
    /// DDR version via WMI — called once at startup, not per-tick. WMI overhead acceptable here.
    /// </summary>
    public string GetDdrVersion()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["SMBIOSMemoryType"] == null) continue;
                ushort type = Convert.ToUInt16(obj["SMBIOSMemoryType"]);
                string version = MapSmbiostoDdr(type);
                if (version != null) return version;
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        return null;
    }

    public void Dispose() => Close();

    private IHardware FindHardware(HardwareType type)
    {
        lock (_lock)
        {
            return _computer?.Hardware.FirstOrDefault(h => h.HardwareType == type);
        }
    }

    public float? GetCpuLoadBasic()
    {
        try { return _cpuCounter?.NextValue(); }
        catch { return null; }
    }

    public float? GetGpuFanSpeed(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        // Try common fan sensor names in priority order
        string[] fanNames = { "GPU Fan", "GPU Fan 1", "Fan #1" };
        foreach (var name in fanNames)
        {
            var s = FindSensor(hw, SensorType.Fan, name);
            if (s?.Value != null) return s.Value;
        }
        // Fallback: first available fan sensor on this GPU
        return hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan)?.Value;
    }

    public float? GetGpuCoreClock(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Clock, "GPU Core")?.Value;
    }

    public float? GetGpuMemoryClock(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Clock, "GPU Memory")?.Value;
    }

    public float? GetGpuMemoryTemperature(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        // Try specific memory temp sensor names in priority order
        string[] names = { "GPU Memory Junction", "Memory Junction", "GPU Memory", "VRAM" };
        foreach (var name in names)
        {
            var s = hw.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Temperature &&
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (s?.Value != null) return s.Value;
        }
        // Fallback: any temp sensor that mentions "Memory" but exclude core/hotspot
        return hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Temperature &&
            s.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0 &&
            s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) < 0)?.Value;
    }

    public float? GetGpuMemoryLoad(string gpuName)
    {
        var hw = ResolveGpu(gpuName);
        if (hw == null) return null;
        return FindSensor(hw, SensorType.Load, "GPU Memory")?.Value;
    }

    public float? GetCpuMaxCoreLoad()
    {
        var hw = FindHardware(HardwareType.Cpu);
        if (hw == null) return null;
        // LHM exposes a pre-computed max sensor on newer drivers
        var maxSensor = FindSensor(hw, SensorType.Load, "CPU Core Max");
        if (maxSensor?.Value != null) return maxSensor.Value;
        // Fallback: scan all per-core load sensors and return highest
        float max = float.MinValue;
        bool found = false;
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != SensorType.Load) continue;
            if (!s.Name.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase)) continue;
            if (s.Value.HasValue && s.Value.Value > max)
            {
                max = s.Value.Value;
                found = true;
            }
        }
        return found ? max : null;
    }

    private IHardware ResolveGpu(string gpuName)
    {
        lock (_lock)
        {
            if (_computer == null) return null;
            var gpus = _computer.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                            h.HardwareType == HardwareType.GpuAmd ||
                            h.HardwareType == HardwareType.GpuIntel)
                .ToList();

            if (!string.IsNullOrEmpty(gpuName))
            {
                var match = gpus.FirstOrDefault(g => g.Name.Equals(gpuName, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            var nvidia = gpus.FirstOrDefault(g =>
                g.HardwareType == HardwareType.GpuNvidia &&
                !g.Name.Contains("integrated", StringComparison.OrdinalIgnoreCase));
            if (nvidia != null) return nvidia;

            var amd = gpus.FirstOrDefault(g =>
                g.HardwareType == HardwareType.GpuAmd &&
                !g.Name.Contains("integrated", StringComparison.OrdinalIgnoreCase));
            if (amd != null) return amd;

            return gpus.FirstOrDefault();
        }
    }

    private static ISensor FindSensor(IHardware hw, SensorType type, string name) =>
        hw.Sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name);

    private static ISensor FindTemperatureSensor(IHardware hw) =>
        hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Temperature &&
            (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
             s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)));

    private static ISensor FindPowerSensor(IHardware hw) =>
        hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Power &&
            (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
             s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)));

    private static string MapSmbiostoDdr(ushort smbiosMemoryType) => smbiosMemoryType switch
    {
        0 => null,
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        30 => "DDR5",
        34 => "DDR5",
        _ => null,
    };
}
