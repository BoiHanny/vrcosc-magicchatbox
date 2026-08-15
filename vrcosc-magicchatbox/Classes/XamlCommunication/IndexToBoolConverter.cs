using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// True when the bound index is the one named in the parameter.
    /// </summary>
    /// <remarks>
    /// The visibility flavour of this already existed, but a control template needs a bool to
    /// trigger on - Visible and Hidden are the wrong vocabulary for "this is the tab you are on".
    /// </remarks>
    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int selectedIndex
               && parameter is string text
               && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetIndex)
               && selectedIndex == targetIndex;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive
                && isActive
                && parameter is string text
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetIndex))
            {
                return targetIndex;
            }

            return Binding.DoNothing;
        }
    }
}
