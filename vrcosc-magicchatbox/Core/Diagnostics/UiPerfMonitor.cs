using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.Core.Diagnostics;

/// <summary>
/// Watches the UI thread while the app runs: how late queued work is dispatched, how often frames are
/// composed, and how often layout is invalidated. Only starts when <see cref="PerfProbe.IsEnabled"/>.
/// </summary>
public static class UiPerfMonitor
{
    private const int ProbeIntervalMs = 100;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static DispatcherTimer? _stallProbe;
    private static Window? _window;

    private static long _lastProbeTicks;
    private static long _lastRenderTicks;
    private static long _layoutUpdates;
    private static long _frames;
    private static bool _started;
    private static bool _frameSampling;

    public static void Start(Window window)
    {
        if (!PerfProbe.IsEnabled || _started)
            return;

        _started = true;
        _window = window;

        LogEnvironment();

        _lastProbeTicks = Clock.ElapsedTicks;
        _lastRenderTicks = Clock.ElapsedTicks;

        // Background priority: the gap between the interval and the actual tick is time the UI thread
        // spent on higher-priority work, which is exactly the stall a user feels.
        _stallProbe = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(ProbeIntervalMs),
        };
        _stallProbe.Tick += OnStallProbeTick;
        _stallProbe.Start();

        window.LayoutUpdated += OnLayoutUpdated;
        window.Closed += OnWindowClosed;
    }

    /// <summary>
    /// Samples frame intervals for <paramref name="duration"/>. Subscribing to CompositionTarget.Rendering
    /// makes WPF composite every frame whether or not anything changed, so this cannot be left on: the
    /// numbers it produces are the cost of measuring, not the cost of running.
    /// </summary>
    public static void SampleFrames(TimeSpan duration)
    {
        if (!PerfProbe.IsEnabled || _frameSampling)
            return;

        _frameSampling = true;
        _lastRenderTicks = Clock.ElapsedTicks;
        CompositionTarget.Rendering += OnRendering;

        var stop = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher)
        {
            Interval = duration,
        };

        stop.Tick += (_, _) =>
        {
            stop.Stop();
            CompositionTarget.Rendering -= OnRendering;
            _frameSampling = false;
        };

        stop.Start();
    }

    public static void Stop()
    {
        if (!_started)
            return;

        _started = false;

        if (_stallProbe != null)
        {
            _stallProbe.Stop();
            _stallProbe.Tick -= OnStallProbeTick;
            _stallProbe = null;
        }

        if (_frameSampling)
        {
            CompositionTarget.Rendering -= OnRendering;
            _frameSampling = false;
        }

        if (_window != null)
        {
            _window.LayoutUpdated -= OnLayoutUpdated;
            _window.Closed -= OnWindowClosed;
            _window = null;
        }
    }

    public static long LayoutUpdateCount => _layoutUpdates;

    public static long FrameCount => _frames;

    private static void OnWindowClosed(object? sender, EventArgs e) => Stop();

    private static void OnStallProbeTick(object? sender, EventArgs e)
    {
        long now = Clock.ElapsedTicks;
        double actualMs = TicksToMs(now - _lastProbeTicks);
        _lastProbeTicks = now;

        double lateBy = actualMs - ProbeIntervalMs;
        if (lateBy > 0)
            PerfProbe.Record("ui.dispatcher.stall", lateBy);
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        long now = Clock.ElapsedTicks;
        double frameMs = TicksToMs(now - _lastRenderTicks);
        _lastRenderTicks = now;

        _frames++;

        // The first tick after an idle gap is not a dropped frame, it is the compositor waking up.
        if (frameMs < 250)
            PerfProbe.Record("ui.frame.interval", frameMs);
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e) => _layoutUpdates++;

    private static void LogEnvironment()
    {
        var sb = new StringBuilder();

        int tier = RenderCapability.Tier >> 16;
        sb.Append("[Perf] Render tier ").Append(tier);
        sb.Append(", pixel shader 3.0 supported: ").Append(RenderCapability.IsPixelShaderVersionSupported(3, 0));
        sb.Append(", max texture ").Append(RenderCapability.MaxHardwareTextureSize);
        sb.Append(", DPI ").Append(GetDpiScale().ToString("F2", CultureInfo.InvariantCulture));
        sb.Append(", processors ").Append(Environment.ProcessorCount);
        sb.Append(", server GC ").Append(System.Runtime.GCSettings.IsServerGC);

        Classes.DataAndSecurity.Logging.WriteInfo(sb.ToString());
        PerfProbe.Mark(sb.ToString());
    }

    private static double GetDpiScale()
    {
        try
        {
            if (_window != null && PresentationSource.FromVisual(_window) is HwndSource source)
                return source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        }
        catch
        {
            // Diagnostics must never take the app down.
        }

        return 1.0;
    }

    private static double TicksToMs(long ticks)
        => ticks * 1000.0 / Stopwatch.Frequency;
}
