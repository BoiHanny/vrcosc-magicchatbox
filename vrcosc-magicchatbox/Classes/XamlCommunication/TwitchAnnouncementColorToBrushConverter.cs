using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Converts a <see cref="TwitchAnnouncementColor"/> enum value to a frozen
    /// <see cref="SolidColorBrush"/> matching Twitch's announcement colour palette.
    /// Returns <see cref="Brushes.Transparent"/> for unrecognised values.
    /// </summary>
    public class TwitchAnnouncementColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TwitchAnnouncementColor color)
            {
                return Brushes.Transparent;
            }

            Color resolved = color switch
            {
                TwitchAnnouncementColor.Blue => Color.FromRgb(64, 149, 255),
                TwitchAnnouncementColor.Green => Color.FromRgb(46, 204, 113),
                TwitchAnnouncementColor.Orange => Color.FromRgb(255, 140, 66),
                TwitchAnnouncementColor.Purple => Color.FromRgb(155, 89, 182),
                _ => Color.FromRgb(145, 70, 255)
            };

            var brush = new SolidColorBrush(resolved);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
