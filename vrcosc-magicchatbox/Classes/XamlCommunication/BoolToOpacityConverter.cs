using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Converts a boolean to an opacity value. Returns <see cref="TrueOpacity"/> when
    /// <c>true</c> and <see cref="FalseOpacity"/> when <c>false</c>.
    /// Defaults: <c>TrueOpacity = 1.0</c>, <c>FalseOpacity = 0.4</c>.
    /// </summary>
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
