using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes;

public class VersionDisplayConverter : IValueConverter
{
    public static string Describe(Version? version)
    {
        if (version is null)
        {
            return string.Empty;
        }

        int parts = version.Revision > 0 ? 4 : version.Build >= 0 ? 3 : 2;
        return version.ToString(parts);
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string text = Describe(value as Version);

        return parameter is string format && !string.IsNullOrEmpty(format) && text.Length > 0
            ? string.Format(culture, format, text)
            : text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
