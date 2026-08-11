using System;
using System.Runtime.InteropServices;
using Valve.VR;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services.Vr;

public sealed class VrPerformanceSampler
{
    private readonly IOpenVrSessionService _session;
    private readonly Func<DateTime> _utcNow;

    private VrFrameCounters? _previousCounters;
    private DateTime _previousSampleUtc;
    private float _targetHz;
    private float _smoothedAppGpuMs;
    private float _smoothedCompositorGpuMs;
    private float _smoothedCpuMs;
    private bool _loggedReadFailure;

    public VrPerformanceSampler(IOpenVrSessionService session, Func<DateTime>? utcNow = null)
    {
        _session = session;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public VrPerformanceSnapshot? Sample()
    {
        var compositor = _session.Compositor;
        if (compositor == null)
        {
            Reset();
            return null;
        }

        try
        {
            var stats = default(Compositor_CumulativeStats);
            compositor.GetCumulativeStats(ref stats, (uint)Marshal.SizeOf<Compositor_CumulativeStats>());

            var counters = new VrFrameCounters(
                stats.m_nNumFramePresents,
                stats.m_nNumDroppedFrames,
                stats.m_nNumReprojectedFrames);

            var timing = new Compositor_FrameTiming
            {
                m_nSize = (uint)Marshal.SizeOf<Compositor_FrameTiming>(),
            };

            var timings = default(VrFrameTimings);
            if (compositor.GetFrameTiming(ref timing, 0))
            {
                _smoothedAppGpuMs = VrPerformanceMath.Smooth(_smoothedAppGpuMs, timing.m_flTotalRenderGpuMs);
                _smoothedCompositorGpuMs = VrPerformanceMath.Smooth(_smoothedCompositorGpuMs, timing.m_flCompositorRenderGpuMs);
                _smoothedCpuMs = VrPerformanceMath.Smooth(
                    _smoothedCpuMs,
                    timing.m_flCompositorRenderCpuMs + timing.m_flPresentCallCpuMs);

                timings = new VrFrameTimings(
                    _smoothedAppGpuMs,
                    _smoothedCompositorGpuMs,
                    _smoothedCpuMs,
                    timing.m_flClientFrameIntervalMs,
                    timing.m_nReprojectionFlags);
            }

            _loggedReadFailure = false;

            DateTime now = _utcNow();
            var previous = _previousCounters;
            DateTime previousAt = _previousSampleUtc;

            _previousCounters = counters;
            _previousSampleUtc = now;

            if (previous == null)
                return null;

            return VrPerformanceMath.Compute(
                previous.Value,
                counters,
                now - previousAt,
                ResolveTargetHz(),
                timings);
        }
        catch (Exception ex)
        {
            if (!_loggedReadFailure)
            {
                _loggedReadFailure = true;
                Logging.WriteInfo($"VR performance read failed: {ex.Message}");
            }

            Reset();
            return null;
        }
    }

    private float ResolveTargetHz()
    {
        if (_targetHz > 0)
            return _targetHz;

        var system = _session.System;
        if (system == null)
            return 0f;

        var error = ETrackedPropertyError.TrackedProp_Success;
        float hz = system.GetFloatTrackedDeviceProperty(
            Valve.VR.OpenVR.k_unTrackedDeviceIndex_Hmd,
            ETrackedDeviceProperty.Prop_DisplayFrequency_Float,
            ref error);

        if (error == ETrackedPropertyError.TrackedProp_Success && hz > 0)
            _targetHz = hz;

        return _targetHz;
    }

    public void Reset()
    {
        _previousCounters = null;
        _previousSampleUtc = default;
        _targetHz = 0f;
        _smoothedAppGpuMs = 0f;
        _smoothedCompositorGpuMs = 0f;
        _smoothedCpuMs = 0f;
    }
}
