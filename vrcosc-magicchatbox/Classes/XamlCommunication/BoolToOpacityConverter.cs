using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace vrcosc_magicchatbox.Classes
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = (bool)value;
            DoubleAnimation animation = new DoubleAnimation
            {
                From = boolValue ? 0 : 1,
                To = boolValue ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                BeginTime = TimeSpan.FromMilliseconds(0) // Set the delay before the animation starts
            };

            return animation;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
