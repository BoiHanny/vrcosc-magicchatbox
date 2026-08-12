using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes;

/// <summary>
/// True when the bound collection contains the key passed as the converter parameter. Lets sixteen
/// tiles each ask "am I in this set" against one published collection, rather than the state object
/// carrying sixteen near-identical booleans.
/// </summary>
public class CollectionContainsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<string> keys || parameter is not string key)
            return false;

        return keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
