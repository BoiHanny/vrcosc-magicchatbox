using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

// Heart rate is the one parameter family other people's prefabs already bind, so the scaling has to
// stay bit-for-bit what it was before it became configurable. These tests pin the old formulas
// (hr/255f and hr/127.5f - 1f) against the new min/max path at its defaults.
public class HeartRateScaleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(72)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(180)]
    [InlineData(254)]
    [InlineData(255)]
    public void Default_bounds_reproduce_the_old_HRPercent_exactly(int heartRate)
    {
        float previous = heartRate / 255f;

        float now = HeartRateScale.Normalize(heartRate, HeartRateScale.DefaultMin, HeartRateScale.DefaultMax);

        Assert.Equal(previous, now);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(72)]
    [InlineData(127)]
    [InlineData(180)]
    [InlineData(255)]
    public void Default_bounds_reproduce_the_old_FullHRPercent_exactly(int heartRate)
    {
        float previous = (heartRate / 127.5f) - 1f;

        float now = HeartRateScale.ToFullRange(
            HeartRateScale.Normalize(heartRate, HeartRateScale.DefaultMin, HeartRateScale.DefaultMax));

        Assert.Equal(previous, now, 6);
    }

    [Fact]
    public void Custom_bounds_spread_a_human_heart_rate_across_the_whole_range()
    {
        // The point of the setting: 60-180 uses the full blendtree instead of the ~25% that 0-255 gives.
        Assert.Equal(0f, HeartRateScale.Normalize(60, 60, 180));
        Assert.Equal(0.5f, HeartRateScale.Normalize(120, 60, 180));
        Assert.Equal(1f, HeartRateScale.Normalize(180, 60, 180));
    }

    [Theory]
    [InlineData(40, 60, 180, 0f)]
    [InlineData(220, 60, 180, 1f)]
    public void Values_outside_the_bounds_clamp_rather_than_overshoot(int heartRate, int min, int max, float expected)
    {
        // An unclamped value above 1.0 is silently flattened by VRChat, which loses exactly the
        // "working hard" region somebody set custom bounds to see.
        Assert.Equal(expected, HeartRateScale.Normalize(heartRate, min, max));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(200, 100)]
    public void An_inverted_or_empty_range_yields_zero_rather_than_dividing_by_zero(int min, int max)
    {
        Assert.Equal(0f, HeartRateScale.Normalize(120, min, max));
    }

    [Fact]
    public void The_full_range_conversion_maps_the_midpoint_to_zero()
    {
        Assert.Equal(-1f, HeartRateScale.ToFullRange(0f));
        Assert.Equal(0f, HeartRateScale.ToFullRange(0.5f));
        Assert.Equal(1f, HeartRateScale.ToFullRange(1f));
    }
}
