using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services.Hardware;

/// <summary>
/// Reads GPU sensors through LibreHardwareMonitor with <b>only</b> <c>IsGpuEnabled</c> set.
/// <para>
/// This exists because the driverless pipeline has no AMD data source at all: temperature, hotspot,
/// power, fan and clocks were hardcoded to <c>nvidia-smi</c>, and load/VRAM rested solely on Windows
/// performance counters. LHM's AMD path is user-mode <c>atiadlxx.dll</c> and its NVIDIA path is
/// <c>nvml.dll</c>; neither loads a kernel driver. PawnIO is only reached from the CPU, motherboard
/// and RAM-SPD groups, which stay disabled here.
/// </para>
/// </summary>
public sealed class LhmGpuSensorProvider : IDisposable
{
    /// <summary>Sensors are re-read at most this often; every getter shares one refresh.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>After this many failed opens the provider gives up for the rest of the process.</summary>
    private const int MaxOpenFailures = 2;

    private readonly object _lock = new();
    private Computer? _computer;
    private Dictionary<string, GpuSensorReadings> _readings = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _capturedAtUtc;
    private int _openFailures;
    private bool _loggedUnavailable;
    private bool _disposed;

    /// <summary>False once initialisation has failed enough times to stop trying.</summary>
    public bool IsPermanentlyUnavailable
    {
        get { lock (_lock) return _openFailures >= MaxOpenFailures; }
    }

    /// <summary>True while a LibreHardwareMonitor session is open.</summary>
    public bool IsOpen
    {
        get { lock (_lock) return _computer != null; }
    }

    /// <summary>
    /// Opens a GPU-only monitoring session. Returns false and self-disables after repeated
    /// failures so a broken or unusual driver can never take the whole stats subsystem down.
    /// </summary>
    public bool TryOpen()
    {
        lock (_lock)
        {
            if (_disposed)
                return false;
            if (_computer != null)
                return true;
            if (_openFailures >= MaxOpenFailures)
                return false;
        }

        Computer? computer = null;
        try
        {
            computer = new Computer
            {
                IsGpuEnabled = true,
                IsCpuEnabled = false,
                IsMemoryEnabled = false,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false,
                IsBatteryEnabled = false,
                IsPsuEnabled = false,
                IsPowerMonitorEnabled = false,
            };

            // Computer.AddGroups has no exception handling of its own, so a hardware group that
            // throws during construction escapes straight out of Open().
            computer.Open();
        }
        catch (Exception ex)
        {
            TryCloseQuietly(computer);
            lock (_lock)
            {
                _openFailures++;
                bool giveUp = _openFailures >= MaxOpenFailures;
                Logging.WriteInfo(
                    $"GPU sensor provider failed to open ({_openFailures}/{MaxOpenFailures}): {ex.Message}"
                    + (giveUp ? " Falling back to performance counters for the rest of this session." : string.Empty));
            }

            return false;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                TryCloseQuietly(computer);
                return false;
            }

            _computer = computer;
            _readings = new Dictionary<string, GpuSensorReadings>(StringComparer.OrdinalIgnoreCase);
            _capturedAtUtc = default;
        }

        return true;
    }

    /// <summary>Closes the session. Safe to call when not open.</summary>
    public void Close()
    {
        Computer? computer;
        lock (_lock)
        {
            computer = _computer;
            _computer = null;
            _readings = new Dictionary<string, GpuSensorReadings>(StringComparer.OrdinalIgnoreCase);
            _capturedAtUtc = default;
        }

        TryCloseQuietly(computer);
    }

    /// <summary>Names of every GPU the provider can see.</summary>
    public IReadOnlyList<string> GetHardwareNames()
    {
        var snapshot = GetSnapshot();
        return snapshot.Keys.ToList();
    }

    /// <summary>
    /// Readings for the named GPU. Falls back to the only visible GPU when the name doesn't match,
    /// and returns null when the provider has nothing at all.
    /// </summary>
    public GpuSensorReadings? Read(string? gpuName)
    {
        var snapshot = GetSnapshot();
        if (snapshot.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(gpuName))
        {
            if (snapshot.TryGetValue(gpuName, out var exact))
                return exact;

            var normalizedRequested = Normalize(gpuName);
            var match = snapshot.Values.FirstOrDefault(r => Normalize(r.HardwareName) == normalizedRequested);
            if (match != null)
                return match;

            match = snapshot.Values.FirstOrDefault(r =>
            {
                string candidate = Normalize(r.HardwareName);
                return candidate.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                       normalizedRequested.Contains(candidate, StringComparison.OrdinalIgnoreCase);
            });
            if (match != null)
                return match;

            // A name that matches nothing gets nothing, matching HardwareMonitorService's
            // ResolveGpuInfo and ResolveNvidiaSample. Falling back to the only visible GPU is how
            // the selected iGPU ends up reporting the dGPU's hotspot temperature and board power.
            return null;
        }

        return snapshot.Count == 1 ? snapshot.Values.First() : null;
    }

    /// <summary>A one-line description of what the provider is doing, for diagnostics.</summary>
    public string DescribeStatus()
    {
        if (IsPermanentlyUnavailable)
            return "vendor GPU sensors: unavailable (initialisation failed)";
        if (!IsOpen)
            return "vendor GPU sensors: closed";

        var snapshot = GetSnapshot();
        return snapshot.Count == 0
            ? "vendor GPU sensors: open, no GPUs reported"
            : $"vendor GPU sensors: {string.Join(", ", snapshot.Values.Select(r => $"{r.HardwareName} [{r.Vendor}]"))}";
    }

    private IReadOnlyDictionary<string, GpuSensorReadings> GetSnapshot()
    {
        Computer? computer;
        lock (_lock)
        {
            if (DateTime.UtcNow - _capturedAtUtc < RefreshInterval)
                return _readings;

            computer = _computer;
        }

        if (computer == null)
            return new Dictionary<string, GpuSensorReadings>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, GpuSensorReadings> fresh;
        try
        {
            fresh = ReadAll(computer);
        }
        catch (Exception ex)
        {
            bool shouldLog;
            IReadOnlyDictionary<string, GpuSensorReadings> empty;
            lock (_lock)
            {
                // Stamp the clock on the failure path too. HardwareMonitorService calls this once
                // per sensor, so without throttling here a driver in a bad state makes a single
                // stats tick run ReadAll a dozen-plus times, each enumerating and updating every
                // GPU before throwing again.
                _readings = new Dictionary<string, GpuSensorReadings>(StringComparer.OrdinalIgnoreCase);
                _capturedAtUtc = DateTime.UtcNow;
                shouldLog = !_loggedUnavailable;
                _loggedUnavailable = true;
                empty = _readings;
            }

            if (shouldLog)
                Logging.WriteInfo($"GPU sensor read failed: {ex.Message}");

            return empty;
        }

        lock (_lock)
        {
            _readings = fresh;
            _capturedAtUtc = DateTime.UtcNow;

            // Let a later failure speak up again once the card has recovered.
            _loggedUnavailable = false;
            return _readings;
        }
    }

    private static Dictionary<string, GpuSensorReadings> ReadAll(Computer computer)
    {
        var result = new Dictionary<string, GpuSensorReadings>(StringComparer.OrdinalIgnoreCase);

        foreach (var hardware in computer.Hardware)
        {
            var vendor = MapVendor(hardware.HardwareType);
            if (vendor == GpuVendor.Unknown)
                continue;

            hardware.Update();

            var readings = new GpuSensorReadings
            {
                HardwareName = hardware.Name ?? string.Empty,
                Vendor = vendor,
                CoreLoad = Read(hardware, SensorType.Load, "GPU Core"),
                D3DLoad = Read(hardware, SensorType.Load, "D3D 3D"),
                MemoryLoad = Read(hardware, SensorType.Load, "GPU Memory Controller", "GPU Memory"),
                CoreTemperatureC = Read(hardware, SensorType.Temperature, "GPU Core", "GPU Temperature"),
                HotspotTemperatureC = Read(hardware, SensorType.Temperature, "GPU Hot Spot", "GPU Hotspot"),
                MemoryTemperatureC = Read(hardware, SensorType.Temperature, "GPU Memory"),
                PowerWatts = Read(hardware, SensorType.Power, "GPU Package", "GPU Board Power", "GPU Core"),
                FanPercent = Read(hardware, SensorType.Control, "GPU Fan"),
                CoreClockMhz = Read(hardware, SensorType.Clock, "GPU Core"),
                MemoryClockMhz = Read(hardware, SensorType.Clock, "GPU Memory"),
                // Sensor names differ by vendor: AMD publishes "GPU Memory Used/Total", while
                // NVIDIA only exposes the D3D dedicated-memory counters.
                VramUsedMiB = Read(hardware, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used"),
                VramTotalMiB = Read(hardware, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total"),
            };

            if (!string.IsNullOrWhiteSpace(readings.HardwareName))
                result[readings.HardwareName] = readings;
        }

        return result;
    }

    /// <summary>
    /// Finds a sensor value by type, preferring the given names in order. Falls back to the first
    /// sensor of that type only when no name was requested, so an unrelated sensor can never be
    /// reported as, say, the hotspot temperature.
    /// </summary>
    private static float? Read(IHardware hardware, SensorType type, params string[] preferredNames)
    {
        var candidates = hardware.Sensors.Where(s => s.SensorType == type).ToList();
        if (candidates.Count == 0)
            return null;

        foreach (string name in preferredNames)
        {
            var exact = candidates.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (exact?.Value is { } exactValue)
                return exactValue;
        }

        foreach (string name in preferredNames)
        {
            var partial = candidates.FirstOrDefault(s =>
                s.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
            if (partial?.Value is { } partialValue)
                return partialValue;
        }

        return preferredNames.Length == 0 ? candidates[0].Value : null;
    }

    private static GpuVendor MapVendor(HardwareType type) => type switch
    {
        HardwareType.GpuAmd => GpuVendor.Amd,
        HardwareType.GpuNvidia => GpuVendor.Nvidia,
        HardwareType.GpuIntel => GpuVendor.Intel,
        _ => GpuVendor.Unknown,
    };

    private static string Normalize(string? value)
        => new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray())
            .ToLowerInvariant();

    private static void TryCloseQuietly(Computer? computer)
    {
        if (computer == null)
            return;

        try
        {
            computer.Close();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"GPU sensor provider close failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Close();
    }
}
