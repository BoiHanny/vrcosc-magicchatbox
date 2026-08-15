using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int selectedIndex
               && parameter is string text
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetIndex)
               && selectedIndex == targetIndex;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive
                && isActive
                && parameter is string text
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetIndex))
            {
                return targetIndex;
            }

            return Binding.DoNothing;
        }
    }
}
