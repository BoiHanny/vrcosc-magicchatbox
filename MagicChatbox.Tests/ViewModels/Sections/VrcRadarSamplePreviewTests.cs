using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The radar has real data only once you are in VRChat, which is the last moment anyone wants to be
/// editing eight template boxes. These pin the stand-in room, and that the dropdown beside them no
/// longer shows the names the enum members happen to have.
/// </summary>
public sealed class VrcRadarSamplePreviewTests
{
    private static VrcLogSettings Settings() => new();

    [Fact]
    public void Every_stock_preset_renders_with_no_placeholder_left_over()
    {
        var settings = Settings();

        var leftovers = VrcLogSettings.WorldTemplatePresets
            .Select(preset => new { preset.Name, Line = RadarSampleLine.Build(settings, preset.Value) })
            .Where(x => x.Line.Contains('{') || x.Line.Contains('}'))
            .Select(x => x.Name)
            .ToList();

        Assert.True(leftovers.Count == 0, "placeholder printed verbatim by: " + string.Join(", ", leftovers));
    }

    [Fact]
    public void The_stand_in_room_is_recognisable()
    {
        string line = RadarSampleLine.Build(Settings(), "{world} {count}");

        Assert.Equal($"{RadarSampleLine.SampleWorld} 14", line);
    }

    [Fact]
    public void Turning_the_room_type_off_takes_it_out_of_the_preview()
    {
        var settings = Settings();
        Assert.Contains(RadarSampleLine.SampleType, RadarSampleLine.Build(settings, "{world} | {type}"));

        settings.ShowInstanceType = false;

        Assert.DoesNotContain(RadarSampleLine.SampleType, RadarSampleLine.Build(settings, "{world} | {type}"));
    }

    [Fact]
    public void Turning_the_region_off_takes_it_out_of_the_preview()
    {
        var settings = Settings();
        settings.ShowRegion = false;

        Assert.DoesNotContain(RadarSampleLine.SampleRegion, RadarSampleLine.Build(settings, "{world} | {region}"));
    }

    [Fact]
    public void A_field_that_went_empty_does_not_leave_its_separator_behind()
    {
        var settings = Settings();
        settings.ShowInstanceType = false;
        settings.ShowRegion = false;

        Assert.Equal(RadarSampleLine.SampleWorld, RadarSampleLine.Build(settings, "{world} | {type} | {region}"));
    }

    [Fact]
    public void Both_newline_escapes_really_break_the_line()
    {
        Assert.Equal("a\nb", RadarSampleLine.Build(Settings(), @"a\nb"));
        Assert.Equal("a\nb", RadarSampleLine.Build(Settings(), "a/nb"));
    }

    [Fact]
    public void An_empty_template_previews_as_nothing_rather_than_as_leftovers()
    {
        Assert.Equal(string.Empty, RadarSampleLine.Build(Settings(), string.Empty));
        Assert.Equal(string.Empty, RadarSampleLine.Build(Settings(), null));
    }

    [Fact]
    public void Every_display_mode_reads_as_english_in_the_dropdown()
    {
        foreach (RadarDisplayMode mode in Enum.GetValues<RadarDisplayMode>())
        {
            string description = Describe(mode);

            Assert.NotEqual(mode.ToString(), description);
            Assert.DoesNotMatch(new Regex("[a-z][A-Z]"), description);
        }
    }

    [Fact]
    public void No_two_display_modes_read_the_same()
    {
        var descriptions = Enum.GetValues<RadarDisplayMode>().Select(Describe).ToList();

        Assert.Equal(descriptions.Count, descriptions.Distinct(StringComparer.Ordinal).Count());
    }

    private static string Describe(RadarDisplayMode mode)
    {
        var field = typeof(RadarDisplayMode).GetField(mode.ToString());
        Assert.NotNull(field);

        var attribute = (DescriptionAttribute?)Attribute.GetCustomAttribute(field!, typeof(DescriptionAttribute));
        Assert.NotNull(attribute);

        return attribute!.Description;
    }
}
