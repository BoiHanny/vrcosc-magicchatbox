using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public static class NetworkStatsPreview
{
    private const double SampleDownMbps = 84.3;
    private const double SampleUpMbps = 6.1;
    private const double SampleMaxMbps = 476.9;
    private const double SampleMaxUpMbps = 41.2;
    private const double SampleTotalDownMB = 1840.0;
    private const double SampleTotalUpMB = 212.5;
    private const double SampleUtilization = 17.68;

    public static string Render(NetworkStatsSettings settings)
    {
        if (settings is null)
            return string.Empty;

        var readings = new List<string>(7);

        void Add(bool show, string label, string value)
        {
            if (show)
                readings.Add(new SegmentWriter().Field(Label(settings, label), OscText.Value(value)).Text);
        }

        Add(settings.ShowCurrentDown, "Down", Speed(settings, SampleDownMbps));
        Add(settings.ShowCurrentUp, "Up", Speed(settings, SampleUpMbps));
        Add(settings.ShowMaxDown, "Max Down", Speed(settings, SampleMaxMbps));
        Add(settings.ShowMaxUp, "Max Up", Speed(settings, SampleMaxUpMbps));
        Add(settings.ShowTotalDown, "Total Down", Data(settings, SampleTotalDownMB));
        Add(settings.ShowTotalUp, "Total Up", Data(settings, SampleTotalUpMB));

        if (settings.ShowNetworkUtilization)
        {
            readings.Add(new SegmentWriter()
                .Field(
                    Label(settings, "Network Utilization"),
                    OscText.Value(SampleUtilization.ToString("N2", CultureInfo.CurrentCulture)),
                    Unit(settings, "%"))
                .Text);
        }

        return string.Join(" | ", readings);
    }

    private static OscText Label(NetworkStatsSettings settings, string label)
        => settings.StyledCharacters ? OscText.Label(label) : OscText.Raw(label);

    private static OscText Unit(NetworkStatsSettings settings, string unit)
        => settings.StyledCharacters ? OscText.Unit(unit) : OscText.Raw(unit);

    private static string Measure(NetworkStatsSettings settings, double amount, string unit)
        => new SegmentWriter()
            .Field(OscText.Value(amount.ToString("N2", CultureInfo.CurrentCulture)), Unit(settings, unit))
            .Text;

    private static string Data(NetworkStatsSettings settings, double dataMB)
    {
        if (dataMB < 1) return Measure(settings, dataMB * 1000, "KB");
        if (dataMB >= 1_000_000) return Measure(settings, dataMB / 1e6, "TB");
        if (dataMB >= 1000) return Measure(settings, dataMB / 1000, "GB");

        return Measure(settings, dataMB, "MB");
    }

    private static string Speed(NetworkStatsSettings settings, double speedMbps)
    {
        if (speedMbps < 1) return Measure(settings, speedMbps * 1000, "Kbps");
        if (speedMbps >= 1000) return Measure(settings, speedMbps / 1000, "Gbps");

        return Measure(settings, speedMbps, "Mbps");
    }
}

public partial class NetworkStatisticsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public NetworkStatisticsModule NetworkStatsModule { get; }

    [ObservableProperty] private string _previewLine = string.Empty;

    public NetworkStatisticsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<NetworkStatisticsModule> networkStatsModule)
    {
        AppSettings = appSettingsProvider.Value;
        NetworkStatsModule = networkStatsModule.Value;

        NetworkStatsModule.Settings.PropertyChanged += OnSettingsChanged;
        RefreshPreview();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();

    private void RefreshPreview() => PreviewLine = NetworkStatsPreview.Render(NetworkStatsModule.Settings);
}
