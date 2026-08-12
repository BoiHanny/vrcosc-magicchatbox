using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Media;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Models;

public class MediaSessionPositionAnchorTests
{
    private static MediaSessionInfo Session() => new(new MediaLinkSettings(), new MediaLinkDisplayState());

    [Fact]
    public void PositionFromAnOldSampleIsExtrapolatedFromWhenItWasSampled()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        var sampledAt = DateTime.UtcNow.AddSeconds(-5);
        session.SetPositionFromSample(TimeSpan.FromSeconds(60), sampledAt);

        Assert.InRange(session.CurrentTime.TotalSeconds, 64.5, 65.5);
    }

    [Fact]
    public void AFreshSampleIsNotExtrapolatedForward()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        session.SetPositionFromSample(TimeSpan.FromSeconds(60), DateTime.UtcNow);

        Assert.InRange(session.CurrentTime.TotalSeconds, 59.5, 60.5);
    }

    [Fact]
    public void StoredPositionIsTheSampledValueNotTheExtrapolatedOne()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        session.SetPositionFromSample(TimeSpan.FromSeconds(60), DateTime.UtcNow.AddSeconds(-5));

        Assert.Equal(TimeSpan.FromSeconds(60), session.StoredCurrentTime);
    }

    [Fact]
    public void PlainSetterStillAnchorsToNow()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        session.CurrentTime = TimeSpan.FromSeconds(60);

        Assert.InRange(session.CurrentTime.TotalSeconds, 59.5, 60.5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnUnsetOrEpochSampleTimeFallsBackToNow(long ticks)
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        var bogus = ticks < 0 ? DateTime.MinValue : default;
        session.SetPositionFromSample(TimeSpan.FromSeconds(60), bogus);

        Assert.InRange(session.CurrentTime.TotalSeconds, 59.5, 60.5);
    }

    [Fact]
    public void ASampleTimeInTheFutureFallsBackToNow()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        session.SetPositionFromSample(TimeSpan.FromSeconds(60), DateTime.UtcNow.AddSeconds(30));

        Assert.InRange(session.CurrentTime.TotalSeconds, 59.5, 60.5);
    }

    [Fact]
    public void AnAbsurdlyOldSampleTimeFallsBackToNow()
    {
        var session = Session();
        session.FullTime = TimeSpan.FromMinutes(4);
        session.PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        session.SetPositionFromSample(TimeSpan.FromSeconds(60), DateTime.UtcNow.AddHours(-2));

        Assert.InRange(session.CurrentTime.TotalSeconds, 59.5, 60.5);
    }
}

public class TimelineAnchorTests
{
    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void APlausibleSampleTimeIsTrusted()
    {
        var sampled = Now.AddSeconds(-5);
        Assert.Equal(sampled, MediaTimelinePolicy.ResolveAnchor(sampled, Now));
    }

    [Fact]
    public void AZeroSampleTimeIsRejected()
    {
        Assert.Equal(Now, MediaTimelinePolicy.ResolveAnchor(default, Now));
    }

    [Fact]
    public void AFutureSampleTimeIsRejected()
    {
        Assert.Equal(Now, MediaTimelinePolicy.ResolveAnchor(Now.AddSeconds(5), Now));
    }

    [Fact]
    public void ASampleOlderThanTheTrustWindowIsRejected()
    {
        var tooOld = Now - MediaTimelinePolicy.MaxAnchorBacklog - TimeSpan.FromSeconds(1);
        Assert.Equal(Now, MediaTimelinePolicy.ResolveAnchor(tooOld, Now));
    }

    [Fact]
    public void ASampleExactlyAtTheTrustBoundaryIsAccepted()
    {
        var edge = Now - MediaTimelinePolicy.MaxAnchorBacklog;
        Assert.Equal(edge, MediaTimelinePolicy.ResolveAnchor(edge, Now));
    }

    [Fact]
    public void ASampleAMillisecondInTheFutureIsRejectedRatherThanRewindingTime()
    {
        Assert.Equal(Now, MediaTimelinePolicy.ResolveAnchor(Now.AddMilliseconds(1), Now));
    }
}
