using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for VRChat Radar (log parser) options.
/// Handles start/stop and exposes log-based settings + live stats.
/// </summary>
public partial class VrcRadarSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public VrcLogSettings RadarSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    [ObservableProperty] private bool _isStarting;
    [ObservableProperty] private string? _selectedWorldPresetName;

    /// <summary>Available display mode options for the ComboBox.</summary>
    public RadarDisplayMode[] DisplayModes { get; } =
    [
        RadarDisplayMode.AlwaysShow,
        RadarDisplayMode.TransientOnly,
        RadarDisplayMode.EventOverlay,
        RadarDisplayMode.JoinLeaveOnly,
        RadarDisplayMode.CompactInfo
    ];

    /// <summary>World template preset names for UI combo box.</summary>
    public string[] WorldPresetNames { get; } = VrcLogSettings.WorldTemplatePresets
        .Select(p => p.Name).ToArray();

    public VrcRadarSectionViewModel(
        ISettingsProvider<AppSettings> appProvider,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<VrcLogSettings> radarProvider,
        Lazy<IModuleHost> moduleHost)
    {
        AppSettings = appProvider.Value;
        IntegrationSettings = intgrProvider.Value;
        RadarSettings = radarProvider.Value;
        _moduleHost = moduleHost;
    }

    partial void OnSelectedWorldPresetNameChanged(string? value)
    {
        if (value == null) return;
        var preset = VrcLogSettings.WorldTemplatePresets.FirstOrDefault(p => p.Name == value);
        if (preset != default)
            RadarSettings.TemplateWorld = preset.Value;
    }

    [RelayCommand]
    private async Task StartRadarAsync()
    {
        var radar = Modules.VrcRadar;
        if (radar == null || ((Services.IModule)radar).IsRunning) return;

        IsStarting = true;
        try
        {
            await radar.StartAsync();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar start error: {ex.Message}");
        }
        finally
        {
            IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task StopRadarAsync()
    {
        var radar = Modules.VrcRadar;
        if (radar == null) return;

        try
        {
            await radar.StopAsync();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar stop error: {ex.Message}");
        }
    }
}
