using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Models;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class TrackerBatteryEntryTests
{
    private static TrackerBatterySettings Settings(bool small) => new() { UseSmallText = small };

    private static TrackerDevice Device(float level = 0.875f, bool charging = false) => new()
    {
        SerialNumber = "LHR-1234",
        OriginalModelName = "Index Controller",
        DeviceKind = "Controller",
        CustomIcon = "🎮",
        IsConnected = true,
        IsCharging = charging,
        BatteryLevel = level,
    };

    private static string Build(TrackerBatterySettings settings, string template = "{icon} {name} {batt}%", bool isLow = false)
        => TrackerBatteryModule.BuildEntry(Device(), template, settings, isLow);

    [Fact]
    public void TheBatteryPercentageStaysFullSizeWhileTheNameShrinks()
    {
        string text = Build(Settings(small: true));

        Assert.Contains("87", text);
        Assert.DoesNotContain("⁸⁷", text);
        Assert.Contains("ⁱⁿᵈᵉˣ ᶜᵒⁿᵗʳᵒˡˡᵉʳ", text);
    }

    [Fact]
    public void ThePercentSignIsLeftAloneRatherThanSwappedForAnUnverifiedGlyph()
    {
        string text = Build(Settings(small: true));

        Assert.Contains("87%", text);
        Assert.DoesNotContain("⁒", text);
    }

    [Fact]
    public void TheIconSurvivesSmallText()
    {
        Assert.Contains("🎮", Build(Settings(small: true)));
    }

    [Fact]
    public void TheLowBatteryTagStaysFullSizeBecauseItIsTheWarning()
    {
        var settings = Settings(small: true);

        string text = TrackerBatteryModule.BuildEntry(Device(level: 0.0625f), "{name} {batt}% {low}", settings, isLow: true);

        Assert.Contains("LOW", text);
        Assert.DoesNotContain("ˡᵒʷ", text);
    }

    [Fact]
    public void TheStatusWordIsALabelAndGetsRaised()
    {
        var settings = Settings(small: true);

        string text = TrackerBatteryModule.BuildEntry(Device(), "{name} {batt}% {status}", settings, isLow: false);

        Assert.Contains("ᵒⁿˡⁱⁿᵉ", text);
        Assert.DoesNotContain("Online", text);
    }

    [Fact]
    public void AChargingReadingKeepsItsPlusAndItsNumber()
    {
        var settings = Settings(small: true);

        string text = TrackerBatteryModule.BuildEntry(Device(charging: true), "{name} {batt}%", settings, isLow: false);

        Assert.Contains("+87%", text);
    }

    [Fact]
    public void SmallTextOffLeavesEveryPartAsTyped()
    {
        string text = Build(Settings(small: false));

        Assert.Equal("🎮 Index Controller 87%", text);
    }

    [Fact]
    public void RaisingTheNameDoesNotChangeWhatTheEntryCosts()
    {
        Assert.Equal(Build(Settings(small: false)).Length, Build(Settings(small: true)).Length);
    }
}
