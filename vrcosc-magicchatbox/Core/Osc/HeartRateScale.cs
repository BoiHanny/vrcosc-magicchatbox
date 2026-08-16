using System;

namespace vrcosc_magicchatbox.Core.Osc;

public static class HeartRateScale
{
    public const int DefaultMin = 0;
    public const int DefaultMax = 255;

    public static float Normalize(int heartRate, int min, int max)
    {
        if (max <= min)
            return 0f;

        return Math.Clamp((heartRate - min) / (float)(max - min), 0f, 1f);
    }

    public static float ToFullRange(float normalized) => (normalized * 2f) - 1f;
}
