using System;
using System.Globalization;
using System.Windows.Data;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Returns <c>true</c> when the bound character count exceeds 140, used to
    /// trigger over-limit visual feedback in the UI.
    /// </summary>
    public class CharacterCountToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int characterCount = (int)value;
            return characterCount > 140;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
