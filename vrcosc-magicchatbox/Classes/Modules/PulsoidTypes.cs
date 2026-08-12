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

public class HeartRateData
{
    public int HeartRate { get; set; }
    public DateTime MeasuredAt { get; set; }
}

public partial class PulsoidStatisticsResponse
{
    public int average_beats_per_minute { get; set; } = 0;
    public int calories_burned_in_kcal { get; set; } = 0;
    public int maximum_beats_per_minute { get; set; } = 0;
    public int minimum_beats_per_minute { get; set; } = 0;
    public int streamed_duration_in_seconds { get; set; } = 0;
}

public class PulsoidTrendSymbolSet
{
    public string CombinedTrendSymbol => $"{UpwardTrendSymbol} - {DownwardTrendSymbol}";
    public string DownwardTrendSymbol { get; set; } = "↓";
    public string UpwardTrendSymbol { get; set; } = "↑";
}
