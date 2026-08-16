using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class VrcBridgeSectionViewModel : ObservableObject
{
    private readonly ISettingsProvider<VrcBridgeSettings> _settingsProvider;
    private readonly Lazy<IModuleHost> _modules;

    [ObservableProperty] private string _newMutedWorld = string.Empty;
    [ObservableProperty] private string _newBlockedTerm = string.Empty;

    public VrcBridgeSettings Settings { get; }
    public AppSettings AppSettings { get; }

    public VrcBridgeSectionViewModel(
        ISettingsProvider<VrcBridgeSettings> settingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<IModuleHost> modules)
    {
        _settingsProvider = settingsProvider;
        _modules = modules;
        Settings = settingsProvider.Value;
        AppSettings = appSettingsProvider.Value;

        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VrcBridgeSettings.EnableBridge))
                _ = ApplyEnabledStateAsync();

            OnPropertyChanged(nameof(StatusText));
        };
    }

    public string StatusText
    {
        get
        {
            var bridge = _modules.Value.VrcBridge;
            if (bridge == null)
                return "Not started";

            return bridge.StatusMessage;
        }
    }

    public string ReceivePortText
    {
        get
        {
            var bridge = _modules.Value.VrcBridge;
            int bound = bridge?.OscReceivePort ?? 0;

            return bound == 0
                ? "Not listening yet"
                : $"Listening on port {bound}";
        }
    }

    public string TrafficText
    {
        get
        {
            var bridge = _modules.Value.VrcBridge;
            if (bridge == null || !bridge.IsRunning)
                return "Not connected";

            long received = bridge.ParametersReceived;

            return received == 0
                ? "Nothing from VRChat yet"
                : $"{received} values received from your avatar";
        }
    }

    public IReadOnlyList<string> Neighbours => _modules.Value.VrcBridge?.DescribeNeighbours() ?? Array.Empty<string>();

    public string NeighboursText
    {
        get
        {
            var neighbours = Neighbours;

            if (neighbours.Count == 0)
                return "No other OSC apps have announced themselves.";

            return $"Also on this PC: {string.Join(", ", neighbours)}";
        }
    }

    [RelayCommand]
    private void RefreshDiagnostics()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ReceivePortText));
        OnPropertyChanged(nameof(TrafficText));
        OnPropertyChanged(nameof(Neighbours));
        OnPropertyChanged(nameof(NeighboursText));
    }

    public int ParameterCount => AvatarParameterContract.Parameters.Count;

    public IReadOnlyList<AvatarParameter> ControlParameters => AvatarParameterContract.Parameters
        .Where(p => p.Flow == AvatarParameterFlow.AvatarToApp)
        .ToList();

    [RelayCommand]
    private void AddMutedWorld()
    {
        string world = (NewMutedWorld ?? string.Empty).Trim();
        if (world.Length == 0 || Settings.MutedWorlds.Contains(world, StringComparer.OrdinalIgnoreCase))
            return;

        Settings.MutedWorlds.Add(world);
        NewMutedWorld = string.Empty;
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void RemoveMutedWorld(string? world)
    {
        if (world == null)
            return;

        Settings.MutedWorlds.Remove(world);
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void AddBlockedTerm()
    {
        string term = (NewBlockedTerm ?? string.Empty).Trim();
        if (term.Length == 0 || Settings.BlockedTerms.Contains(term, StringComparer.OrdinalIgnoreCase))
            return;

        Settings.BlockedTerms.Add(term);
        NewBlockedTerm = string.Empty;
        _settingsProvider.Save();
    }

    [RelayCommand]
    private void RemoveBlockedTerm(string? term)
    {
        if (term == null)
            return;

        Settings.BlockedTerms.Remove(term);
        _settingsProvider.Save();
    }

    private async System.Threading.Tasks.Task ApplyEnabledStateAsync()
    {
        var bridge = _modules.Value.VrcBridge;
        if (bridge == null)
            return;

        try
        {
            if (Settings.EnableBridge)
                await bridge.StartAsync().ConfigureAwait(false);
            else
                await bridge.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
        }

        OnPropertyChanged(nameof(StatusText));
    }
}
