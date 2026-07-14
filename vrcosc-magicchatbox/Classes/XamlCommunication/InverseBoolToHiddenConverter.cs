using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Returns <see cref="Visibility.Collapsed"/> when the bound boolean is <c>true</c>
    /// and <see cref="Visibility.Visible"/> when it is <c>false</c>.
    /// </summary>
    public class InverseBoolToHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bValue = (bool)value;
            if (!bValue)
                return Visibility.Collapsed;
            else
                return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            if (visibility != Visibility.Collapsed)
                return true;
            else
                return false;
        }
    }
}
