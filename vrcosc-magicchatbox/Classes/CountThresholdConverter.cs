using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
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
