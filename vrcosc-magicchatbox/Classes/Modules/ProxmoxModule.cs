using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class ProxmoxModule : ObservableObject, IModule
{
    private readonly ISettingsProvider<ProxmoxSettings> _settingsProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly IHardwareMonitorService _hardware;
    private CancellationTokenSource? _pollCancellation;
    private Task? _pollTask;

    [ObservableProperty] private string _statusLine = "Proxmox: not connected";
    [ObservableProperty] private string _lastError = string.Empty;
    [ObservableProperty] private bool _isRunning;

    public ProxmoxModule(
        ISettingsProvider<ProxmoxSettings> settingsProvider,
        IUiDispatcher dispatcher,
        IHardwareMonitorService hardware)
    {
        _settingsProvider = settingsProvider;
        _dispatcher = dispatcher;
        _hardware = hardware;
    }

    public ProxmoxSettings Settings => _settingsProvider.Value;
    public string Name => "Proxmox";
    public bool IsEnabled { get; set; } = true;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_pollTask is { IsCompleted: false })
            return Task.CompletedTask;

        _pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollAsync(_pollCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _pollCancellation?.Cancel();
        if (_pollTask != null)
        {
            try { await _pollTask.WaitAsync(ct); }
            catch (OperationCanceledException) { }
        }

        SetRunning(false);
    }

    public void SaveSettings() => _settingsProvider.Save();

    public void Dispose()
    {
        _pollCancellation?.Cancel();
        _pollCancellation?.Dispose();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        SetRunning(true);
        while (!ct.IsCancellationRequested)
        {
            await RefreshAsync(ct);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(Settings.PollIntervalSeconds, 5, 3600)), ct);
            }
            catch (OperationCanceledException) { break; }
        }

        SetRunning(false);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Settings.ApiToken))
        {
            SetState("Proxmox: add an API token", string.Empty);
            return;
        }

        try
        {
            using var handler = new HttpClientHandler();
            if (Settings.AllowInvalidCertificate)
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Settings.ApiToken.Trim().StartsWith("PVEAPIToken=", StringComparison.OrdinalIgnoreCase)
                ? Settings.ApiToken.Trim()
                : $"PVEAPIToken={Settings.ApiToken.Trim()}");

            string endpoint = Settings.Endpoint.TrimEnd('/');
            var nodes = (string.IsNullOrWhiteSpace(Settings.NodeNames) ? Settings.NodeName : Settings.NodeNames)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var statuses = new System.Collections.Generic.List<NodeStatus>(nodes.Length);

            foreach (string nodeName in nodes)
            {
                string node = Uri.EscapeDataString(nodeName);
                using var response = await client.GetAsync($"{endpoint}/api2/json/nodes/{node}/status", ct);
                response.EnsureSuccessStatusCode();

                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                statuses.Add(ReadStatus(nodeName, document.RootElement.GetProperty("data")));
            }

            var localMemory = _hardware.GetWindowsMemoryInfo();
            statuses.Add(new NodeStatus(
                "Local PC",
                _hardware.GetCpuLoadBasic() ?? 0,
                Environment.ProcessorCount,
                localMemory?.usedGiB is double usedGiB ? (long)(usedGiB * 1073741824d) : 0,
                localMemory?.totalGiB is double totalGiB ? (long)(totalGiB * 1073741824d) : 0,
                0));

            SetState(FormatClusterStatus(statuses), string.Empty);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetState($"Proxmox: unavailable ({ex.Message})", ex.Message);
        }
    }

    private static NodeStatus ReadStatus(string node, JsonElement data)
    {
        double cpu = data.TryGetProperty("cpu", out JsonElement cpuValue) ? cpuValue.GetDouble() * 100 : 0;
        int cpus = data.TryGetProperty("cpuinfo", out JsonElement cpuInfo)
            && cpuInfo.TryGetProperty("cpus", out JsonElement cpuCount)
            ? cpuCount.GetInt32()
            : 1;
        long used = data.TryGetProperty("memory", out JsonElement memory) && memory.TryGetProperty("used", out JsonElement usedValue) ? usedValue.GetInt64() : 0;
        long max = data.TryGetProperty("memory", out memory) && memory.TryGetProperty("total", out JsonElement maxValue) ? maxValue.GetInt64() : 0;
        string load = data.TryGetProperty("loadavg", out JsonElement loadValue) && loadValue.ValueKind == JsonValueKind.Array && loadValue.GetArrayLength() > 0
            ? loadValue[0].GetString() ?? "?"
            : "?";

        double.TryParse(load, NumberStyles.Float, CultureInfo.InvariantCulture, out double loadValueNumber);
        return new NodeStatus(node, cpu, cpus, used, max, loadValueNumber);
    }

    private static string FormatClusterStatus(System.Collections.Generic.IReadOnlyList<NodeStatus> statuses)
    {
        double totalCpuCapacity = 0;
        double totalCpuUse = 0;
        long usedMemory = 0;
        long totalMemory = 0;
        double totalLoad = 0;

        foreach (NodeStatus status in statuses)
        {
            totalCpuCapacity += status.Cpus;
            totalCpuUse += status.CpuPercent / 100d * status.Cpus;
            usedMemory += status.UsedMemory;
            totalMemory += status.TotalMemory;
            totalLoad += status.Load;
        }

        double cpuPercent = totalCpuCapacity > 0 ? totalCpuUse / totalCpuCapacity * 100 : 0;
        string ram = totalMemory > 0 ? $"{usedMemory / 1073741824d:F1}/{totalMemory / 1073741824d:F1}GB" : "?";
        return $"Server Specs: {statuses.Count} nodes | vCPU {totalCpuCapacity:0} | CPU {cpuPercent:F0}% | RAM {ram} | Load {totalLoad:F2}";
    }

    private readonly record struct NodeStatus(
        string Name,
        double CpuPercent,
        int Cpus,
        long UsedMemory,
        long TotalMemory,
        double Load);

    private void SetState(string line, string error)
        => _dispatcher.BeginInvoke(() =>
        {
            StatusLine = line;
            LastError = error;
        });

    private void SetRunning(bool running)
        => _dispatcher.BeginInvoke(() => IsRunning = running);
}
