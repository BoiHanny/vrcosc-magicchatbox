using vrcosc_magicchatbox.Classes.Modules.Status;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Status;

public class StatusSetSummaryTests
{
    [Fact]
    public void An_empty_set_says_so_rather_than_reporting_zero()
    {
        Assert.Equal("Nothing marked to cycle in here", StatusSetSummary.Describe(0, cycleEnabled: true, 30));
        Assert.Equal("Nothing marked to cycle in here", StatusSetSummary.Describe(0, cycleEnabled: false, 30));
    }

    [Fact]
    public void Cycling_off_is_reported_instead_of_an_interval_that_will_never_elapse()
    {
        Assert.Equal("4 messages, cycling is off", StatusSetSummary.Describe(4, cycleEnabled: false, 30));
    }

    [Fact]
    public void Seconds_read_as_seconds()
    {
        Assert.Equal("4 messages every 30s", StatusSetSummary.Describe(4, cycleEnabled: true, 30));
    }

    [Fact]
    public void A_whole_number_of_minutes_drops_the_seconds()
    {
        Assert.Equal("6 messages every 2m", StatusSetSummary.Describe(6, cycleEnabled: true, 120));
    }

    [Fact]
    public void A_ragged_interval_keeps_both_parts()
    {
        Assert.Equal("6 messages every 1m 30s", StatusSetSummary.Describe(6, cycleEnabled: true, 90));
    }

    [Theory]
    [InlineData(true, "1 message every 30s")]
    [InlineData(false, "1 message, cycling is off")]
    public void One_message_is_not_one_messages(bool cycleEnabled, string expected)
    {
        Assert.Equal(expected, StatusSetSummary.Describe(1, cycleEnabled, 30));
    }

    [Fact]
    public void A_zero_interval_does_not_claim_to_cycle_every_zero_seconds()
    {
        Assert.Equal("3 messages cycling", StatusSetSummary.Describe(3, cycleEnabled: true, 0));
    }
}
