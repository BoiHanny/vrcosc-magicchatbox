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
            if (enumValue == null) return value.ToString();

            var attributes = enumValue.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 && attributes[0] is DescriptionAttribute attribute
                ? attribute.Description
                : value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
