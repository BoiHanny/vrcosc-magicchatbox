using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc;
using Xunit;

using StatExtra = vrcosc_magicchatbox.Classes.Modules.ComponentStatsModule.StatExtra;
using StatReading = vrcosc_magicchatbox.Classes.Modules.ComponentStatsModule.StatReading;
using StatsDetail = vrcosc_magicchatbox.Classes.Modules.ComponentStatsModule.StatsDetail;

namespace MagicChatbox.Tests.Classes.Modules;

public class ComponentStatsSegmentTests
{
    private const string Separator = ComponentStatsModule.DefaultSeparator;
    private const string CpuName = "AMD Ryzen 9 7950X3D 16-Core Processor";
    private const string GpuName = "NVIDIA GeForce RTX 4080";

    /// <summary>
    /// Every output switch this integration has, on at once, with the names real hardware reports:
    /// hardware titles instead of CPU/GPU, full-size names, capacities, the memory generation, the
    /// decimals kept, and all eight GPU readings as words rather than emoji.
    /// </summary>
    private static IReadOnlyList<StatReading> WorstCase() =>
    [
        new StatReading { Name = CpuName, ShortName = "CPU", Value = "100.0", Unit = "﹪" },
        new StatReading
        {
            Name = GpuName,
            ShortName = "GPU",
            Value = "100.0",
            Unit = "﹪",
            CoreExtras =
            [
                new StatExtra("temp", false, "100.0", "°C"),
                new StatExtra("GPU HotSpot", false, "100.0", "°C"),
                new StatExtra("power", false, "450.0", "W"),
            ],
            OtherExtras =
            [
                new StatExtra("mem temp", false, "100.0", "°C"),
                new StatExtra("fan", false, "100", "﹪"),
                new StatExtra("core clk", false, "3105", "MHz"),
                new StatExtra("mem clk", false, "10501", "MHz"),
                new StatExtra("mem load", false, "100.0", "﹪"),
            ],
        },
        new StatReading { Name = GpuName, ShortName = "VRAM", Value = "16.0", Max = "16.0", Unit = "ᵍᵇ" },
        new StatReading { Name = "RAM", ShortName = "RAM", Value = "31.4", Max = "64.0", Unit = "ᵍᵇ", Suffix = "⁽ᴰᴰᴿ⁵⁾" },
    ];

    private static string Write(IReadOnlyList<StatReading> readings, StatsDetail detail)
        => ComponentStatsModule.Render(readings, Separator, detail);

    #region Bounding the segment

    [Fact]
    public void Everything_switched_on_is_nearly_twice_the_whole_line()
    {
        // The reason this integration needed a budget at all. Nothing here is exotic - it is one
        // machine's four components with the sensor panel filled in.
        string full = Write(WorstCase(), StatsDetail.Full);

        Assert.Equal(266, full.Length);
        Assert.True(full.Length > OscBuildContext.MaxOscLength);
    }

    [Theory]
    [InlineData(144)]
    [InlineData(100)]
    [InlineData(60)]
    [InlineData(40)]
    [InlineData(20)]
    [InlineData(8)]
    [InlineData(1)]
    [InlineData(0)]
    public void Whatever_room_is_left_is_what_gets_written(int budget)
    {
        string text = ComponentStatsModule.FitToBudget(WorstCase(), Separator, budget);

        Assert.True(text.Length <= budget, $"budget {budget} produced {text.Length} characters: {text}");
    }

    [Fact]
    public void A_line_of_its_own_buys_the_temperatures_and_the_power_but_not_the_diagnostics()
    {
        string text = ComponentStatsModule.FitToBudget(WorstCase(), Separator, OscBuildContext.MaxOscLength);

        Assert.Equal(Write(WorstCase(), StatsDetail.CoreOnly), text);
        Assert.Equal(100, text.Length);
    }

    [Fact]
    public void What_is_written_fits_the_room_the_line_actually_has_left()
    {
        // An empty candidate already accounts for the separator this segment will need, so asking
        // for it and then subtracting the separator again would under-fill the line by three.
        var context = new OscBuildContext
        {
            CurrentSegments = ["♥ 132 ᵇᵖᵐ", "Down 12,40 Mbps · Up 1,10 Mbps"],
            Separator = " ┆ ",
            Prefix = "hi! ",
            Suffix = " o/",
        };

        string text = ComponentStatsModule.FitToBudget(WorstCase(), Separator, context.RemainingCharsIf(string.Empty));

        Assert.True(context.WouldFit(text), $"{context.LengthIf(text)} characters is over the line");
    }

    [Fact]
    public void With_room_to_spare_nothing_is_dropped()
    {
        var readings = new List<StatReading>
        {
            new() { Name = CpuName, ShortName = "CPU", Value = "7.2", Unit = "﹪" },
        };

        Assert.Equal(Write(readings, StatsDetail.Full), ComponentStatsModule.FitToBudget(readings, Separator, 144));
    }

    #endregion

    #region What goes first

    [Fact]
    public void The_hardware_names_go_first_because_they_never_change()
    {
        string shorter = Write(WorstCase(), StatsDetail.ShortNames);

        Assert.DoesNotContain(GpuName, shorter);
        Assert.DoesNotContain(CpuName, shorter);
        Assert.Contains("CPU: 100.0", shorter);
        Assert.True(shorter.Length < Write(WorstCase(), StatsDetail.Full).Length);
    }

    [Fact]
    public void The_capacity_and_the_memory_generation_go_before_any_live_reading()
    {
        string text = Write(WorstCase(), StatsDetail.NoCapacity);

        Assert.DoesNotContain("/", text);
        Assert.DoesNotContain("⁽ᴰᴰᴿ⁵⁾", text);
        Assert.Contains("RAM: 31.4", text);
        Assert.Contains("mem load", text);
    }

    [Fact]
    public void The_diagnostics_go_before_the_readings_the_component_itself_carries()
    {
        string text = Write(WorstCase(), StatsDetail.CoreOnly);

        Assert.DoesNotContain("mem clk", text);
        Assert.DoesNotContain("core clk", text);
        Assert.DoesNotContain("fan", text);
        Assert.DoesNotContain("mem temp", text);
        Assert.Contains("temp 100.0", text);
        Assert.Contains("power 450.0", text);
    }

    [Fact]
    public void The_four_loads_are_what_survives()
    {
        string text = Write(WorstCase(), StatsDetail.LoadsOnly);

        Assert.DoesNotContain("power", text);
        Assert.Contains("CPU: 100.0", text);
        Assert.Contains("GPU: 100.0", text);
        Assert.Contains("VRAM: 16.0", text);
        Assert.Contains("RAM: 31.4", text);
    }

    [Theory]
    [InlineData("7.2", "7")]
    [InlineData("7,2", "7")]
    [InlineData("100", "100")]
    public void The_last_rung_drops_the_fraction_whichever_culture_wrote_it(string reading, string expected)
    {
        var readings = new List<StatReading>
        {
            new() { Name = "CPU", ShortName = "CPU", Value = reading, Unit = "﹪" },
        };

        Assert.Equal($"CPU: {expected}﹪", Write(readings, StatsDetail.Bare));
    }

    #endregion

    #region The value/label rule

    [Fact]
    public void The_reading_stays_full_size_while_its_name_and_unit_are_raised()
    {
        var readings = new List<StatReading>
        {
            new() { Name = "GPU", ShortName = "GPU", RaiseName = true, Value = "62.4", Unit = "°C" },
        };

        Assert.Equal(
            TextUtilities.TransformToSuperscript("GPU") + " 62.4" + TextUtilities.TransformToSuperscript("°C"),
            Write(readings, StatsDetail.Full));
    }

    [Fact]
    public void A_clock_speed_and_a_fan_speed_no_longer_carry_a_full_size_unit()
    {
        // The two spots that broke the rule: MHz and the fan's percent were written at full size
        // beside raised labels, which is the wrong half of the reading standing out.
        var readings = new List<StatReading>
        {
            new()
            {
                Name = "GPU",
                ShortName = "GPU",
                RaiseName = true,
                Value = "40.0",
                Unit = "﹪",
                OtherExtras =
                [
                    new StatExtra("fan", true, "62", "﹪"),
                    new StatExtra("core clk", true, "3105", "MHz"),
                ],
            },
        };

        string text = Write(readings, StatsDetail.Full);

        Assert.DoesNotContain("MHz", text);
        Assert.DoesNotContain("%", text);
        Assert.Contains("3105" + TextUtilities.TransformToSuperscript("MHz"), text);
    }

    [Fact]
    public void An_emoji_label_is_placed_exactly_as_it_was_written()
    {
        var readings = new List<StatReading>
        {
            new()
            {
                Name = "GPU",
                ShortName = "GPU",
                RaiseName = true,
                Value = "40.0",
                Unit = "﹪",
                CoreExtras = [new StatExtra("⚡", false, "450.0", "W")],
            },
        };

        Assert.Contains("⚡ 450.0" + TextUtilities.TransformToSuperscript("W"), Write(readings, StatsDetail.Full));
    }

    #endregion

    #region Spacing

    [Fact]
    public void A_reading_with_no_unit_leaves_no_gap_where_it_would_have_been()
    {
        var readings = new List<StatReading>
        {
            new() { Name = "CPU", ShortName = "CPU", RaiseName = true, Value = "7.2" },
        };

        Assert.Equal(TextUtilities.TransformToSuperscript("CPU") + " 7.2", Write(readings, StatsDetail.Full));
    }

    [Fact]
    public void No_rung_ever_writes_a_double_space_or_an_edge_space()
    {
        StatsDetail[] rungs =
        [
            StatsDetail.Full, StatsDetail.ShortNames, StatsDetail.NoCapacity,
            StatsDetail.CoreOnly, StatsDetail.LoadsOnly, StatsDetail.Bare,
        ];

        foreach (StatsDetail detail in rungs)
        {
            string text = Write(WorstCase(), detail);

            Assert.DoesNotContain("  ", text);
            Assert.Equal(text.Trim(), text);
        }
    }

    [Fact]
    public void A_component_with_nothing_to_say_does_not_leave_a_separator_behind_it()
    {
        var readings = new List<StatReading>
        {
            new() { Name = "", ShortName = "", Value = "" },
            new() { Name = "CPU", ShortName = "CPU", RaiseName = true, Value = "7.2", Unit = "﹪" },
        };

        Assert.Equal(TextUtilities.TransformToSuperscript("CPU") + " 7.2﹪", Write(readings, StatsDetail.Full));
    }

    #endregion

    #region The separator

    [Theory]
    [InlineData(null, ComponentStatsModule.DefaultSeparator)]
    [InlineData("", ComponentStatsModule.DefaultSeparator)]
    [InlineData("   ", ComponentStatsModule.DefaultSeparator)]
    [InlineData(" | ", " | ")]
    public void An_empty_separator_falls_back_to_the_default(string? configured, string expected)
        => Assert.Equal(expected, ComponentStatsModule.ClampSeparator(configured));

    [Fact]
    public void A_pasted_separator_cannot_crowd_out_the_readings_it_divides()
    {
        string clamped = ComponentStatsModule.ClampSeparator(new string('=', 400));

        Assert.Equal(ComponentStatsModule.MaxSeparatorLength, clamped.Length);
    }

    [Fact]
    public void Clamping_a_separator_never_splits_a_surrogate_pair()
    {
        // A lone half renders as a replacement box and costs a character for nothing.
        string clamped = ComponentStatsModule.ClampSeparator("abc🎵🎵🎵🎵");

        Assert.False(char.IsHighSurrogate(clamped[^1]));
        Assert.True(clamped.Length <= ComponentStatsModule.MaxSeparatorLength);
    }

    #endregion
}
