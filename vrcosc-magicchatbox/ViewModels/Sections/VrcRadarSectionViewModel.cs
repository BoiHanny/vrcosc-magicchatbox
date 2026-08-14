using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class VrcRadarSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public VrcLogSettings RadarSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    [ObservableProperty] private bool _isStarting;
    [ObservableProperty] private string? _selectedWorldPresetName;

    /// <summary>The world line as it would read from a stand-in room.</summary>
    [ObservableProperty] private string _worldSamplePreview = string.Empty;

    /// <summary>The same for someone walking in, which is the line people see most.</summary>
    [ObservableProperty] private string _joinSamplePreview = string.Empty;

    /// <summary>And the end-of-session line, which nobody can trigger on demand to check.</summary>
    [ObservableProperty] private string _sessionStatsSamplePreview = string.Empty;

    public RadarDisplayMode[] DisplayModes { get; } =
    [
        RadarDisplayMode.AlwaysShow,
        RadarDisplayMode.TransientOnly,
        RadarDisplayMode.EventOverlay,
        RadarDisplayMode.JoinLeaveOnly,
        RadarDisplayMode.CompactInfo
    ];

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

        RadarSettings.PropertyChanged += OnRadarSettingsChanged;
        RefreshPreviews();
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

    private void OnRadarSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreviews();

    private void RefreshPreviews()
    {
        WorldSamplePreview = RadarSampleLine.Build(RadarSettings, RadarSettings.TemplateWorld);
        JoinSamplePreview = RadarSampleLine.Build(RadarSettings, RadarSettings.TemplateJoin);
        SessionStatsSamplePreview = RadarSampleLine.Build(RadarSettings, RadarSettings.TemplateSessionStats);
    }
}

/// <summary>
/// Fills a radar template with a stand-in room so the settings page can show a finished line.
/// </summary>
/// <remarks>
/// The module's own renderer needs a live VRChat log behind it and a room to be in, which is the
/// one thing a user configuring the templates does not have. This mirrors the tidy-up the module
/// applies on the way out - the empty-field collapse in particular, because turning the instance
/// type off is exactly the kind of change whose effect the user cannot otherwise see.
/// </remarks>
public static class RadarSampleLine
{
    public const string SampleWorld = "Midnight Rooftop";
    public const string SamplePlayer = "Robin";
    public const string SampleOwner = "Sam";
    public const string SampleType = "Public";
    public const string SampleRegion = "EU";

    public static string Build(VrcLogSettings settings, string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        string text = template
            .Replace("{master}", settings.MasterIcon ?? string.Empty)
            .Replace("{world}", SampleWorld)
            .Replace("{count}", "14")
            .Replace("{peak}", "22")
            .Replace("{peak_session}", "22")
            .Replace("{session_time}", VrcLogText.Duration(TimeSpan.FromMinutes(72)))
            .Replace("{app_session}", VrcLogText.Duration(TimeSpan.FromMinutes(185)))
            .Replace("{offline}", VrcLogText.Duration(TimeSpan.FromMinutes(8)))
            .Replace("{owner}", SampleOwner)
            .Replace("{user}", SamplePlayer)
            .Replace("{size}", "42MB")
            .Replace("{speed}", "8.1MB/s")
            .Replace("{worlds}", "3")
            .Replace("{players}", "27");

        // These two are the switches directly above the template box, so the preview has to obey
        // them or the switches look dead.
        text = text.Replace("{type}", settings.ShowInstanceType ? SampleType : string.Empty);
        text = text.Replace("{region}", settings.ShowRegion ? SampleRegion : string.Empty);

        text = Regex.Replace(text, @"\s*\|\s*\|\s*", " | ");
        text = Regex.Replace(text, @"(\s*\|\s*)+$", "");
        text = Regex.Replace(text, @"^\s*\|\s*", "");
        text = Regex.Replace(text, @"\s{2,}", " ");
        text = text.Trim();

        return text.Replace("\\n", "\n").Replace("/n", "\n");
    }
}
