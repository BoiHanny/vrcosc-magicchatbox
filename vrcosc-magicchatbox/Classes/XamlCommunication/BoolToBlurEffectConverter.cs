

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Effects;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Returns a <see cref="BlurEffect"/> with a radius of 5 when the bound boolean is
    /// <c>false</c>, and a zero-radius effect (no blur) when it is <c>true</c>.
    /// </summary>
    public class BoolToBlurEffectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLiveTime && !isLiveTime)
            {
                return new BlurEffect { Radius = 5 };
            }

            return new BlurEffect { Radius = 0 };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
