using System;
using System.Globalization;
using System.Windows.Data;
using vrcosc_magicchatbox.Core.Updates;

namespace vrcosc_magicchatbox.Classes
{
    /// <summary>
    /// Presents an <see cref="UpdateChannelMode"/> as a plain on/off tick for the update card.
    /// </summary>
    /// <remarks>
    /// The channel has three settings and the tick only has two, so unticking it lands on
    /// <see cref="UpdateChannelMode.Notify"/> rather than <see cref="UpdateChannelMode.Off"/>: someone
    /// turning off automatic installing is saying "ask me first", not "stop telling me about updates".
    /// Turning updates off entirely stays where it already is, on the Options page.
    ///
    /// <c>EnumEqualsConverter</c> cannot do this - its ConvertBack returns <c>Binding.DoNothing</c> when
    /// unchecked, which is right for a radio group and would silently ignore unticking a checkbox.
    /// </remarks>
    public class AutoUpdateModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is UpdateChannelMode mode && mode == UpdateChannelMode.Auto;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? UpdateChannelMode.Auto : UpdateChannelMode.Notify;
    }
}
