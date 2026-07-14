using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Returns <c>true</c> when the bound count value meets or exceeds the threshold
    /// supplied via <c>ConverterParameter</c>. Accepts <see cref="int"/>,
    /// <see cref="System.Collections.ICollection"/>, or any <see cref="System.Collections.IEnumerable"/>.
    /// </summary>
    public class CountThresholdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return false;
            }

            int threshold = 0;
            if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
            {
                threshold = parsed;
            }

            int count = value switch
            {
                int directCount => directCount,
                ICollection collection => collection.Count,
                IEnumerable enumerable => enumerable.Cast<object>().Count(),
                _ => 0
            };

            return count >= threshold;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
