using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Returns <see cref="Visibility.Visible"/> when the bound integer count is
    /// greater than zero; otherwise <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
