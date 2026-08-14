using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// The sample line the Tracker battery section previews with. Batteries only report while SteamVR
/// is running, so without this the section is a template editor with a permanently blank preview.
/// </summary>
public sealed class TrackerBatterySamplePreviewTests
{
    private static TrackerBatterySettings Settings() => new() { ShowTrackers = true, MaxEntries = 0 };

    [Fact]
    public void ThePreviewIsNotEmptyWithNoHeadsetConnected()
    {
        Assert.NotEmpty(TrackerBatteryModule.BuildSampleMessage(Settings()));
    }

    [Fact]
    public void EachShowBoxTakesItsDeviceOffTheLine()
    {
        var settings = Settings();
        string all = TrackerBatteryModule.BuildSampleMessage(settings);

        settings.ShowHeadset = false;
        string withoutHeadset = TrackerBatteryModule.BuildSampleMessage(settings);

        settings.ShowControllers = false;
        string alsoWithoutControllers = TrackerBatteryModule.BuildSampleMessage(settings);

        Assert.True(withoutHeadset.Length < all.Length);
        Assert.True(alsoWithoutControllers.Length < withoutHeadset.Length);
    }

    [Fact]
    public void TurningEveryDeviceOffLeavesNothingRatherThanAStrandedSeparator()
    {
        var settings = Settings();
        settings.ShowHeadset = false;
        settings.ShowControllers = false;
        settings.ShowTrackers = false;

        Assert.Equal(string.Empty, TrackerBatteryModule.BuildSampleMessage(settings));
    }

    [Fact]
    public void ThePreviewFollowsTheTemplate()
    {
        var settings = Settings();
        settings.Template = "{name} at {batt} percent";

        Assert.Contains("at 82 percent", TrackerBatteryModule.BuildSampleMessage(settings));
    }

    [Fact]
    public void TheStartAndEndTextWrapTheWholeLineOnceRatherThanEachDevice()
    {
        var settings = Settings();
        settings.Prefix = "BATT";
        settings.Suffix = "END";

        string line = TrackerBatteryModule.BuildSampleMessage(settings);

        Assert.StartsWith("BATT", line);
        Assert.EndsWith("END", line);
        Assert.Equal(1, line.Split("BATT").Length - 1);
    }

    [Fact]
    public void OnlyMentionLowOnesKeepsJustTheDeviceUnderTheThreshold()
    {
        var settings = Settings();
        settings.LowThreshold = 20;
        settings.GlobalEmergency = true;

        string line = TrackerBatteryModule.BuildSampleMessage(settings);

        Assert.Contains("14", line);
        Assert.DoesNotContain("82", line);
    }

    [Fact]
    public void TheLimitOnHowManyDevicesToNameIsHonoured()
    {
        var settings = Settings();
        settings.Separator = " | ";
        settings.MaxEntries = 1;

        Assert.DoesNotContain(" | ", TrackerBatteryModule.BuildSampleMessage(settings));
    }

    [Fact]
    public void SortingLowToHighPutsTheEmptiestDeviceFirst()
    {
        var settings = Settings();
        settings.SortMode = TrackerBatterySortMode.BatteryLowToHigh;

        string line = TrackerBatteryModule.BuildSampleMessage(settings);

        Assert.True(line.IndexOf("14") < line.IndexOf("82"));
    }

    [Fact]
    public void EverySortOrderIsOfferedToTheUserInWordsRatherThanAsACodeName()
    {
        var converter = new EnumDescriptionConverter();

        foreach (TrackerBatterySortMode mode in TrackerBatterySettings.AvailableSortModes)
        {
            object shown = converter.Convert(mode, typeof(string), null!, null!);

            Assert.NotEqual(mode.ToString(), shown);
            Assert.Contains(" ", (string)shown);
        }
    }

    [Fact]
    public void TheSortOrderDescriptionsAreTheOnesTheUserWasAlreadyMeantToSee()
    {
        var converter = new EnumDescriptionConverter();

        Assert.Equal(
            "Battery low to high",
            converter.Convert(TrackerBatterySortMode.BatteryLowToHigh, typeof(string), null!, null!));
    }
}
