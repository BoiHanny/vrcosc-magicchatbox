using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Driverless hardware monitor built on Windows APIs and WMI. It intentionally avoids
/// kernel-mode sensor drivers such as WinRing0 so opening the app cannot trigger BYOVD alerts.
/// </summary>
public sealed class HardwareMonitorService : IHardwareMonitorService
{
    private readonly object _lock = new();
    private IReadOnlyList<string>? _gpuCache;
    private IReadOnlyList<GpuInfo>? _gpuInfoCache;
    private string? _cpuNameCache;
    private IReadOnlyList<NvidiaSmiSample>? _nvidiaSmiCache;
    private DateTime _nvidiaSmiCapturedAtUtc;
    private bool _nvidiaSmiUnavailable;
    private bool _loggedNvidiaSmiUnavailable;
    private readonly Dictionary<string, PerformanceCounter> _performanceCounters = new(StringComparer.OrdinalIgnoreCase);
    private GpuPerformanceSnapshot _gpuPerformanceSnapshot = GpuPerformanceSnapshot.Empty;
    private DateTime _gpuPerformanceCapturedAtUtc;
    private bool _isOpen;
    private bool _hasPreviousSystemTimes;
    private ulong _previousIdleTime;
    private ulong _previousKernelTime;
    private ulong _previousUserTime;
    private static readonly TimeSpan GpuPerformanceCounterRefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly Regex GpuCounterLuidRegex = new(
        @"luid_0x(?<high>[0-9a-f]+)_0x(?<low>[0-9a-f]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GpuEngineCounterRegex = new(
        @"luid_0x(?<high>[0-9a-f]+)_0x(?<low>[0-9a-f]+)_phys_(?<phys>\d+)_eng_(?<engine>\d+)_engtype_(?<type>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Guid DxgiFactory1Guid = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint DxgiAdapterFlagSoftware = 2;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public LUID AdapterLuid;
        public uint Flags;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        [PreserveSig] int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr unknown);
        [PreserveSig] int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr parent);
        [PreserveSig] int EnumAdapters(uint adapter, out IntPtr adapterPointer);
        [PreserveSig] int MakeWindowAssociation(IntPtr windowHandle, uint flags);
        [PreserveSig] int GetWindowAssociation(out IntPtr windowHandle);
        [PreserveSig] int CreateSwapChain(IntPtr device, IntPtr desc, out IntPtr swapChain);
        [PreserveSig] int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);
        [PreserveSig] int EnumAdapters1(uint adapter, out IDXGIAdapter1 adapterPointer);
        [PreserveSig] int IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        [PreserveSig] int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr unknown);
        [PreserveSig] int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr parent);
        [PreserveSig] int EnumOutputs(uint output, out IntPtr outputPointer);
        [PreserveSig] int GetDesc(out IntPtr desc);
        [PreserveSig] int CheckInterfaceSupport(ref Guid interfaceName, out long userModeVersion);
        [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 desc);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IDXGIFactory1 factory);

    public bool IsOpen
    {
        get { lock (_lock) return _isOpen; }
    }

    public void Open()
    {
        lock (_lock)
        {
            _isOpen = true;
        }

        PrimeCpuBaseline();
    }

    public void Close()
    {
        lock (_lock)
        {
            _isOpen = false;

            // GPU caches
            _gpuCache = null;
            _gpuInfoCache = null;
            _gpuPerformanceSnapshot = GpuPerformanceSnapshot.Empty;
            _gpuPerformanceCapturedAtUtc = default;

            // CPU baselines & name cache — force re-prime on next Open
            _hasPreviousSystemTimes = false;
            _previousIdleTime = 0;
            _previousKernelTime = 0;
            _previousUserTime = 0;
            _cpuNameCache = null;

            // NVIDIA caches + unavailable flags — re-detect on next session
            _nvidiaSmiCache = null;
            _nvidiaSmiCapturedAtUtc = default;
            _nvidiaSmiUnavailable = false;
            _loggedNvidiaSmiUnavailable = false;

            foreach (var counter in _performanceCounters.Values)
                counter.Dispose();
            _performanceCounters.Clear();
        }
    }

    public void UpdateAll()
    {
        // Ensure CPU baseline exists before the first delta read so the first
        // tick after Open() can return a real load value instead of null.
        PrimeCpuBaseline();
        _ = GetGpuPerformanceSnapshot();
    }

    /// <summary>
    /// Captures the initial GetSystemTimes snapshot so the first CPU load delta
    /// is meaningful. Safe to call repeatedly — only writes when no baseline exists.
    /// </summary>
    private void PrimeCpuBaseline()
    {
        try
        {
            lock (_lock)
            {
                if (_hasPreviousSystemTimes)
                    return;
            }

            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                return;

            lock (_lock)
            {
                if (_hasPreviousSystemTimes)
                    return;

                StoreSystemTimes(idleTime.ToUInt64(), kernelTime.ToUInt64(), userTime.ToUInt64());
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"CPU baseline prime error: {ex.Message}");
        }
    }

    public float? GetCpuLoad() => GetCpuLoadBasic();

    public string? GetCpuName()
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(_cpuNameCache))
                return _cpuNameCache;
        }

        string? cpuName = QuerySingleWmiString("Win32_Processor", "Name");
        lock (_lock)
        {
            _cpuNameCache = cpuName;
        }

        return cpuName;
    }

    public float? GetGpuLoad(string gpuName, string sensorName)
    {
        if (sensorName.Contains("D3D", StringComparison.OrdinalIgnoreCase))
            return GetGpuEngineUtilization(gpuName, "3D") ?? ResolveNvidiaSample(gpuName)?.GpuUtilization;

        return ResolveNvidiaSample(gpuName)?.GpuUtilization ??
               GetGpuEngineUtilization(gpuName);
    }

    public float? GetGpuTemperature(string gpuName) => ResolveNvidiaSample(gpuName)?.TemperatureC;

    /// <summary>
    /// GPU hotspot temperature is not exposed by the driverless pipeline
    /// (Windows Performance Counters / DXGI / nvidia-smi don't surface it).
    /// Always returns null. Tracked as follow-up: NVML / ADL / IGCL integration.
    /// </summary>
    public float? GetGpuHotspotTemperature(string gpuName) => null;

    public float? GetGpuPower(string gpuName) => ResolveNvidiaSample(gpuName)?.PowerW;

    public float? GetGpuVramUsed(string gpuName, string sensorName)
        => ResolveNvidiaSample(gpuName)?.MemoryUsedMiB ?? GetGpuDedicatedMemoryUsageMiB(gpuName);

    public float? GetGpuVramTotal(string gpuName, string sensorName)
    {
        var nvidiaSample = ResolveNvidiaSample(gpuName);
        if (nvidiaSample?.MemoryTotalMiB is > 0)
            return nvidiaSample.MemoryTotalMiB;

        var gpu = ResolveGpuInfo(gpuName);
        if (gpu?.AdapterRamBytes is not > 0)
            return null;

        const double bytesToMiB = 1024.0 * 1024.0;
        return (float)(gpu.AdapterRamBytes.Value / bytesToMiB);
    }

    public string? GetGpuName(string gpuName) => ResolveGpuInfo(gpuName)?.Name;

    public float? GetRamUsed()
    {
        var info = GetWindowsMemoryInfo();
        return info.HasValue ? (float)info.Value.usedGiB : null;
    }

    public float? GetRamAvailable()
    {
        var info = GetWindowsMemoryInfo();
        return info.HasValue ? (float)Math.Max(0, info.Value.totalGiB - info.Value.usedGiB) : null;
    }

    /// <summary>
    /// Uses kernel32 GlobalMemoryStatusEx - fast and driverless.
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
            if (_gpuCache is { Count: > 0 })
                return _gpuCache;
        }

        var gpus = GetGpuInfoFromWindows()
            .Select(gpu => gpu.Name)
            .Concat(GetNvidiaSmiSamples().Select(gpu => gpu.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_lock)
        {
            _gpuCache = gpus;
        }

        return gpus;
    }

    /// <summary>
    /// DDR version via WMI - queried lazily and with a timeout so a bad WMI provider
    /// cannot stall the entire app startup path.
    /// </summary>
    public string? GetDdrVersion()
    {
        try
        {
            using var searcher = CreateSearcher("Win32_PhysicalMemory", "SMBIOSMemoryType");

            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["SMBIOSMemoryType"] == null) continue;
                ushort type = Convert.ToUInt16(obj["SMBIOSMemoryType"]);
                string? version = MapSmbiostoDdr(type);
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

    public float? GetCpuLoadBasic()
    {
        try
        {
            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                return null;

            ulong currentIdle = idleTime.ToUInt64();
            ulong currentKernel = kernelTime.ToUInt64();
            ulong currentUser = userTime.ToUInt64();

            lock (_lock)
            {
                if (!_hasPreviousSystemTimes
                    || currentIdle < _previousIdleTime
                    || currentKernel < _previousKernelTime
                    || currentUser < _previousUserTime)
                {
                    StoreSystemTimes(currentIdle, currentKernel, currentUser);
                    return null;
                }

                ulong idleDelta = currentIdle - _previousIdleTime;
                ulong kernelDelta = currentKernel - _previousKernelTime;
                ulong userDelta = currentUser - _previousUserTime;
                StoreSystemTimes(currentIdle, currentKernel, currentUser);

                ulong totalDelta = kernelDelta + userDelta;
                if (totalDelta == 0)
                    return null;

                double idleRatio = idleDelta / (double)totalDelta;
                return (float)Math.Clamp((1d - idleRatio) * 100d, 0d, 100d);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"CPU load read error: {ex.Message}");
            return null;
        }
    }

    private void StoreSystemTimes(ulong idleTime, ulong kernelTime, ulong userTime)
    {
        _previousIdleTime = idleTime;
        _previousKernelTime = kernelTime;
        _previousUserTime = userTime;
        _hasPreviousSystemTimes = true;
    }

    public float? GetGpuFanSpeed(string gpuName) => ResolveNvidiaSample(gpuName)?.FanPercent;

    public float? GetGpuCoreClock(string gpuName) => ResolveNvidiaSample(gpuName)?.GraphicsClockMHz;

    public float? GetGpuMemoryClock(string gpuName) => ResolveNvidiaSample(gpuName)?.MemoryClockMHz;

    public float? GetGpuMemoryTemperature(string gpuName) => ResolveNvidiaSample(gpuName)?.MemoryTemperatureC;

    public float? GetGpuMemoryLoad(string gpuName)
    {
        var sample = ResolveNvidiaSample(gpuName);
        if (sample?.MemoryUtilization is not null)
            return sample.MemoryUtilization;

        float? usedMiB = GetGpuVramUsed(gpuName, string.Empty);
        float? totalMiB = GetGpuVramTotal(gpuName, string.Empty);
        if (usedMiB is null || totalMiB is not > 0)
            return null;

        return (float)Math.Clamp(usedMiB.Value / totalMiB.Value * 100f, 0f, 100f);
    }

    private GpuInfo? ResolveGpuInfo(string? gpuName)
    {
        var gpus = GetGpuInfoFromWindows();
        if (gpus.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(gpuName))
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

        return gpus.FirstOrDefault(g => !g.Name.Contains("integrated", StringComparison.OrdinalIgnoreCase))
               ?? gpus[0];
    }

    private IReadOnlyList<GpuInfo> GetGpuInfoFromWindows()
    {
        lock (_lock)
        {
            if (_gpuInfoCache is { Count: > 0 })
                return _gpuInfoCache;
        }

        try
        {
            var dxgiGpus = GetDxgiGpuInfo();
            var gpus = dxgiGpus.Count > 0 ? dxgiGpus : GetWmiGpuInfo();

            lock (_lock)
            {
                _gpuInfoCache = gpus;
            }

            return gpus;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return Array.Empty<GpuInfo>();
        }
    }

    private static IReadOnlyList<GpuInfo> GetDxgiGpuInfo()
    {
        var factoryGuid = DxgiFactory1Guid;
        int hr = CreateDXGIFactory1(ref factoryGuid, out var factory);
        if (hr < 0 || factory == null)
            return Array.Empty<GpuInfo>();

        var gpus = new List<GpuInfo>();
        try
        {
            for (uint adapterIndex = 0; adapterIndex < 32; adapterIndex++)
            {
                hr = factory.EnumAdapters1(adapterIndex, out var adapter);
                if (hr == DxgiErrorNotFound)
                    break;
                if (hr < 0 || adapter == null)
                    continue;

                try
                {
                    if (adapter.GetDesc1(out var desc) < 0)
                        continue;

                    if ((desc.Flags & DxgiAdapterFlagSoftware) != 0)
                        continue;

                    string name = desc.Description?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    gpus.Add(new GpuInfo(
                        name,
                        desc.DedicatedVideoMemory.ToUInt64(),
                        FormatLuidToken(desc.AdapterLuid),
                        desc.VendorId,
                        desc.DeviceId));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
        }
        catch
        {
            return Array.Empty<GpuInfo>();
        }
        finally
        {
            Marshal.FinalReleaseComObject(factory);
        }

        return gpus
            .DistinctBy(gpu => gpu.LuidToken ?? gpu.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<GpuInfo> GetWmiGpuInfo()
    {
        try
        {
            using var searcher = CreateSearcher("Win32_VideoController", "Name, AdapterRAM, AdapterCompatibility, PNPDeviceID");

            return searcher.Get()
                .Cast<ManagementObject>()
                .Select(obj =>
                {
                    string? name = obj["Name"]?.ToString();
                    return string.IsNullOrWhiteSpace(name)
                        ? null
                        : new GpuInfo(
                            name.Trim(),
                            TryReadUInt64(obj["AdapterRAM"]),
                            null,
                            null,
                            null,
                            obj["AdapterCompatibility"]?.ToString(),
                            obj["PNPDeviceID"]?.ToString());
                })
                .OfType<GpuInfo>()
                .DistinctBy(gpu => gpu.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return Array.Empty<GpuInfo>();
        }
    }

    private float? GetGpuEngineUtilization(string? gpuName, string? engineTypeFilter = null)
    {
        try
        {
            string? luidToken = ResolveGpuInfo(gpuName)?.LuidToken;
            var snapshot = GetGpuPerformanceSnapshot();
            return snapshot.GetEngineUtilization(luidToken, engineTypeFilter);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"GPU engine counter read error: {ex.Message}");
            return null;
        }
    }

    private float? GetGpuDedicatedMemoryUsageMiB(string? gpuName)
    {
        try
        {
            string? luidToken = ResolveGpuInfo(gpuName)?.LuidToken;
            double bytes = GetGpuPerformanceSnapshot().GetDedicatedUsageBytes(luidToken);

            if (bytes <= 0)
                return null;

            const double bytesToMiB = 1024.0 * 1024.0;
            return (float)(bytes / bytesToMiB);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"GPU memory counter read error: {ex.Message}");
            return null;
        }
    }

    private GpuPerformanceSnapshot GetGpuPerformanceSnapshot()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _gpuPerformanceCapturedAtUtc < GpuPerformanceCounterRefreshInterval)
                return _gpuPerformanceSnapshot;
        }

        var snapshot = ReadGpuPerformanceSnapshot();
        lock (_lock)
        {
            _gpuPerformanceSnapshot = snapshot;
            _gpuPerformanceCapturedAtUtc = DateTime.UtcNow;
        }

        return snapshot;
    }

    private GpuPerformanceSnapshot ReadGpuPerformanceSnapshot()
    {
        var rawEngineValues = ReadPerformanceCounterValues("GPU Engine", "Utilization Percentage");
        var engines = new Dictionary<string, GpuEngineMetric>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in rawEngineValues)
        {
            if (!TryParseGpuEngineCounter(pair.Key, out string? luidToken, out string engineKey, out string engineType))
                continue;

            if (engines.TryGetValue(engineKey, out var existing))
            {
                engines[engineKey] = existing with
                {
                    Utilization = Math.Clamp(existing.Utilization + pair.Value, 0f, 100f)
                };
            }
            else
            {
                engines[engineKey] = new GpuEngineMetric(
                    luidToken,
                    engineKey,
                    engineType,
                    Math.Clamp(pair.Value, 0f, 100f));
            }
        }

        var processDedicatedBytes = SumGpuMemoryByLuid(
            ReadPerformanceCounterValues("GPU Process Memory", "Dedicated Usage"));
        var adapterDedicatedBytes = SumGpuMemoryByLuid(
            ReadPerformanceCounterValues("GPU Adapter Memory", "Dedicated Usage"));

        return new GpuPerformanceSnapshot(engines.Values.ToList(), processDedicatedBytes, adapterDedicatedBytes);
    }

    private IReadOnlyDictionary<string, float> ReadPerformanceCounterValues(string categoryName, string counterName)
    {
        string[] instanceNames;
        try
        {
            var category = new PerformanceCounterCategory(categoryName);
            instanceNames = category.GetInstanceNames();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"{categoryName} counter enumeration error: {ex.Message}");
            return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string instanceName in instanceNames)
        {
            string cacheKey = GetPerformanceCounterCacheKey(categoryName, counterName, instanceName);
            activeKeys.Add(cacheKey);
            PerformanceCounter counter;
            bool isNew = false;

            try
            {
                lock (_lock)
                {
                    if (!_performanceCounters.TryGetValue(cacheKey, out counter!))
                    {
                        counter = new PerformanceCounter(categoryName, counterName, instanceName, readOnly: true);
                        _performanceCounters[cacheKey] = counter;
                        isNew = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"{categoryName} counter '{instanceName}' open error: {ex.Message}");
                continue;
            }

            try
            {
                float value = Math.Max(0f, counter.NextValue());
                if (!isNew)
                    values[instanceName] = value;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"{categoryName} counter '{instanceName}' read error: {ex.Message}");
                RemovePerformanceCounter(cacheKey);
            }
        }

        RemoveStalePerformanceCounters(categoryName, counterName, activeKeys);
        return values;
    }

    private void RemovePerformanceCounter(string cacheKey)
    {
        lock (_lock)
        {
            if (_performanceCounters.Remove(cacheKey, out var counter))
                counter.Dispose();
        }
    }

    private void RemoveStalePerformanceCounters(string categoryName, string counterName, HashSet<string> activeKeys)
    {
        string prefix = GetPerformanceCounterCacheKeyPrefix(categoryName, counterName);
        lock (_lock)
        {
            var staleKeys = _performanceCounters.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !activeKeys.Contains(key))
                .ToList();

            foreach (string staleKey in staleKeys)
            {
                if (_performanceCounters.Remove(staleKey, out var counter))
                    counter.Dispose();
            }
        }
    }

    private static Dictionary<string, double> SumGpuMemoryByLuid(IReadOnlyDictionary<string, float> counterValues)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in counterValues)
        {
            string luidToken = TryParseLuidToken(pair.Key) ?? string.Empty;
            result[luidToken] = result.GetValueOrDefault(luidToken) + pair.Value;
        }

        return result;
    }

    private static bool TryParseGpuEngineCounter(string instanceName, out string? luidToken, out string engineKey, out string engineType)
    {
        var match = GpuEngineCounterRegex.Match(instanceName);
        if (!match.Success)
        {
            luidToken = null;
            engineKey = instanceName;
            engineType = string.Empty;
            return false;
        }

        luidToken = NormalizeLuidToken(match.Groups["high"].Value, match.Groups["low"].Value);
        engineType = match.Groups["type"].Value;
        engineKey = $"{luidToken}_phys_{match.Groups["phys"].Value}_eng_{match.Groups["engine"].Value}_engtype_{engineType}";
        return true;
    }

    private static string? TryParseLuidToken(string instanceName)
    {
        var match = GpuCounterLuidRegex.Match(instanceName);
        return match.Success
            ? NormalizeLuidToken(match.Groups["high"].Value, match.Groups["low"].Value)
            : null;
    }

    private static string NormalizeLuidToken(string highHex, string lowHex)
    {
        uint high = uint.TryParse(highHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedHigh)
            ? parsedHigh
            : 0;
        uint low = uint.TryParse(lowHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedLow)
            ? parsedLow
            : 0;

        return $"luid_0x{high:X8}_0x{low:X8}";
    }

    private static string FormatLuidToken(LUID luid)
        => $"luid_0x{unchecked((uint)luid.HighPart):X8}_0x{luid.LowPart:X8}";

    private static string GetPerformanceCounterCacheKey(string categoryName, string counterName, string instanceName)
        => $"{GetPerformanceCounterCacheKeyPrefix(categoryName, counterName)}{instanceName}";

    private static string GetPerformanceCounterCacheKeyPrefix(string categoryName, string counterName)
        => $"{categoryName}|{counterName}|";

    private NvidiaSmiSample? ResolveNvidiaSample(string? gpuName)
    {
        var samples = GetNvidiaSmiSamples();
        if (samples.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(gpuName))
        {
            var match = samples.FirstOrDefault(s => s.Name.Equals(gpuName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            string normalizedRequested = NormalizeHardwareName(gpuName);
            match = samples.FirstOrDefault(s =>
                NormalizeHardwareName(s.Name).Equals(normalizedRequested, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            match = samples.FirstOrDefault(s =>
            {
                string candidate = NormalizeHardwareName(s.Name);
                return candidate.Contains(normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                       normalizedRequested.Contains(candidate, StringComparison.OrdinalIgnoreCase);
            });
            if (match != null) return match;
        }

        return samples[0];
    }

    private IReadOnlyList<NvidiaSmiSample> GetNvidiaSmiSamples()
    {
        lock (_lock)
        {
            if (_nvidiaSmiCache != null &&
                DateTime.UtcNow - _nvidiaSmiCapturedAtUtc < TimeSpan.FromSeconds(2))
            {
                return _nvidiaSmiCache;
            }

            if (_nvidiaSmiUnavailable)
                return Array.Empty<NvidiaSmiSample>();
        }

        IReadOnlyList<NvidiaSmiSample> samples = QueryNvidiaSmi();
        lock (_lock)
        {
            _nvidiaSmiCache = samples;
            _nvidiaSmiCapturedAtUtc = DateTime.UtcNow;
        }

        return samples;
    }

    private IReadOnlyList<NvidiaSmiSample> QueryNvidiaSmi()
    {
        string? executablePath = FindNvidiaSmi();
        if (executablePath == null)
        {
            MarkNvidiaSmiUnavailable("nvidia-smi was not found.");
            return Array.Empty<NvidiaSmiSample>();
        }

        try
        {
            string? output = RunNvidiaSmiQuery(executablePath, includeMemoryTemperature: true);
            output ??= RunNvidiaSmiQuery(executablePath, includeMemoryTemperature: false);
            if (string.IsNullOrWhiteSpace(output))
                return Array.Empty<NvidiaSmiSample>();

            return output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseNvidiaSmiLine)
                .Where(sample => sample != null)
                .Cast<NvidiaSmiSample>()
                .ToList();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"nvidia-smi read error: {ex.Message}");
            return Array.Empty<NvidiaSmiSample>();
        }
    }

    private static string? RunNvidiaSmiQuery(string executablePath, bool includeMemoryTemperature)
    {
        string queryFields = includeMemoryTemperature
            ? "index,name,utilization.gpu,utilization.memory,memory.used,memory.total,temperature.gpu,power.draw,fan.speed,clocks.gr,clocks.mem,temperature.memory"
            : "index,name,utilization.gpu,utilization.memory,memory.used,memory.total,temperature.gpu,power.draw,fan.speed,clocks.gr,clocks.mem";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--query-gpu={queryFields} --format=csv,noheader,nounits",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        process.Start();
        if (!process.WaitForExit(1500))
        {
            process.Kill(entireProcessTree: true);
            return null;
        }

        if (process.ExitCode != 0)
        {
            if (!includeMemoryTemperature)
                Logging.WriteInfo($"nvidia-smi read error: {process.StandardError.ReadToEnd().Trim()}");
            return null;
        }

        return process.StandardOutput.ReadToEnd();
    }

    private static NvidiaSmiSample? ParseNvidiaSmiLine(string line)
    {
        string[] parts = line.Split(',').Select(part => part.Trim()).ToArray();
        if (parts.Length < 11)
            return null;

        return new NvidiaSmiSample(
            ParseNullableInt(parts[0]) ?? 0,
            parts[1],
            ParseNullableFloat(parts[2]),
            ParseNullableFloat(parts[3]),
            ParseNullableFloat(parts[4]),
            ParseNullableFloat(parts[5]),
            ParseNullableFloat(parts[6]),
            ParseNullableFloat(parts[7]),
            ParseNullableFloat(parts[8]),
            ParseNullableFloat(parts[9]),
            ParseNullableFloat(parts[10]),
            parts.Length > 11 ? ParseNullableFloat(parts[11]) : null);
    }

    private static string? FindNvidiaSmi()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string bundledPath = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(bundledPath))
            return bundledPath;

        string systemPath = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
        if (File.Exists(systemPath))
            return systemPath;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidatePath = Path.Combine(directory.Trim().Trim('"'), "nvidia-smi.exe");
                if (File.Exists(candidatePath))
                    return candidatePath;
            }
        }

        return null;
    }

    private void MarkNvidiaSmiUnavailable(string reason)
    {
        lock (_lock)
        {
            _nvidiaSmiUnavailable = true;
            _nvidiaSmiCache = Array.Empty<NvidiaSmiSample>();
            _nvidiaSmiCapturedAtUtc = DateTime.UtcNow;

            if (_loggedNvidiaSmiUnavailable)
                return;

            _loggedNvidiaSmiUnavailable = true;
        }

        if (!string.IsNullOrWhiteSpace(reason))
            Logging.WriteInfo($"nvidia-smi unavailable: {reason.Trim()}");
    }

    private static int? ParseNullableInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static float? ParseNullableFloat(string value)
    {
        if (value.Equals("[Not Supported]", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            return null;

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : null;
    }

    private static ulong? TryReadUInt64(object? value)
    {
        if (value == null)
            return null;

        try
        {
            return Convert.ToUInt64(value);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static string? QuerySingleWmiString(string className, string propertyName)
    {
        try
        {
            using var searcher = CreateSearcher(className, propertyName);
            foreach (ManagementObject obj in searcher.Get())
            {
                string? value = obj[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }

        return null;
    }

    private static ManagementObjectSearcher CreateSearcher(string className, string properties)
    {
        var options = new System.Management.EnumerationOptions
        {
            ReturnImmediately = false,
            Rewindable = false,
            Timeout = TimeSpan.FromSeconds(2)
        };

        return new ManagementObjectSearcher(
            "root\\CIMV2",
            $"SELECT {properties} FROM {className}",
            options);
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

    private static string? MapSmbiostoDdr(ushort smbiosMemoryType) => smbiosMemoryType switch
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

    private sealed record GpuInfo(
        string Name,
        ulong? AdapterRamBytes,
        string? LuidToken = null,
        uint? VendorId = null,
        uint? DeviceId = null,
        string? AdapterCompatibility = null,
        string? PnpDeviceId = null);

    private sealed record GpuEngineMetric(
        string? LuidToken,
        string EngineKey,
        string EngineType,
        float Utilization);

    private sealed class GpuPerformanceSnapshot
    {
        public static readonly GpuPerformanceSnapshot Empty = new(
            Array.Empty<GpuEngineMetric>(),
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));

        private readonly IReadOnlyList<GpuEngineMetric> _engines;
        private readonly IReadOnlyDictionary<string, double> _processDedicatedBytesByLuid;
        private readonly IReadOnlyDictionary<string, double> _adapterDedicatedBytesByLuid;

        public GpuPerformanceSnapshot(
            IReadOnlyList<GpuEngineMetric> engines,
            IReadOnlyDictionary<string, double> processDedicatedBytesByLuid,
            IReadOnlyDictionary<string, double> adapterDedicatedBytesByLuid)
        {
            _engines = engines;
            _processDedicatedBytesByLuid = processDedicatedBytesByLuid;
            _adapterDedicatedBytesByLuid = adapterDedicatedBytesByLuid;
        }

        public float? GetEngineUtilization(string? luidToken, string? engineTypeFilter)
        {
            IEnumerable<GpuEngineMetric> candidates = _engines;
            if (!string.IsNullOrWhiteSpace(luidToken))
                candidates = candidates.Where(engine => engine.LuidToken?.Equals(luidToken, StringComparison.OrdinalIgnoreCase) == true);

            var candidateList = candidates.ToList();
            if (candidateList.Count == 0 && !string.IsNullOrWhiteSpace(luidToken))
                candidateList = _engines.ToList();

            if (candidateList.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(engineTypeFilter))
            {
                var filtered = candidateList
                    .Where(engine => engine.EngineType.Equals(engineTypeFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filtered.Count > 0)
                    return Math.Clamp(filtered.Max(engine => engine.Utilization), 0f, 100f);
            }

            return Math.Clamp(candidateList.Max(engine => engine.Utilization), 0f, 100f);
        }

        public double GetDedicatedUsageBytes(string? luidToken)
        {
            double processBytes = GetUsageBytes(_processDedicatedBytesByLuid, luidToken);
            if (processBytes > 0)
                return processBytes;

            return GetUsageBytes(_adapterDedicatedBytesByLuid, luidToken);
        }

        private static double GetUsageBytes(IReadOnlyDictionary<string, double> bytesByLuid, string? luidToken)
        {
            if (!string.IsNullOrWhiteSpace(luidToken) &&
                bytesByLuid.TryGetValue(luidToken, out double selectedBytes) &&
                selectedBytes > 0)
            {
                return selectedBytes;
            }

            return bytesByLuid.Values.Sum();
        }
    }

    private sealed record NvidiaSmiSample(
        int Index,
        string Name,
        float? GpuUtilization,
        float? MemoryUtilization,
        float? MemoryUsedMiB,
        float? MemoryTotalMiB,
        float? TemperatureC,
        float? PowerW,
        float? FanPercent,
        float? GraphicsClockMHz,
        float? MemoryClockMHz,
        float? MemoryTemperatureC);
}
