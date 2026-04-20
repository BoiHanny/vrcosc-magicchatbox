using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
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
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
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
                UpdateHardwareTree(hw);
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
        var sensor = hw == null ? null : FindTemperatureSensor(hw);
        sensor ??= FindCpuTemperatureFallbackSensor();
        return sensor?.Value;
    }

    public float? GetCpuPower()
    {
        var hw = FindHardware(HardwareType.Cpu);
        var sensor = hw == null ? null : FindPowerSensor(hw);
        sensor ??= FindCpuPowerFallbackSensor();
        return sensor?.Value;
    }

    public string GetCpuName()
    {
        return GetHardwareSnapshot().FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name;
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
            if (_computer != null)
            {
                _gpuCache ??= GetHardwareSnapshot()
                    .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                                h.HardwareType == HardwareType.GpuAmd ||
                                h.HardwareType == HardwareType.GpuIntel)
                    .Select(h => h.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (_gpuCache.Count > 0)
                    return _gpuCache;
            }
        }

        return GetAvailableGpusFromWindows();
    }

    /// <summary>
    /// DDR version via WMI — queried lazily and with a timeout so a bad WMI provider
    /// cannot stall the entire app startup path.
    /// </summary>
    public string GetDdrVersion()
    {
        try
        {
            var options = new EnumerationOptions
            {
                ReturnImmediately = false,
                Rewindable = false,
                Timeout = TimeSpan.FromSeconds(2)
            };

            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory",
                options);

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
        return GetHardwareSnapshot().FirstOrDefault(h => h.HardwareType == type);
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

    public float? GetCpuCoreClock()
    {
        var hw = FindHardware(HardwareType.Cpu);
        if (hw == null) return null;
        // Priority 1: any sensor named "CPU Core #N" under Clock type
        var coreSensor = hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Clock &&
            s.Name.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase));
        if (coreSensor?.Value != null) return coreSensor.Value;
        // Priority 2: bus speed clock sensor
        var busSensor = hw.Sensors.FirstOrDefault(s =>
            s.SensorType == SensorType.Clock &&
            s.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase));
        if (busSensor?.Value != null) return busSensor.Value;
        // Priority 3: any available Clock sensor on the CPU hardware
        return hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock)?.Value;
    }

    private IHardware ResolveGpu(string gpuName)
    {
        var gpus = GetHardwareSnapshot()
            .Where(h => h.HardwareType == HardwareType.GpuNvidia ||
                        h.HardwareType == HardwareType.GpuAmd ||
                        h.HardwareType == HardwareType.GpuIntel)
            .ToList();

        if (!string.IsNullOrEmpty(gpuName))
        {
            var match = gpus.FirstOrDefault(g => g.Name.Equals(gpuName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            string normalizedRequested = NormalizeHardwareName(gpuName);

            match = gpus.FirstOrDefault(g =>
                NormalizeHardwareName(g.Name).Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            match = gpus.FirstOrDefault(g =>
            {
                string candidate = NormalizeHardwareName(g.Name);
                return candidate.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                       normalizedRequested.Contains(candidate, StringComparison.OrdinalIgnoreCase);
            });
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

    private IReadOnlyList<string> GetAvailableGpusFromWindows()
    {
        try
        {
            var options = new EnumerationOptions
            {
                ReturnImmediately = false,
                Rewindable = false,
                Timeout = TimeSpan.FromSeconds(2)
            };

            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT Name FROM Win32_VideoController",
                options);

            return searcher.Get()
                .Cast<ManagementObject>()
                .Select(obj => obj["Name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return Array.Empty<string>();
        }
    }

    private static string NormalizeHardwareName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
            else if (builder.Length > 0 && builder[^1] != ' ')
                builder.Append(' ');
        }

        return builder.ToString().Trim();
    }

    private List<IHardware> GetHardwareSnapshot()
    {
        lock (_lock)
        {
            if (_computer == null) return new List<IHardware>();

            var hardware = new List<IHardware>();
            foreach (var root in _computer.Hardware)
            {
                CollectHardware(root, hardware);
            }

            return hardware;
        }
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateHardwareTree(subHardware);
        }
    }

    private static void CollectHardware(IHardware hardware, List<IHardware> hardwareList)
    {
        hardwareList.Add(hardware);
        foreach (var subHardware in hardware.SubHardware)
        {
            CollectHardware(subHardware, hardwareList);
        }
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var node in EnumerateHardwareTree(hardware))
        {
            foreach (var sensor in node.Sensors)
            {
                yield return sensor;
            }
        }
    }

    private static IEnumerable<IHardware> EnumerateHardwareTree(IHardware hardware)
    {
        yield return hardware;

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var nested in EnumerateHardwareTree(subHardware))
            {
                yield return nested;
            }
        }
    }

    private IEnumerable<ISensor> EnumerateAllSensors()
    {
        return GetHardwareSnapshot().SelectMany(EnumerateSensors);
    }

    private static ISensor FindSensor(IHardware hw, SensorType type, string name) =>
        EnumerateSensors(hw).FirstOrDefault(s =>
            s.SensorType == type &&
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static ISensor FindTemperatureSensor(IHardware hw) =>
        PickBestSensor(EnumerateSensors(hw), SensorType.Temperature, GetPreferredTemperatureScore);

    private static ISensor FindPowerSensor(IHardware hw) =>
        PickBestSensor(EnumerateSensors(hw), SensorType.Power, GetPreferredPowerScore);

    private ISensor FindCpuTemperatureFallbackSensor() =>
        PickBestSensor(EnumerateAllSensors(), SensorType.Temperature, GetCpuTemperatureFallbackScore, requirePositiveScore: true);

    private ISensor FindCpuPowerFallbackSensor() =>
        PickBestSensor(EnumerateAllSensors(), SensorType.Power, GetCpuPowerFallbackScore, requirePositiveScore: true);

    private static ISensor PickBestSensor(
        IEnumerable<ISensor> sensors,
        SensorType sensorType,
        Func<string, int> scoreSelector,
        bool requirePositiveScore = false)
    {
        ISensor bestSensor = null;
        int bestScore = int.MinValue;

        foreach (var sensor in sensors)
        {
            if (sensor.SensorType != sensorType || !sensor.Value.HasValue)
            {
                continue;
            }

            int score = scoreSelector(sensor.Name);
            if (score > bestScore)
            {
                bestScore = score;
                bestSensor = sensor;
            }
        }

        if (bestSensor != null && (!requirePositiveScore || bestScore > 0))
        {
            return bestSensor;
        }

        return requirePositiveScore
            ? null
            : sensors.FirstOrDefault(s => s.SensorType == sensorType && s.Value.HasValue);
    }

    private static int GetPreferredTemperatureScore(string sensorName)
    {
        int score = 0;

        if (ContainsAny(sensorName, "tctl", "tdie", "package"))
            score = 100;
        else if (ContainsAny(sensorName, "core", "edge"))
            score = 90;
        else if (ContainsAny(sensorName, "cpu", "gpu"))
            score = 80;
        else if (sensorName.Contains("ccd", StringComparison.OrdinalIgnoreCase))
            score = 75;
        else if (ContainsAny(sensorName, "junction", "hot spot", "hotspot"))
            score = 35;
        else if (ContainsAny(sensorName, "memory", "vram"))
            score = 20;

        if (sensorName.Contains("limit", StringComparison.OrdinalIgnoreCase))
            score -= 40;

        return score;
    }

    private static int GetPreferredPowerScore(string sensorName)
    {
        int score = 0;

        if (sensorName.Contains("package", StringComparison.OrdinalIgnoreCase))
            score = 100;
        else if (sensorName.Contains("board", StringComparison.OrdinalIgnoreCase))
            score = 95;
        else if (sensorName.Contains("core+s o c", StringComparison.OrdinalIgnoreCase) ||
                 sensorName.Contains("core+soc", StringComparison.OrdinalIgnoreCase))
            score = 92;
        else if (ContainsAny(sensorName, "cores", "core", "cpu", "gpu"))
            score = 85;
        else if (ContainsAny(sensorName, "ppt", "total"))
            score = 80;
        else if (sensorName.Contains("soc", StringComparison.OrdinalIgnoreCase))
            score = 70;
        else if (ContainsAny(sensorName, "memory", "vram"))
            score = 20;

        if (sensorName.Contains("limit", StringComparison.OrdinalIgnoreCase))
            score -= 50;

        return score;
    }

    private static int GetCpuTemperatureFallbackScore(string sensorName)
    {
        int score = 0;

        if (ContainsAny(sensorName, "tctl", "tdie"))
            score = 100;
        else if (sensorName.Contains("cpu package", StringComparison.OrdinalIgnoreCase))
            score = 98;
        else if (sensorName.Contains("package", StringComparison.OrdinalIgnoreCase))
            score = 90;
        else if (sensorName.Contains("ccd", StringComparison.OrdinalIgnoreCase))
            score = 88;
        else if (sensorName.Contains("cpu", StringComparison.OrdinalIgnoreCase) &&
                 !sensorName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            score = 82;
        else if (sensorName.Contains("core", StringComparison.OrdinalIgnoreCase))
            score = 75;

        if (sensorName.Contains("limit", StringComparison.OrdinalIgnoreCase))
            score -= 40;

        return score;
    }

    private static int GetCpuPowerFallbackScore(string sensorName)
    {
        int score = 0;

        if (sensorName.Contains("cpu package", StringComparison.OrdinalIgnoreCase))
            score = 100;
        else if (sensorName.Contains("package", StringComparison.OrdinalIgnoreCase))
            score = 95;
        else if (sensorName.Contains("core+soc", StringComparison.OrdinalIgnoreCase))
            score = 92;
        else if (sensorName.Contains("ppt", StringComparison.OrdinalIgnoreCase))
            score = 90;
        else if (sensorName.Contains("cpu", StringComparison.OrdinalIgnoreCase) &&
                 !sensorName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            score = 85;
        else if (sensorName.Contains("core", StringComparison.OrdinalIgnoreCase))
            score = 80;
        else if (sensorName.Contains("soc", StringComparison.OrdinalIgnoreCase))
            score = 65;

        if (sensorName.Contains("limit", StringComparison.OrdinalIgnoreCase))
            score -= 60;

        return score;
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (var value in values)
        {
            if (source.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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
