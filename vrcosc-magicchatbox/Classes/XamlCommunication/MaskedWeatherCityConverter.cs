using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    public class MaskedWeatherCityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (trimmed.Length <= 3)
            {
                return trimmed;
            }

            char last = trimmed[trimmed.Length - 1];
            return $"{trimmed.Substring(0, 3)}***{last}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value as string ?? string.Empty;
        }
    }
}
