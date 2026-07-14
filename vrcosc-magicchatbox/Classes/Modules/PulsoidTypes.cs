using System;
using System.ComponentModel;

namespace vrcosc_magicchatbox.Classes.Modules;

public enum StatisticsTimeRange
{
    [Description("24h")]
    _24h,
    [Description("7d")]
    _7d,
    [Description("30d")]
    _30d
}

/// <summary>Contains a single heart-rate reading and its UTC timestamp.</summary>
public class HeartRateData
{
    public int HeartRate { get; set; }
    public DateTime MeasuredAt { get; set; }
}

/// <summary>Aggregate heart-rate statistics returned by the Pulsoid statistics API.</summary>
public partial class PulsoidStatisticsResponse
{
    public int average_beats_per_minute { get; set; } = 0;
    public int calories_burned_in_kcal { get; set; } = 0;
    public int maximum_beats_per_minute { get; set; } = 0;
    public int minimum_beats_per_minute { get; set; } = 0;
    public int streamed_duration_in_seconds { get; set; } = 0;
}

/// <summary>A paired set of up/down trend symbols shown next to the heart-rate display.</summary>
public class PulsoidTrendSymbolSet
{
    public string CombinedTrendSymbol => $"{UpwardTrendSymbol} - {DownwardTrendSymbol}";
    public string DownwardTrendSymbol { get; set; } = "↓";
    public string UpwardTrendSymbol { get; set; } = "↑";
}
