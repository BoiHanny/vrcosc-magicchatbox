using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.UI.Controls;

/// <summary>
/// Builds a long page's sections only as they approach the viewport, and remembers how tall each one
/// turned out so the placeholder it replaces can reserve the right space next time. Reserving the space
/// is what makes the scrollbar honest and lets a saved scroll offset be restored without building
/// everything above it first.
/// </summary>
public sealed class SectionRealizer
{
    /// <summary>Assumed height for a section whose real height has never been measured.</summary>
    public const double DefaultSectionHeight = 320;

    private readonly List<Slot> _slots = new();
    private readonly ScrollViewer _scroll;
    private readonly IDictionary<string, double> _heights;

    private bool _attached;
    private bool _passQueued;

    public SectionRealizer(ScrollViewer scroll, IDictionary<string, double> rememberedHeights)
    {
        _scroll = scroll ?? throw new ArgumentNullException(nameof(scroll));
        _heights = rememberedHeights ?? new Dictionary<string, double>();
    }

    /// <summary>How much beyond the viewport, in viewport heights, is built ahead of the user.</summary>
    public double LookaheadViewports { get; set; } = 0.5;

    /// <summary>
    /// Sections built per pass. Realizing invalidates layout, which raises SizeChanged, which asks to
    /// realize again; without a cap one navigation turns into a storm of layout passes.
    /// </summary>
    public int MaxPerPass { get; set; } = 2;

    public int RealizedCount => _slots.Count(s => s.IsRealized);

    public int TotalCount => _slots.Count;

    public void Add(string key, ContentControl wrapper, Func<FrameworkElement> factory, string dataContextPath)
    {
        if (wrapper == null || factory == null)
            return;

        var slot = new Slot(key, wrapper, factory, dataContextPath);
        _slots.Add(slot);

        if (_heights.TryGetValue(key, out double height) && height > 0)
            wrapper.MinHeight = height;
        else
            wrapper.MinHeight = DefaultSectionHeight;
    }

    public void Start()
    {
        if (_attached)
            return;

        _attached = true;
        _scroll.ScrollChanged += OnScrollChanged;
        _scroll.SizeChanged += OnScrollSizeChanged;
        RealizeVisible();
    }

    public void Stop()
    {
        if (!_attached)
            return;

        _attached = false;
        _scroll.ScrollChanged -= OnScrollChanged;
        _scroll.SizeChanged -= OnScrollSizeChanged;
    }

    /// <summary>Builds every remaining section. Needed before any operation that must see the whole page.</summary>
    public void RealizeAll()
    {
        foreach (Slot slot in _slots)
            Realize(slot, animate: false);
    }

    /// <summary>Queues a realization pass. Safe to call from layout callbacks; passes coalesce.</summary>
    public void RealizeVisible()
    {
        if (_passQueued || _slots.Count == 0)
            return;

        _passQueued = true;
        _scroll.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RealizePass));
    }

    private void RealizePass()
    {
        _passQueued = false;

        if (!_attached)
            return;

        double viewportHeight = _scroll.ViewportHeight > 0 ? _scroll.ViewportHeight : _scroll.ActualHeight;
        if (viewportHeight <= 0)
            return;

        double lookahead = viewportHeight * LookaheadViewports;
        int built = 0;

        foreach (Slot slot in _slots)
        {
            if (slot.IsRealized || !TryGetViewportBounds(slot, out double top, out double bottom))
                continue;

            if (bottom < -lookahead || top > viewportHeight + lookahead)
                continue;

            Realize(slot, animate: true);

            if (++built >= MaxPerPass)
            {
                // More may still be in range; let layout settle first and pick them up next pass.
                RealizeVisible();
                return;
            }
        }
    }

    /// <summary>Position of a wrapper relative to the top of the viewport; negative means scrolled past.</summary>
    private bool TryGetViewportBounds(Slot slot, out double top, out double bottom)
    {
        top = 0;
        bottom = 0;

        if (!slot.Wrapper.IsDescendantOf(_scroll))
            return false;

        try
        {
            top = slot.Wrapper.TransformToAncestor(_scroll).Transform(default).Y;
            bottom = top + Math.Max(slot.Wrapper.ActualHeight, slot.Wrapper.MinHeight);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The wrapper is not connected to the scroll viewer yet; a later scroll or resize retries.
            return false;
        }
    }

    private void Realize(Slot slot, bool animate)
    {
        if (slot.IsRealized)
            return;

        slot.IsRealized = true;

        long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        long startAllocated = GC.GetAllocatedBytesForCurrentThread();

        FrameworkElement content = slot.Factory();
        content.SetBinding(FrameworkElement.DataContextProperty, new Binding(slot.DataContextPath));

        Core.Diagnostics.PerfProbe.Record(
            $"options.section.{slot.Key}",
            System.Diagnostics.Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - startAllocated);

        // The reservation exists only to hold space for the placeholder; real content sizes itself.
        slot.Wrapper.MinHeight = 0;
        slot.Wrapper.Content = content;
        slot.Content = content;

        slot.HeightMeasured += RememberHeight;
        content.SizeChanged += slot.OnContentSizeChanged;

        ReducedVisuals.Refresh(content);

        if (animate && !ReducedVisuals.IsEnabled)
            PlayEntrance(content);
    }

    private void RememberHeight(string key, double height)
    {
        if (height > 0)
            _heights[key] = height;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => RealizeVisible();

    private void OnScrollSizeChanged(object sender, SizeChangedEventArgs e) => RealizeVisible();

    private static void PlayEntrance(FrameworkElement content)
    {
        // Slide only. Animating Opacity on a section subtree makes WPF allocate an intermediate render
        // surface for it, which is the most expensive part of showing a section.
        var slide = new TranslateTransform();
        content.RenderTransform = slide;

        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14.0, 0.0, new Duration(TimeSpan.FromMilliseconds(160)))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private sealed class Slot
    {
        public Slot(string key, ContentControl wrapper, Func<FrameworkElement> factory, string dataContextPath)
        {
            Key = key;
            Wrapper = wrapper;
            Factory = factory;
            DataContextPath = dataContextPath;
        }

        public string Key { get; }

        public ContentControl Wrapper { get; }

        public Func<FrameworkElement> Factory { get; }

        public string DataContextPath { get; }

        public bool IsRealized { get; set; }

        public FrameworkElement? Content { get; set; }

        public event Action<string, double>? HeightMeasured;

        public void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height > 0)
                HeightMeasured?.Invoke(Key, e.NewSize.Height);
        }
    }
}
