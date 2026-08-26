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
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services.Hardware;

namespace vrcosc_magicchatbox.Services;

public sealed partial class HardwareMonitorService : IHardwareMonitorService
{
    private readonly object _lock = new();
    private readonly object _nvidiaSmiQueryLock = new();
    private readonly LhmGpuSensorProvider _vendorGpu = new();
    private IReadOnlyList<string>? _gpuCache;
    private IReadOnlyList<GpuInfo>? _gpuInfoCache;
    private string? _cpuNameCache;
    private IReadOnlyList<NvidiaSmiSample>? _nvidiaSmiCache;
    private DateTime _nvidiaSmiCapturedAtUtc;
    private bool _nvidiaSmiUnavailable;
    private bool _loggedNvidiaSmiUnavailable;
    private int _nvidiaSmiFailures;
    private const int MaxNvidiaSmiFailures = 3;
    private DateTime _nvidiaSmiRetryAfterUtc;
    private bool? _nvidiaSmiSupportsMemoryTemperature;

    private static readonly TimeSpan NvidiaSmiFailureCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NvidiaSmiQueryTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan MinimumNvidiaSmiSampleTtl = TimeSpan.FromSeconds(5);
    private const int NvidiaSmiSampleTtlTickFactor = 2;
    private static readonly TimeSpan DefaultStatsTickInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _statsTickInterval = DefaultStatsTickInterval;
    private readonly Dictionary<string, PerformanceCounter> _performanceCounters = new(StringComparer.OrdinalIgnoreCase);
    private GpuPerformanceSnapshot _gpuPerformanceSnapshot = GpuPerformanceSnapshot.Empty;
    private DateTime _gpuPerformanceCapturedAtUtc;
    private string? _gpuPerformanceSnapshotLuidToken;
    private int _gpuPerformanceRefreshInFlight;
    private bool _isOpen;
    private bool _hasPreviousSystemTimes;
    private ulong _previousIdleTime;
    private ulong _previousKernelTime;
    private ulong _previousUserTime;
    private static readonly TimeSpan GpuPerformanceCounterRefreshInterval = TimeSpan.FromSeconds(5);
    private const string GpuEngineCategory = "GPU Engine";
    private const string GpuProcessMemoryCategory = "GPU Process Memory";
    private const string GpuAdapterMemoryCategory = "GPU Adapter Memory";
    private const string GpuEngineTypeMarker = "_engtype_";
    private const int MaxGpuCounterInstances = 64;
    private static readonly string[] ConsumedGpuEngineTypes =
    {
        "3D",
        "Graphics",
        "Compute",
        "Cuda",
        "VideoDecode",
        "VideoEncode",
        "VideoProcessing",
        "Copy",
    };
    [GeneratedRegex(
        @"luid_0x(?<high>[0-9a-f]+)_0x(?<low>[0-9a-f]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GpuCounterLuidRegex();

    [GeneratedRegex(
        @"luid_0x(?<high>[0-9a-f]+)_0x(?<low>[0-9a-f]+)_phys_(?<phys>\d+)_eng_(?<engine>\d+)_engtype_(?<type>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GpuEngineCounterRegex();
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

        if (VendorGpuSensorsEnabled)
            _vendorGpu.TryOpen();
    }

    public void Close()
    {
        _vendorGpu.Close();

        lock (_lock)
        {
            _isOpen = false;

            _gpuCache = null;
            _gpuInfoCache = null;
            _gpuPerformanceSnapshot = GpuPerformanceSnapshot.Empty;
            _gpuPerformanceCapturedAtUtc = default;
            _gpuPerformanceSnapshotLuidToken = null;

            foreach (var counter in _performanceCounters.Values)
                counter.Dispose();
            _performanceCounters.Clear();

            _hasPreviousSystemTimes = false;
            _previousIdleTime = 0;
            _previousKernelTime = 0;
            _previousUserTime = 0;
            _cpuNameCache = null;

            _nvidiaSmiCache = null;
            _nvidiaSmiCapturedAtUtc = default;
            _nvidiaSmiFailures = 0;
            _nvidiaSmiRetryAfterUtc = default;
            _nvidiaSmiSupportsMemoryTemperature = null;
        }
    }

    public void UpdateAll()
    {
        PrimeCpuBaseline();
    }

    public bool VendorGpuSensorsEnabled { get; set; } = true;

    public TimeSpan StatsTickInterval
    {
        get { lock (_lock) return _statsTickInterval; }
        set
        {
            lock (_lock)
            {
                _statsTickInterval = value > TimeSpan.Zero ? value : DefaultStatsTickInterval;
            }
        }
    }

    private TimeSpan NvidiaSmiSampleTtl
        => TimeSpan.FromTicks(Math.Max(
            MinimumNvidiaSmiSampleTtl.Ticks,
            _statsTickInterval.Ticks * NvidiaSmiSampleTtlTickFactor));

    private bool VendorGpuActive => VendorGpuSensorsEnabled && !_vendorGpu.IsPermanentlyUnavailable;

    private float? ReadVendorSensor(string? gpuName, Func<GpuSensorReadings, float?> selector)
    {
        if (!VendorGpuActive)
            return null;

        if (!_vendorGpu.IsOpen && !_vendorGpu.TryOpen())
            return null;

        var readings = _vendorGpu.Read(gpuName ?? ResolveGpuInfo(null)?.Name);
        return readings == null ? null : selector(readings);
    }

    public string GetHardwareMonitorStatusMessage()
    {
        var adapters = GetGpuInfoFromWindows();
        string adapterList = adapters.Count == 0
            ? "no adapters enumerated"
            : string.Join("; ", adapters.Select(a =>
                $"{a.Name} [vendor 0x{a.VendorId ?? 0:X4}, {(a.AdapterRamBytes ?? 0) / (1024 * 1024)} MiB]"));

        var selected = ResolveGpuInfo(null);

        string counters;
        lock (_lock)
        {
            counters = _gpuPerformanceCapturedAtUtc == default
                ? "GPU Engine counters: not read yet"
                : _gpuPerformanceSnapshot.IsEmpty
                    ? "GPU Engine counters: no data"
                    : "GPU Engine counters: ok";
        }

        return $"adapters: {adapterList} | selected: {selected?.Name ?? "none"} | "
             + $"{_vendorGpu.DescribeStatus()} | {counters}";
    }

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
        bool wants3D = sensorName.Contains("D3D", StringComparison.OrdinalIgnoreCase);

        float? vendor = wants3D
            ? ReadVendorSensor(gpuName, r => r.D3DLoad ?? r.CoreLoad)
            : ReadVendorSensor(gpuName, r => r.CoreLoad);
        if (vendor != null)
            return vendor;

        if (wants3D)
            return GetGpuEngineUtilization(gpuName, "3D") ?? ResolveNvidiaSample(gpuName)?.GpuUtilization;

        return ResolveNvidiaSample(gpuName)?.GpuUtilization ??
               GetGpuEngineUtilization(gpuName);
    }

    public float? GetGpuTemperature(string gpuName)
        => ReadVendorSensor(gpuName, r => r.CoreTemperatureC)
           ?? ResolveNvidiaSample(gpuName)?.TemperatureC;

    public float? GetGpuHotspotTemperature(string gpuName)
        => ReadVendorSensor(gpuName, r => r.HotspotTemperatureC);

    public float? GetGpuPower(string gpuName)
        => ReadVendorSensor(gpuName, r => r.PowerWatts)
           ?? ResolveNvidiaSample(gpuName)?.PowerW;

    public float? GetGpuVramUsed(string gpuName, string sensorName)
        => ReadVendorSensor(gpuName, r => r.VramUsedMiB)
           ?? ResolveNvidiaSample(gpuName)?.MemoryUsedMiB
           ?? GetGpuDedicatedMemoryUsageMiB(gpuName);

    public float? GetGpuVramTotal(string gpuName, string sensorName)
    {
        float? vendorTotal = ReadVendorSensor(gpuName, r => r.VramTotalMiB);
        if (vendorTotal is > 0)
            return vendorTotal;

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

    public void Dispose()
    {
        Close();
        _vendorGpu.Dispose();

        lock (_lock)
        {
            foreach (var counter in _performanceCounters.Values)
                counter.Dispose();
            _performanceCounters.Clear();

            _nvidiaSmiUnavailable = false;
            _loggedNvidiaSmiUnavailable = false;
            _nvidiaSmiFailures = 0;
            _nvidiaSmiRetryAfterUtc = default;
        }
    }

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

    public float? GetGpuFanSpeed(string gpuName)
        => ReadVendorSensor(gpuName, r => r.FanPercent)
           ?? ResolveNvidiaSample(gpuName)?.FanPercent;

    public float? GetGpuCoreClock(string gpuName)
        => ReadVendorSensor(gpuName, r => r.CoreClockMhz)
           ?? ResolveNvidiaSample(gpuName)?.GraphicsClockMHz;

    public float? GetGpuMemoryClock(string gpuName)
        => ReadVendorSensor(gpuName, r => r.MemoryClockMhz)
           ?? ResolveNvidiaSample(gpuName)?.MemoryClockMHz;

    public float? GetGpuMemoryTemperature(string gpuName)
        => ReadVendorSensor(gpuName, r => r.MemoryTemperatureC)
           ?? ResolveNvidiaSample(gpuName)?.MemoryTemperatureC;

    public float? GetGpuMemoryLoad(string gpuName)
    {
        float? vendorLoad = ReadVendorSensor(gpuName, r => r.MemoryLoad);
        if (vendorLoad is not null)
            return vendorLoad;

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

            return match;
        }

        return GpuAdapterSelector.SelectPrimary(gpus) ?? gpus[0];
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
                        desc.DeviceId)
                    {
                        AdapterIndex = adapterIndex,
                        Flags = desc.Flags,
                    });
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
                    string? adapterCompatibility = obj["AdapterCompatibility"]?.ToString();
                    string? pnpDeviceId = obj["PNPDeviceID"]?.ToString();
                    return string.IsNullOrWhiteSpace(name)
                        ? null
                        : new GpuInfo(
                            name.Trim(),
                            TryReadUInt64(obj["AdapterRAM"]),
                            null,
                            GpuVendors.ParseWmiVendorId(pnpDeviceId, adapterCompatibility),
                            null,
                            adapterCompatibility,
                            pnpDeviceId);
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
            var snapshot = GetGpuPerformanceSnapshot(luidToken);
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
            double bytes = GetGpuPerformanceSnapshot(luidToken).GetDedicatedUsageBytes(luidToken);

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

    private GpuPerformanceSnapshot GetGpuPerformanceSnapshot(string? luidToken)
    {
        lock (_lock)
        {
            if (IsGpuPerformanceSnapshotFresh(luidToken))
                return _gpuPerformanceSnapshot;
        }

        if (Interlocked.CompareExchange(ref _gpuPerformanceRefreshInFlight, 1, 0) != 0)
        {
            lock (_lock)
                return _gpuPerformanceSnapshot;
        }

        try
        {
            lock (_lock)
            {
                if (IsGpuPerformanceSnapshotFresh(luidToken))
                    return _gpuPerformanceSnapshot;
            }

            var snapshot = ReadGpuPerformanceSnapshot(luidToken);

            lock (_lock)
            {
                _gpuPerformanceSnapshot = snapshot;
                _gpuPerformanceCapturedAtUtc = DateTime.UtcNow;
                _gpuPerformanceSnapshotLuidToken = luidToken;
            }

            return snapshot;
        }
        finally
        {
            Interlocked.Exchange(ref _gpuPerformanceRefreshInFlight, 0);
        }
    }

    private bool IsGpuPerformanceSnapshotFresh(string? luidToken)
        => _gpuPerformanceCapturedAtUtc != default
           && DateTime.UtcNow - _gpuPerformanceCapturedAtUtc < GpuPerformanceCounterRefreshInterval
           && string.Equals(_gpuPerformanceSnapshotLuidToken, luidToken, StringComparison.OrdinalIgnoreCase);

    private GpuPerformanceSnapshot ReadGpuPerformanceSnapshot(string? luidToken)
    {
        var rawEngineValues = ReadPerformanceCounterValues(GpuEngineCategory, "Utilization Percentage", luidToken);
        var engines = new Dictionary<string, GpuEngineMetric>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in rawEngineValues)
        {
            if (!TryParseGpuEngineCounter(pair.Key, out string? engineLuidToken, out string engineKey, out string engineType))
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
                    engineLuidToken,
                    engineKey,
                    engineType,
                    Math.Clamp(pair.Value, 0f, 100f));
            }
        }

        var processDedicatedBytes = SumGpuMemoryByLuid(
            ReadPerformanceCounterValues(GpuProcessMemoryCategory, "Dedicated Usage", luidToken));
        var adapterDedicatedBytes = SumGpuMemoryByLuid(
            ReadPerformanceCounterValues(GpuAdapterMemoryCategory, "Dedicated Usage", luidToken));

        return new GpuPerformanceSnapshot(engines.Values.ToList(), processDedicatedBytes, adapterDedicatedBytes);
    }

    private IReadOnlyDictionary<string, float> ReadPerformanceCounterValues(string categoryName, string counterName, string? luidToken)
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

        foreach (string instanceName in SelectGpuCounterInstances(categoryName, instanceNames, luidToken))
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

    private static List<string> SelectGpuCounterInstances(string categoryName, string[] instanceNames, string? luidToken)
    {
        bool filterEngineTypes = categoryName.Equals(GpuEngineCategory, StringComparison.OrdinalIgnoreCase);
        bool filterLuid = !string.IsNullOrWhiteSpace(luidToken);
        var matches = new List<string>();

        foreach (string instanceName in instanceNames)
        {
            if (filterEngineTypes && GetConsumedGpuEngineTypeIndex(GetGpuEngineType(instanceName)) < 0)
                continue;

            if (filterLuid &&
                !string.Equals(TryParseLuidToken(instanceName), luidToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(instanceName);
        }

        if (matches.Count <= MaxGpuCounterInstances)
            return matches;

        return matches
            .OrderBy(GetGpuEngineTypePriority)
            .Take(MaxGpuCounterInstances)
            .ToList();
    }

    private static string GetGpuEngineType(string instanceName)
    {
        int markerIndex = instanceName.LastIndexOf(GpuEngineTypeMarker, StringComparison.OrdinalIgnoreCase);
        return markerIndex < 0
            ? string.Empty
            : instanceName[(markerIndex + GpuEngineTypeMarker.Length)..];
    }

    private static int GetConsumedGpuEngineTypeIndex(string engineType)
    {
        if (engineType.Length == 0)
            return -1;

        for (int index = 0; index < ConsumedGpuEngineTypes.Length; index++)
        {
            if (engineType.StartsWith(ConsumedGpuEngineTypes[index], StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static int GetGpuEngineTypePriority(string instanceName)
    {
        int index = GetConsumedGpuEngineTypeIndex(GetGpuEngineType(instanceName));
        return index < 0 ? ConsumedGpuEngineTypes.Length : index;
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
        var match = GpuEngineCounterRegex().Match(instanceName);
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
        var match = GpuCounterLuidRegex().Match(instanceName);
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
        if (!string.IsNullOrWhiteSpace(gpuName) &&
            GpuVendors.FromVendorId(ResolveGpuInfo(gpuName)?.VendorId) != GpuVendor.Nvidia)
        {
            return null;
        }

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

            return match;
        }

        return samples.Count == 1 ? samples[0] : null;
    }

    private IReadOnlyList<NvidiaSmiSample> GetNvidiaSmiSamples()
    {
        if (!HasNvidiaAdapter())
            return Array.Empty<NvidiaSmiSample>();

        lock (_nvidiaSmiQueryLock)
        {
            lock (_lock)
            {
                if (_nvidiaSmiCache != null &&
                    DateTime.UtcNow - _nvidiaSmiCapturedAtUtc < NvidiaSmiSampleTtl)
                {
                    return _nvidiaSmiCache;
                }

                if (_nvidiaSmiUnavailable)
                    return Array.Empty<NvidiaSmiSample>();

                if (_nvidiaSmiRetryAfterUtc != default)
                {
                    if (DateTime.UtcNow < _nvidiaSmiRetryAfterUtc)
                        return Array.Empty<NvidiaSmiSample>();

                    _nvidiaSmiRetryAfterUtc = default;
                    _nvidiaSmiFailures = 0;
                }
            }

            IReadOnlyList<NvidiaSmiSample> samples = QueryNvidiaSmiAsync().GetAwaiter().GetResult();
            lock (_lock)
            {
                _nvidiaSmiCache = samples;
                _nvidiaSmiCapturedAtUtc = DateTime.UtcNow;
            }

            return samples;
        }
    }

    private async Task<IReadOnlyList<NvidiaSmiSample>> QueryNvidiaSmiAsync()
    {
        string? executablePath = FindNvidiaSmi();
        if (executablePath == null)
        {
            MarkNvidiaSmiUnavailable("nvidia-smi was not found.");
            return Array.Empty<NvidiaSmiSample>();
        }

        try
        {
            bool? supportsMemoryTemperature;
            lock (_lock)
            {
                supportsMemoryTemperature = _nvidiaSmiSupportsMemoryTemperature;
            }

            string? output = null;
            if (supportsMemoryTemperature != false)
                output = await RunNvidiaSmiQueryAsync(executablePath, includeMemoryTemperature: true).ConfigureAwait(false);

            bool readMemoryTemperature = !string.IsNullOrWhiteSpace(output);
            if (!readMemoryTemperature)
                output = await RunNvidiaSmiQueryAsync(executablePath, includeMemoryTemperature: false).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(output))
            {
                RecordNvidiaSmiFailure();
                return Array.Empty<NvidiaSmiSample>();
            }

            var samples = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseNvidiaSmiLine)
                .Where(sample => sample != null)
                .Cast<NvidiaSmiSample>()
                .ToList();

            lock (_lock)
            {
                _nvidiaSmiFailures = 0;
                _nvidiaSmiRetryAfterUtc = default;

                if (supportsMemoryTemperature != false)
                {
                    _nvidiaSmiSupportsMemoryTemperature = readMemoryTemperature
                        && samples.Any(sample => sample.MemoryTemperatureC != null);
                }
            }

            return samples;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"nvidia-smi read error: {ex.Message}");
            RecordNvidiaSmiFailure();
            return Array.Empty<NvidiaSmiSample>();
        }
    }

    private bool HasNvidiaAdapter()
        => GetGpuInfoFromWindows().Any(gpu => GpuVendors.FromVendorId(gpu.VendorId) == GpuVendor.Nvidia);

    private void RecordNvidiaSmiFailure()
    {
        int failures;
        lock (_lock)
        {
            failures = ++_nvidiaSmiFailures;
            if (failures < MaxNvidiaSmiFailures)
                return;

            _nvidiaSmiRetryAfterUtc = DateTime.UtcNow + NvidiaSmiFailureCooldown;
        }

        Logging.WriteInfo(
            $"nvidia-smi produced no usable output after {failures} attempts; pausing it for " +
            $"{NvidiaSmiFailureCooldown.TotalMinutes:F0} minutes.");
    }

    private static async Task<string?> RunNvidiaSmiQueryAsync(string executablePath, bool includeMemoryTemperature)
    {
        string queryFields = includeMemoryTemperature
            ? "index,name,utilization.gpu,utilization.memory,memory.used,memory.total,temperature.gpu,power.draw,fan.speed,clocks.gr,clocks.mem,temperature.memory"
            : "index,name,utilization.gpu,utilization.memory,memory.used,memory.total,temperature.gpu,power.draw,fan.speed,clocks.gr,clocks.mem";

        using var queryTimeout = new CancellationTokenSource(NvidiaSmiQueryTimeout);
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

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(queryTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillNvidiaSmiProcess(process);
            await DrainNvidiaSmiOutputAsync(standardOutput, standardError).ConfigureAwait(false);
            return null;
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            if (!includeMemoryTemperature)
                Logging.WriteInfo($"nvidia-smi read error: {error.Trim()}");
            return null;
        }

        return output;
    }

    private static void KillNvidiaSmiProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"nvidia-smi terminate error: {ex.Message}");
        }
    }

    private static async Task DrainNvidiaSmiOutputAsync(Task<string> standardOutput, Task<string> standardError)
    {
        try
        {
            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"nvidia-smi output drain error: {ex.Message}");
        }
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
        string? PnpDeviceId = null) : IGpuAdapter
    {
        public uint AdapterIndex { get; init; }

        public uint Flags { get; init; }

        ulong? IGpuAdapter.DedicatedVideoMemoryBytes => AdapterRamBytes;

        bool IGpuAdapter.IsSoftwareAdapter => (Flags & DxgiAdapterFlagSoftware) != 0;
    }

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

        public bool IsEmpty => _engines.Count == 0;

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
