using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;

namespace vrcosc_magicchatbox.UI.Windows
{
    public partial class ColorCustomizationViewModel : ObservableObject
    {
        [ObservableProperty]
        private Color backgroundColor = Color.FromArgb(0xFF, 0x2D, 0x12, 0x67);

        [ObservableProperty]
        private Color backgroundTxtColor;

        [ObservableProperty]
        private Color secondaryColor;

        [ObservableProperty]
        private Color secondarTextColor;

        public ColorCustomizationViewModel()
        {
            UpdateSecondaryColor();
            UpdateTextColors();
        }

        partial void OnBackgroundColorChanged(Color value)
        {
            UpdateSecondaryColor();
            UpdateTextColors();
        }

        private void UpdateTextColors()
        {
            // Adjust the text colors based on the background and secondary colors
            BackgroundTxtColor = SecondaryColor;
            SecondarTextColor = BackgroundColor;
        }

        private void UpdateSecondaryColor()
        {
            // Convert RGB to HSL
            var (hue, saturation, lightness) = RgbToHsl(BackgroundColor);

            // Adjust lightness for secondary color to ensure contrast
            lightness = AdjustLightnessForContrast(lightness);

            // Convert HSL back to RGB for the secondary color
            SecondaryColor = HslToRgb(hue, saturation, lightness);
        }

        private Color AdjustColorLightness(Color color, bool makeLighter)
        {
            // Convert RGB to HSL
            var (hue, saturation, lightness) = RgbToHsl(color);

            // Adjust lightness
            if (makeLighter)
            {
                lightness = Math.Min(lightness + 0.3, 1); // Ensure lightness does not exceed 1
            }
            else
            {
                lightness = Math.Max(lightness - 0.3, 0); // Ensure lightness does not go below 0
            }

            // Convert HSL back to RGB
            return HslToRgb(hue, saturation, lightness);
        }

        private (double hue, double saturation, double lightness) RgbToHsl(Color color)
        {
            // Convert RGB to HSL
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h, s, l;
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (max == g)
                {
                    h = (b - r) / d + 2;
                }
                else
                {
                    h = (r - g) / d + 4;
                }
                h /= 6;
            }
            return (h, s, l);
        }

        private Color HslToRgb(double h, double s, double l)
        {
            // Convert HSL back to RGB
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                Func<double, double, double, double> hue2rgb = (p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1 / 6.0) return p + (q - p) * 6 * t;
                    if (t < 1 / 2.0) return q;
                    if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
                    return p;
                };

                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = hue2rgb(p, q, h + 1 / 3.0);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1 / 3.0);
            }
            return Color.FromArgb(BackgroundColor.A, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private double AdjustLightnessForContrast(double lightness)
        {
            // Ensure there is enough contrast between the primary and secondary colors
            const double darkLightnessValue = 0.25; // A fixed lightness value for dark colors
            const double contrastThreshold = 0.7;
            if (lightness < contrastThreshold)
            {
                // If primary color is dark, make secondary color lighter
                return lightness + (1 - contrastThreshold);
            }
            else
            {
                // If primary color is light, make secondary color darker by setting it to a fixed dark value
                return darkLightnessValue;
            }
        }
    }
}