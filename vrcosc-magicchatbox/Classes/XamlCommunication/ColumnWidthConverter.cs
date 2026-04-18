using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Converts an <c>ActualWidth</c> double to a column width by subtracting a fixed
    /// 50-pixel offset. Returns 0 if the result would be negative.
    /// </summary>
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth)
            {
                double subtractedWidth = 50;
                double resultWidth = actualWidth - subtractedWidth;
                return resultWidth > 0 ? resultWidth : 0;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
