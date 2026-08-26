using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool isSelected || !isSelected || parameter == null)
                return Binding.DoNothing;

            Type enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (!enumType.IsEnum)
                return Binding.DoNothing;

            string? parameterText = parameter.ToString();
            if (parameterText == null)
                return Binding.DoNothing;

            try
            {
                return Enum.Parse(enumType, parameterText, ignoreCase: true);
            }
            catch (ArgumentException)
            {
                return Binding.DoNothing;
            }
        }
    }
}
