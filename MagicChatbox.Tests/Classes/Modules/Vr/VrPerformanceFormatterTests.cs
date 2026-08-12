using System;
using vrcosc_magicchatbox.Classes.Modules.Vr;
using vrcosc_magicchatbox.Services.Vr;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Vr;

public class VrPerformanceFormatterTests
{
    private static VrPerformanceSnapshot Snapshot(
        float fps = 90,
        float targetHz = 90,
        float reprojection = 0,
        float dropped = 0,
        bool motionSmoothing = false,
        float appGpuMs = 5f,
        float compositorGpuMs = 1f,
        float cpuMs = 2f,
        float headroom = 45f)
        => new()
        {
            EffectiveFps = fps,
            TargetHz = targetHz,
            ReprojectionPercent = reprojection,
            DroppedPerMinute = dropped,
            MotionSmoothingActive = motionSmoothing,
            AppGpuMs = appGpuMs,
            CompositorGpuMs = compositorGpuMs,
            CpuMs = cpuMs,
            HeadroomPercent = headroom,
        };

    private static VrPerformanceSettings Settings() => new();

    [Fact]
    public void DefaultsShowFrameRateAndReprojectionOnly()
    {
        string text = VrPerformanceFormatter.Build(Snapshot(), Settings(), isDegraded: false);

        Assert.Contains("90", text);
        Assert.Contains("⁒", text);
        Assert.DoesNotContain("ᵐˢ", text);
    }

    [Fact]
    public void DefaultOutputStaysWellInsideTheOscBudget()
    {
        string text = VrPerformanceFormatter.Build(Snapshot(), Settings(), isDegraded: false);

        Assert.True(text.Length <= 25, $"default segment was {text.Length} chars: '{text}'");
    }

    [Fact]
    public void NothingEnabledProducesAnEmptySegment()
    {
        var settings = Settings();
        settings.ShowFps = false;
        settings.ShowReprojection = false;

        Assert.Equal(string.Empty, VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false));
    }

    [Fact]
    public void NullSnapshotProducesAnEmptySegment()
    {
        Assert.Equal(string.Empty, VrPerformanceFormatter.Build(null, Settings(), isDegraded: false));
    }

    [Fact]
    public void TargetHzTurnsFpsIntoAPair()
    {
        var settings = Settings();
        settings.ShowTargetHz = true;
        settings.UseEmojisForVrPerf = false;

        string text = VrPerformanceFormatter.Build(Snapshot(fps: 46), settings, isDegraded: true);

        Assert.Contains("46/90", text);
    }


    [Fact]
    public void UnitsAreSuperscriptedButNumbersAreNot()
    {
        var settings = Settings();
        settings.UseEmojisForVrPerf = false;

        string text = VrPerformanceFormatter.Build(Snapshot(fps: 90, reprojection: 2), settings, isDegraded: false);

        Assert.Contains("90", text);
        Assert.Contains("2", text);

        Assert.Contains("ᶠᵖˢ", text);
        Assert.Contains("ʳᵉᵖʳᵒʲ", text);
        Assert.DoesNotContain("fps", text);
        Assert.DoesNotContain("reproj", text);
    }

    [Fact]
    public void SuperscriptCanBeTurnedOffForPlainText()
    {
        var settings = Settings();
        settings.UseEmojisForVrPerf = false;
        settings.UseSuperscriptUnits = false;

        string text = VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false);

        Assert.Contains("fps", text);
        Assert.Contains("reproj", text);
        Assert.DoesNotContain("ᶠᵖˢ", text);
    }

    [Fact]
    public void DecimalValuesKeepTheirPointRatherThanBeingSuperscripted()
    {
        var settings = Settings();
        settings.UseEmojisForVrPerf = false;
        settings.ShowFps = false;
        settings.ShowReprojection = false;
        settings.ShowAppGpuMs = true;

        string text = VrPerformanceFormatter.Build(Snapshot(appGpuMs: 7.2f), settings, isDegraded: false);

        Assert.Contains("7.2", text);
        Assert.DoesNotContain("⁷", text);
        Assert.Contains("ᵐˢ", text);
    }

    [Fact]
    public void EmojiModeDropsTheWordButKeepsTheUnit()
    {
        var settings = Settings();
        settings.UseEmojisForVrPerf = true;

        string text = VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false);

        Assert.Contains("🎯", text);
        Assert.Contains("ᶠᵖˢ", text);
        Assert.DoesNotContain("ʳᵉᵖʳᵒʲ", text);
    }

    [Fact]
    public void DegradedSwapsTheFrameRateIconForAWarning()
    {
        var settings = Settings();

        string healthy = VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false);
        string degraded = VrPerformanceFormatter.Build(Snapshot(fps: 46), settings, isDegraded: true);

        Assert.Contains("🎯", healthy);
        Assert.Contains("⚠️", degraded);
    }

    [Fact]
    public void SampleSnapshotsRenderAndStayAffordable()
    {
        var settings = Settings();

        string healthy = VrPerformanceFormatter.Build(
            VrPerformanceFormatter.SampleSnapshot(degraded: false), settings, isDegraded: false);
        string degraded = VrPerformanceFormatter.Build(
            VrPerformanceFormatter.SampleSnapshot(degraded: true), settings, isDegraded: true);

        Assert.NotEqual(string.Empty, healthy);
        Assert.NotEqual(string.Empty, degraded);

        Assert.True(healthy.Length <= 30, $"healthy preview was {healthy.Length}: '{healthy}'");
        Assert.True(degraded.Length <= 40, $"degraded preview was {degraded.Length}: '{degraded}'");
    }

    [Fact]
    public void EveryMetricOnStillFitsTheChatboxBudget()
    {
        var settings = Settings();
        settings.ShowTargetHz = true;
        settings.ShowDroppedFrames = true;
        settings.ShowMotionSmoothing = true;
        settings.ShowAppGpuMs = true;
        settings.ShowCompositorGpuMs = true;
        settings.ShowHeadroom = true;
        settings.ShowCpuTiming = true;

        string text = VrPerformanceFormatter.Build(
            VrPerformanceFormatter.SampleSnapshot(degraded: true), settings, isDegraded: true);

        Assert.True(text.Length < 144, $"all-metrics segment was {text.Length}: '{text}'");
    }

    [Fact]
    public void EveryMetricCanBeTurnedOn()
    {
        var settings = Settings();
        settings.ShowTargetHz = true;
        settings.ShowDroppedFrames = true;
        settings.ShowMotionSmoothing = true;
        settings.ShowAppGpuMs = true;
        settings.ShowCompositorGpuMs = true;
        settings.ShowHeadroom = true;
        settings.ShowCpuTiming = true;

        string text = VrPerformanceFormatter.Build(
            Snapshot(motionSmoothing: true), settings, isDegraded: false);

        Assert.Contains("90/90", text);
        Assert.Contains("🌀", text);
        Assert.Contains("ᵐˢ", text);
    }

    [Fact]
    public void TextLabelsReplaceEmojiWhenEmojiAreOff()
    {
        var settings = Settings();
        settings.UseEmojisForVrPerf = false;
        settings.ShowDroppedFrames = true;

        string text = VrPerformanceFormatter.Build(Snapshot(dropped: 12), settings, isDegraded: false);

        Assert.Contains("ʳᵉᵖʳᵒʲ", text);
        Assert.Contains("ᵈʳᵒᵖˢ", text);
        Assert.DoesNotContain("🎯", text);
    }

    [Fact]
    public void DroppedFramesCarryTheirPerMinuteWindow()
    {
        var emojiSettings = Settings();
        emojiSettings.ShowDroppedFrames = true;

        var textSettings = Settings();
        textSettings.ShowDroppedFrames = true;
        textSettings.UseEmojisForVrPerf = false;

        Assert.Contains("14/ᵐⁱⁿ", VrPerformanceFormatter.Build(Snapshot(dropped: 14), emojiSettings, false));
        Assert.Contains("14/ᵐⁱⁿ", VrPerformanceFormatter.Build(Snapshot(dropped: 14), textSettings, false));
    }

    [Fact]
    public void SeparatorIsHonoured()
    {
        var settings = Settings();
        settings.StatsSeparator = " | ";

        string text = VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false);

        Assert.Contains(" | ", text);
    }


    [Fact]
    public void OnlyWhenDegradedCostsNothingWhileHealthy()
    {
        var settings = Settings();
        settings.DisplayMode = VrPerformanceDisplayMode.OnlyWhenDegraded;

        Assert.Equal(string.Empty, VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false));
    }

    [Fact]
    public void OnlyWhenDegradedAppearsOnceDegraded()
    {
        var settings = Settings();
        settings.DisplayMode = VrPerformanceDisplayMode.OnlyWhenDegraded;

        string text = VrPerformanceFormatter.Build(Snapshot(fps: 46, reprojection: 31), settings, isDegraded: true);

        Assert.NotEqual(string.Empty, text);
        Assert.Contains("46", text);
    }

    [Fact]
    public void CompactThenExpandShowsOnlyFrameRateWhileHealthy()
    {
        var settings = Settings();
        settings.DisplayMode = VrPerformanceDisplayMode.CompactThenExpand;
        settings.ShowDroppedFrames = true;

        string healthy = VrPerformanceFormatter.Build(Snapshot(), settings, isDegraded: false);
        string degraded = VrPerformanceFormatter.Build(Snapshot(reprojection: 31, dropped: 14), settings, isDegraded: true);

        Assert.DoesNotContain("⁒", healthy);
        Assert.Contains("⁒", degraded);
        Assert.True(degraded.Length > healthy.Length);
    }


    [Fact]
    public void ReprojectionAboveThresholdIsDegraded()
    {
        Assert.True(VrPerformanceDegradedTracker.TripsThreshold(Snapshot(reprojection: 10), Settings()));
        Assert.False(VrPerformanceDegradedTracker.TripsThreshold(Snapshot(reprojection: 9), Settings()));
    }

    [Fact]
    public void FrameRateBelowThePercentageOfTargetIsDegraded()
    {
        Assert.True(VrPerformanceDegradedTracker.TripsThreshold(Snapshot(fps: 80), Settings()));
        Assert.False(VrPerformanceDegradedTracker.TripsThreshold(Snapshot(fps: 89), Settings()));
    }

    [Fact]
    public void UnknownTargetHzDoesNotCountAsDegradedFrameRate()
    {
        Assert.False(VrPerformanceDegradedTracker.TripsThreshold(Snapshot(fps: 1, targetHz: 0), Settings()));
    }

    [Fact]
    public void DegradedEntryIsImmediateButExitWaitsOutHysteresis()
    {
        var tracker = new VrPerformanceDegradedTracker();
        var settings = Settings();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(tracker.Update(Snapshot(reprojection: 40), settings, start));

        Assert.True(tracker.Update(Snapshot(), settings, start.AddSeconds(1)));
        Assert.True(tracker.Update(Snapshot(), settings, start.AddSeconds(4)));

        Assert.True(tracker.Update(Snapshot(), settings, start.AddSeconds(5)));

        Assert.False(tracker.Update(Snapshot(), settings, start.AddSeconds(6)));
    }

    [Fact]
    public void AValueRestingOnTheThresholdDoesNotFlickerEveryTick()
    {
        var tracker = new VrPerformanceDegradedTracker();
        var settings = Settings();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        tracker.Update(Snapshot(reprojection: 10), settings, now);

        for (int i = 1; i <= 6; i++)
        {
            float reprojection = i % 2 == 0 ? 10.5f : 9.5f;
            bool degraded = tracker.Update(Snapshot(reprojection: reprojection), settings, now.AddSeconds(i));
            Assert.True(degraded);
        }
    }

    [Fact]
    public void ResetClearsDegradedStateForANewSession()
    {
        var tracker = new VrPerformanceDegradedTracker();
        var now = DateTime.UtcNow;

        tracker.Update(Snapshot(reprojection: 40), Settings(), now);
        Assert.True(tracker.IsDegraded);

        tracker.Reset();
        Assert.False(tracker.IsDegraded);
    }
}
