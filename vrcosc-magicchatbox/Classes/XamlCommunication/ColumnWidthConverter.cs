using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace vrcosc_magicchatbox.Classes
{
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth)
            {
                double subtractedWidth = 50; // Adjust this value as necessary.

                // Ensure the returned width is never negative.
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
