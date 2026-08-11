using System;
using System.Collections.Generic;
using System.Globalization;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Services.Vr;

namespace vrcosc_magicchatbox.Classes.Modules.Vr;

public sealed class VrPerformanceDegradedTracker
{
    private DateTime? _healthySinceUtc;

    public bool IsDegraded { get; private set; }

    public static bool TripsThreshold(VrPerformanceSnapshot snapshot, VrPerformanceSettings settings)
    {
        if (snapshot.ReprojectionPercent >= settings.DegradedReprojectionPercent)
            return true;

        if (snapshot.DroppedPerMinute >= settings.DegradedDroppedPerMinute)
            return true;

        if (snapshot.TargetHz > 0)
        {
            double floor = snapshot.TargetHz * (settings.DegradedFpsPercentOfTarget / 100.0);
            if (snapshot.EffectiveFps < floor)
                return true;
        }

        return false;
    }

    public bool Update(VrPerformanceSnapshot snapshot, VrPerformanceSettings settings, DateTime nowUtc)
    {
        if (TripsThreshold(snapshot, settings))
        {
            _healthySinceUtc = null;
            IsDegraded = true;
            return IsDegraded;
        }

        if (!IsDegraded)
            return false;

        _healthySinceUtc ??= nowUtc;
        if (nowUtc - _healthySinceUtc.Value >= TimeSpan.FromSeconds(Math.Max(0, settings.DegradedHysteresisSeconds)))
        {
            IsDegraded = false;
            _healthySinceUtc = null;
        }

        return IsDegraded;
    }

    public void Reset()
    {
        IsDegraded = false;
        _healthySinceUtc = null;
    }
}

public static class VrPerformanceFormatter
{
    public static string Build(
        VrPerformanceSnapshot? snapshot,
        VrPerformanceSettings settings,
        bool isDegraded)
    {
        if (snapshot == null || settings == null)
            return string.Empty;

        bool compactOnly = false;
        switch (settings.DisplayMode)
        {
            case VrPerformanceDisplayMode.OnlyWhenDegraded when !isDegraded:
                return string.Empty;
            case VrPerformanceDisplayMode.CompactThenExpand when !isDegraded:
                compactOnly = true;
                break;
        }

        var parts = new List<string>();
        bool emoji = settings.UseEmojisForVrPerf;

        if (settings.ShowFps)
        {
            string fps = Number(snapshot.EffectiveFps, settings, decimals: 0);

            string text = settings.ShowTargetHz && snapshot.TargetHz > 0
                ? $"{fps}/{Number(snapshot.TargetHz, settings, decimals: 0)}{Unit("hz", settings)}"
                : $"{fps}{Unit("fps", settings)}";

            parts.Add(Icon(emoji, isDegraded ? "⚠️" : "🎯", text));
        }
        else if (settings.ShowTargetHz && snapshot.TargetHz > 0)
        {
            parts.Add(Icon(emoji, "🎯", $"{Number(snapshot.TargetHz, settings, decimals: 0)}{Unit("hz", settings)}"));
        }

        if (compactOnly)
            return string.Join(settings.StatsSeparator ?? " ", parts);

        if (settings.ShowReprojection)
            parts.Add(Icon(emoji, "🔁", Value(snapshot.ReprojectionPercent, 0, "%", "% reproj", emoji, settings)));

        if (settings.ShowDroppedFrames)
        {
            string drops = Number(snapshot.DroppedPerMinute, settings, decimals: 0);
            string rate = $"{drops}/{Unit("min", settings)}";
            parts.Add(Icon(emoji, "📉", emoji ? rate : $"{rate} {Unit("drops", settings)}"));
        }

        if (settings.ShowMotionSmoothing && snapshot.MotionSmoothingActive)
            parts.Add(emoji ? "🌀" : Unit("motion", settings));

        if (settings.ShowAppGpuMs)
            parts.Add(Icon(emoji, "⏱", Value(snapshot.AppGpuMs, 1, "ms", "ms gpu", emoji, settings)));

        if (settings.ShowCompositorGpuMs)
            parts.Add(Icon(emoji, "🧩", Value(snapshot.CompositorGpuMs, 1, "ms", "ms comp", emoji, settings)));

        if (settings.ShowHeadroom)
            parts.Add(Icon(emoji, "📊", Value(snapshot.HeadroomPercent, 0, "%", "% budget", emoji, settings)));

        if (settings.ShowCpuTiming)
            parts.Add(Icon(emoji, "🧮", Value(snapshot.CpuMs, 1, "ms", "ms cpu", emoji, settings)));

        return string.Join(settings.StatsSeparator ?? " ", parts);
    }

    public static VrPerformanceSnapshot SampleSnapshot(bool degraded) => degraded
        ? new VrPerformanceSnapshot
        {
            TargetHz = 90,
            EffectiveFps = 46,
            ReprojectionPercent = 31,
            DroppedPerMinute = 14,
            MotionSmoothingActive = true,
            AppGpuMs = 10.8f,
            CompositorGpuMs = 1.4f,
            CpuMs = 4.1f,
            HeadroomPercent = 97,
        }
        : new VrPerformanceSnapshot
        {
            TargetHz = 90,
            EffectiveFps = 90,
            ReprojectionPercent = 2,
            DroppedPerMinute = 0,
            MotionSmoothingActive = false,
            AppGpuMs = 7.2f,
            CompositorGpuMs = 0.9f,
            CpuMs = 3.1f,
            HeadroomPercent = 65,
        };

    private static string Value(
        float value,
        int decimals,
        string emojiUnit,
        string textUnit,
        bool emoji,
        VrPerformanceSettings settings)
        => $"{Number(value, settings, decimals)}{Unit(emoji ? emojiUnit : textUnit, settings)}";

    private static string Icon(bool emoji, string icon, string text)
        => emoji ? $"{icon} {text}" : text;

    private static string Unit(string text, VrPerformanceSettings settings)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return settings.UseSuperscriptUnits
            ? TextUtilities.TransformToSuperscript(text)
            : text;
    }

    private static string Number(float value, VrPerformanceSettings settings, int decimals)
    {
        if (settings.RemoveNumberTrailing || decimals == 0)
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        return value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }
}
