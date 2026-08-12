using System;
using vrcosc_magicchatbox.Core.Osc;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

public class TransientWindowTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    private static bool ShouldShow(double secondsAgo, double duration = 25)
        => TransientWindow.ShouldShow(true, Now.AddSeconds(-secondsAgo), Now, duration);

    [Fact]
    public void WithTransientOffEverythingIsAlwaysShown()
    {
        Assert.True(TransientWindow.ShouldShow(false, default, Now, 0));
        Assert.True(TransientWindow.ShouldShow(false, Now.AddHours(-5), Now, 25));
    }

    [Fact]
    public void AFreshChangeIsShown()
    {
        Assert.True(ShouldShow(secondsAgo: 0));
        Assert.True(ShouldShow(secondsAgo: 10));
    }

    [Fact]
    public void TheWindowIsInclusiveAtItsBoundary()
    {
        Assert.True(ShouldShow(secondsAgo: 25, duration: 25));
        Assert.False(ShouldShow(secondsAgo: 25.001, duration: 25));
    }

    [Fact]
    public void AnOldChangeIsHidden()
    {
        Assert.False(ShouldShow(secondsAgo: 60));
    }

    [Fact]
    public void WithNothingEverPlayedNothingIsShown()
    {
        Assert.False(TransientWindow.ShouldShow(true, default, Now, 25));
    }

    [Fact]
    public void AZeroDurationHidesRatherThanShowingForever()
    {
        Assert.False(ShouldShow(secondsAgo: 0, duration: 0));
    }

    [Fact]
    public void AChangeStampedInTheFutureIsTreatedAsJustHappened()
    {
        Assert.True(TransientWindow.ShouldShow(true, Now.AddSeconds(30), Now, 25));
    }
}
