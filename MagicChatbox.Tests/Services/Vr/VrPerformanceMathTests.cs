using System;
using vrcosc_magicchatbox.Services.Vr;
using Xunit;

namespace MagicChatbox.Tests.Services.Vr;

public class VrPerformanceMathTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private static VrFrameTimings Timings(
        float appGpuMs = 5f,
        float compositorGpuMs = 1f,
        float cpuMs = 2f,
        float frameIntervalMs = 11.1f,
        uint reprojectionFlags = 0)
        => new(appGpuMs, compositorGpuMs, cpuMs, frameIntervalMs, reprojectionFlags);

    [Fact]
    public void ReprojectionIsTheShareOfPresentedFrames()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(1000, 0, 0),
            new VrFrameCounters(1100, 0, 25),
            OneSecond,
            targetHz: 90,
            Timings());

        Assert.NotNull(result);
        Assert.Equal(25f, result!.ReprojectionPercent, 3);
    }

    [Fact]
    public void DroppedFramesAreScaledToPerMinute()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 100, 0),
            new VrFrameCounters(90, 103, 0),
            TimeSpan.FromSeconds(30),
            targetHz: 90,
            Timings());

        Assert.Equal(6f, result!.DroppedPerMinute, 3);
    }

    [Fact]
    public void CounterResetIsDiscardedRatherThanReportedAsASpike()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(5000, 40, 20),
            new VrFrameCounters(10, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings());

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveElapsedIsRejectedRatherThanDividingByZero(int seconds)
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(90, 0, 0),
            TimeSpan.FromSeconds(seconds),
            targetHz: 90,
            Timings());

        Assert.Null(result);
    }

    [Fact]
    public void HeadroomIsGpuTimeAgainstTheFrameBudget()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(90, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings(appGpuMs: 5.5555f));

        Assert.Equal(50f, result!.HeadroomPercent, 1);
    }

    [Fact]
    public void EffectiveFpsComesFromTheClientFrameInterval()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(45, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings(frameIntervalMs: 22.2f));

        Assert.Equal(45f, result!.EffectiveFps, 0);
    }

    [Fact]
    public void EffectiveFpsNeverExceedsTheHeadsetTarget()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(90, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings(frameIntervalMs: 0.5f));

        Assert.Equal(90f, result!.EffectiveFps, 3);
    }

    [Fact]
    public void EffectiveFpsFallsBackToCountingPresentsWhenNoIntervalIsReported()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(72, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings(frameIntervalMs: 0f));

        Assert.Equal(72f, result!.EffectiveFps, 0);
    }

    [Theory]
    [InlineData(VrPerformanceMath.ReprojectionMotion, true)]
    [InlineData(VrPerformanceMath.ReprojectionAsync, true)]
    [InlineData(VrPerformanceMath.ReprojectionReasonCpu, false)]
    [InlineData(VrPerformanceMath.ReprojectionReasonGpu, false)]
    [InlineData(0u, false)]
    public void MotionSmoothingReflectsOnlyTheReprojectionModeFlags(uint flags, bool expected)
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(90, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings(reprojectionFlags: flags));

        Assert.Equal(expected, result!.MotionSmoothingActive);
    }

    [Fact]
    public void ZeroPresentsInAWindowReportsNoReprojectionRatherThanDividingByZero()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(500, 0, 0),
            new VrFrameCounters(500, 0, 0),
            OneSecond,
            targetHz: 90,
            Timings());

        Assert.Equal(0f, result!.ReprojectionPercent);
    }

    [Fact]
    public void UnknownTargetHzLeavesHeadroomAtZeroInsteadOfDividingByZero()
    {
        var result = VrPerformanceMath.Compute(
            new VrFrameCounters(0, 0, 0),
            new VrFrameCounters(90, 0, 0),
            OneSecond,
            targetHz: 0,
            Timings(appGpuMs: 8f));

        Assert.Equal(0f, result!.HeadroomPercent);
    }

    [Fact]
    public void SmoothTakesTheFirstSampleWholeThenEases()
    {
        Assert.Equal(10f, VrPerformanceMath.Smooth(0f, 10f), 3);

        float smoothed = VrPerformanceMath.Smooth(10f, 20f, alpha: 0.5f);
        Assert.Equal(15f, smoothed, 3);
    }
}
