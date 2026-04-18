using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Converts an enum value to its <see cref="System.ComponentModel.DescriptionAttribute"/>
    /// text for display in the UI.
    /// </summary>
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var enumValue = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute)enumValue.GetCustomAttributes(typeof(DescriptionAttribute), false)[0];
            return attribute.Description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
