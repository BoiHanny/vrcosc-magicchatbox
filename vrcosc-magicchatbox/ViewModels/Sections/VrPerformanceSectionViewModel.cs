using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Vr;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services.Vr;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class VrPerformanceSectionViewModel : ObservableObject
{
    private readonly ISettingsProvider<VrPerformanceSettings> _settingsProvider;
    private readonly Lazy<IModuleHost> _modules;

    public VrPerformanceSectionViewModel(
        ISettingsProvider<VrPerformanceSettings> settingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IntegrationDisplayState integrationDisplay,
        Lazy<IModuleHost> modules)
    {
        _settingsProvider = settingsProvider;
        _modules = modules;
        Settings = settingsProvider.Value;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        IntegrationDisplay = integrationDisplay;

        Settings.PropertyChanged += (_, _) => RefreshPreviews();

        IntegrationDisplay.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IntegrationDisplayState.VrPerformanceStatus))
                OnPropertyChanged(nameof(StatusText));
        };
    }

    private void RefreshPreviews()
    {
        OnPropertyChanged(nameof(HealthyPreview));
        OnPropertyChanged(nameof(DegradedPreview));
        OnPropertyChanged(nameof(HealthyPreviewLength));
        OnPropertyChanged(nameof(DegradedPreviewLength));
    }

    public string HealthyPreview => Preview(degraded: false);

    public string DegradedPreview => Preview(degraded: true);

    public string HealthyPreviewLength => $"{HealthyPreview.Length} chars";

    public string DegradedPreviewLength => $"{DegradedPreview.Length} chars";

    private string Preview(bool degraded)
    {
        string text = VrPerformanceFormatter.Build(
            VrPerformanceFormatter.SampleSnapshot(degraded), Settings, degraded);

        return string.IsNullOrEmpty(text) ? "(nothing shown)" : text;
    }

    public VrPerformanceSettings Settings { get; }
    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public IntegrationDisplayState IntegrationDisplay { get; }

    public IReadOnlyList<VrPerformanceDisplayMode> DisplayModes { get; } =
        (VrPerformanceDisplayMode[])Enum.GetValues(typeof(VrPerformanceDisplayMode));

    public string StatusText
    {
        get
        {
            string live = IntegrationDisplay.VrPerformanceStatus;
            if (!string.IsNullOrWhiteSpace(live) && live != NotStarted)
                return live;

            try
            {
                var runtime = OpenXrRuntimeDetector.Detect();
                return runtime.SupportsFrameTiming
                    ? $"{NotStarted}. {runtime.DescribeForUser()} Turn the integration on to start reading."
                    : $"{NotStarted}. {runtime.DescribeForUser()}";
            }
            catch (Exception)
            {
                return NotStarted;
            }
        }
    }

    private const string NotStarted = "Not started";

    public void Save() => _settingsProvider.Save();
}
