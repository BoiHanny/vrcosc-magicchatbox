using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Converts a selected index (int) to Visibility.
    /// If the bound value equals the ConverterParameter, returns Visible; otherwise Hidden.
    /// Usage: Visibility="{Binding SelectedMenuIndex, Converter={StaticResource IndexToVisibilityConverter}, ConverterParameter=0}"
    /// </summary>
    public class IndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int selectedIndex && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
            {
                return selectedIndex == targetIndex ? Visibility.Visible : Visibility.Hidden;
            }
            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility && visibility == Visibility.Visible
                && parameter is string paramStr && int.TryParse(paramStr, out int targetIndex))
            {
                return targetIndex;
            }
            return Binding.DoNothing;
        }
    }
}
