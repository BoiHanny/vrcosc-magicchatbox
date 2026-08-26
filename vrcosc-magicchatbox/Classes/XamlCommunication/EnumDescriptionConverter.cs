using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    public class EnumDescriptionConverter : IValueConverter
    {
        // A binding re-runs its converter whenever its source changes. Reflecting for the same attribute
        // every time is pure repeat work, so each enum value is resolved once and remembered.
        private static readonly ConcurrentDictionary<object, string> Descriptions = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            return Descriptions.GetOrAdd(value, static v =>
            {
                string name = v.ToString() ?? string.Empty;

                var field = v.GetType().GetField(name);
                if (field == null) return name;

                var attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                return attributes.Length > 0 && attributes[0] is DescriptionAttribute attribute
                    ? attribute.Description
                    : name;
            });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
