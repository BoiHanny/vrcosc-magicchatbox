using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Effects;

namespace vrcosc_magicchatbox.Classes
{
    public class BlurRadiusToEffectConverter : IValueConverter
    {
        private static readonly object Gate = new();
        private static BlurEffect? _cached;
        private static double _cachedRadius = double.NaN;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double radius = value switch
            {
                int i => i,
                double d => d,
                _ => 0,
            };

            if (radius <= 0)
                return null;

            lock (Gate)
            {
                if (_cached is null || !_cachedRadius.Equals(radius))
                {
                    var effect = new BlurEffect { KernelType = KernelType.Gaussian, Radius = radius };
                    effect.Freeze();
                    _cached = effect;
                    _cachedRadius = radius;
                }

                return _cached;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
