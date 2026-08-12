using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

public class OscPreviewFillTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(121)]
    public void Plenty_of_room_reads_as_roomy(int length)
    {
        Assert.Equal(OscPreviewFill.Roomy, OscPreviewFillLevel.Classify(length, 144));
    }

    [Theory]
    [InlineData(123)]
    [InlineData(140)]
    [InlineData(143)]
    public void Running_out_of_room_reads_as_tight(int length)
    {
        Assert.Equal(OscPreviewFill.Tight, OscPreviewFillLevel.Classify(length, 144));
    }

    [Theory]
    [InlineData(144)]
    [InlineData(200)]
    public void At_or_past_the_limit_reads_as_full(int length)
    {
        // Past the limit matters: the build reports the real length even though it sends nothing.
        Assert.Equal(OscPreviewFill.Full, OscPreviewFillLevel.Classify(length, 144));
    }

    [Fact]
    public void The_boundary_lands_on_tight_not_roomy()
    {
        int boundary = (int)(144 * OscPreviewFillLevel.TightFraction) + 1;

        Assert.Equal(OscPreviewFill.Roomy, OscPreviewFillLevel.Classify(boundary - 2, 144));
        Assert.Equal(OscPreviewFill.Tight, OscPreviewFillLevel.Classify(boundary, 144));
    }

    [Fact]
    public void A_nonsense_limit_never_cries_wolf()
    {
        // Guards a divide-by-nothing turning an empty preview permanently red.
        Assert.Equal(OscPreviewFill.Roomy, OscPreviewFillLevel.Classify(0, 0));
        Assert.Equal(OscPreviewFill.Roomy, OscPreviewFillLevel.Classify(80, -1));
    }

    [Fact]
    public void The_default_limit_is_the_chatbox_limit()
    {
        Assert.Equal(OscPreviewFillLevel.Classify(140, OscBuildContext.MaxOscLength),
                     OscPreviewFillLevel.Classify(140));
    }
}
