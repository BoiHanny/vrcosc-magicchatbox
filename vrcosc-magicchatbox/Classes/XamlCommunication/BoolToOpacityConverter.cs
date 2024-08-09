using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public double TrueOpacity { get; set; } = 1.0;
        public double FalseOpacity { get; set; } = 0.4;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueOpacity : FalseOpacity;
            }

            return TrueOpacity;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
