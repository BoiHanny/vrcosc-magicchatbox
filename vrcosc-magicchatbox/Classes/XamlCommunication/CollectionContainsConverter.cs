using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes;

public class CollectionContainsConverter : IValueConverter
{
    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyCollection<string> keys || parameter is not string key)
            return BoxedFalse;

        if (keys is IReadOnlyList<string> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], key, StringComparison.OrdinalIgnoreCase))
                    return BoxedTrue;
            }

            return BoxedFalse;
        }

        foreach (var item in keys)
        {
            if (string.Equals(item, key, StringComparison.OrdinalIgnoreCase))
                return BoxedTrue;
        }

        return BoxedFalse;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
