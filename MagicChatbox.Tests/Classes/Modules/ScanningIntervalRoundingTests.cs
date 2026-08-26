using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class ScanningIntervalRoundingTests
{
    [Theory]
    [InlineData(9.799999999999999, 9.8)]
    [InlineData(1.9000000000000001, 1.9)]
    [InlineData(0.7999999999999999, 0.8)]
    [InlineData(2.9999999999999996, 3.0)]
    public void A_tick_that_drifted_off_a_tenth_is_stored_as_the_tenth(double drifted, double expected)
    {
        // The slider snaps to minimum plus whole steps of a tenth, which binary floating point
        // cannot represent, so what arrives here is the drifted value the user never chose.
        var settings = new AppSettings { ScanningInterval = drifted };

        Assert.Equal(expected, settings.ScanningInterval);
    }

    [Fact]
    public void Rounding_settles_instead_of_bouncing_between_two_values()
    {
        // The slider pushes its drifted value back every time the rounded one is handed to it, so
        // the second pass has to agree with the first or the two sit there trading values forever.
        var settings = new AppSettings { ScanningInterval = 9.799999999999999 };
        double first = settings.ScanningInterval;

        settings.ScanningInterval = 9.799999999999999;

        Assert.Equal(first, settings.ScanningInterval);
        Assert.Equal(9.8, settings.ScanningInterval);
    }

    [Fact]
    public void The_bounds_still_hold()
    {
        Assert.Equal(
            AppSettings.OscTickIntervalMinSeconds,
            new AppSettings { ScanningInterval = 0.01 }.ScanningInterval);

        Assert.Equal(
            AppSettings.OscTickIntervalMaxSeconds,
            new AppSettings { ScanningInterval = 99 }.ScanningInterval);

        Assert.Equal(
            AppSettings.OscTickIntervalDefaultSeconds,
            new AppSettings { ScanningInterval = double.NaN }.ScanningInterval);
    }

    [Fact]
    public void A_value_already_on_a_tenth_is_left_alone()
    {
        var settings = new AppSettings { ScanningInterval = 2.5 };

        Assert.Equal(2.5, settings.ScanningInterval);
    }
}
