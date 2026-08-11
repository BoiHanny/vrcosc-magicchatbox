using System;

namespace vrcosc_magicchatbox.Services.Vr;

public readonly record struct VrFrameCounters(uint FramePresents, uint DroppedFrames, uint ReprojectedFrames);

public readonly record struct VrFrameTimings(
    float AppGpuMs,
    float CompositorGpuMs,
    float CpuMs,
    float ClientFrameIntervalMs,
    uint ReprojectionFlags);

public sealed record VrPerformanceSnapshot
{
    public float TargetHz { get; init; }

    public float EffectiveFps { get; init; }

    public float ReprojectionPercent { get; init; }

    public float DroppedPerMinute { get; init; }

    public bool MotionSmoothingActive { get; init; }

    public float AppGpuMs { get; init; }

    public float CompositorGpuMs { get; init; }

    public float CpuMs { get; init; }

    public float HeadroomPercent { get; init; }
}

public static class VrPerformanceMath
{
    public const uint ReprojectionReasonCpu = 0x01;
    public const uint ReprojectionReasonGpu = 0x02;
    public const uint ReprojectionAsync = 0x04;
    public const uint ReprojectionMotion = 0x08;

    public static VrPerformanceSnapshot? Compute(
        VrFrameCounters previous,
        VrFrameCounters current,
        TimeSpan elapsed,
        float targetHz,
        VrFrameTimings timings)
    {
        if (elapsed <= TimeSpan.Zero)
            return null;

        if (current.FramePresents < previous.FramePresents ||
            current.DroppedFrames < previous.DroppedFrames ||
            current.ReprojectedFrames < previous.ReprojectedFrames)
        {
            return null;
        }

        uint presents = current.FramePresents - previous.FramePresents;
        uint dropped = current.DroppedFrames - previous.DroppedFrames;
        uint reprojected = current.ReprojectedFrames - previous.ReprojectedFrames;

        float reprojectionPercent = presents > 0
            ? Math.Clamp(reprojected / (float)presents * 100f, 0f, 100f)
            : 0f;

        float droppedPerMinute = (float)(dropped / elapsed.TotalMinutes);

        float effectiveFps = timings.ClientFrameIntervalMs > 0.01f
            ? 1000f / timings.ClientFrameIntervalMs
            : (float)(presents / elapsed.TotalSeconds);

        if (targetHz > 0)
            effectiveFps = Math.Min(effectiveFps, targetHz);

        float headroomPercent = 0f;
        if (targetHz > 0)
        {
            float frameBudgetMs = 1000f / targetHz;
            if (frameBudgetMs > 0)
                headroomPercent = timings.AppGpuMs / frameBudgetMs * 100f;
        }

        bool motionSmoothing = (timings.ReprojectionFlags & (ReprojectionMotion | ReprojectionAsync)) != 0;

        return new VrPerformanceSnapshot
        {
            TargetHz = targetHz,
            EffectiveFps = Math.Max(0f, effectiveFps),
            ReprojectionPercent = reprojectionPercent,
            DroppedPerMinute = Math.Max(0f, droppedPerMinute),
            MotionSmoothingActive = motionSmoothing,
            AppGpuMs = Math.Max(0f, timings.AppGpuMs),
            CompositorGpuMs = Math.Max(0f, timings.CompositorGpuMs),
            CpuMs = Math.Max(0f, timings.CpuMs),
            HeadroomPercent = Math.Max(0f, headroomPercent),
        };
    }

    public static float Smooth(float previous, float sample, float alpha = 0.3f)
    {
        if (previous <= 0f)
            return sample;
        return previous + alpha * (sample - previous);
    }
}
