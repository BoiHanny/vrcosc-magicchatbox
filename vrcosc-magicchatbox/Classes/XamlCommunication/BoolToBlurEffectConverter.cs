using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Effects;

namespace vrcosc_magicchatbox.Classes
{
    public class BoolToBlurEffectConverter : IValueConverter
    {
        private static readonly BlurEffect Blurred = CreateBlurred();

        private static BlurEffect CreateBlurred()
        {
            var effect = new BlurEffect { Radius = 5 };
            effect.Freeze();
            return effect;
        }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLiveTime && !isLiveTime)
            {
                return Blurred;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
