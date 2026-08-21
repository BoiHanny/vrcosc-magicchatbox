using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using vrcosc_magicchatbox.Core.Updates;

namespace vrcosc_magicchatbox.Classes.XamlCommunication;

public class UpdateStepStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Pending = Frozen(0xB5, 0xAB, 0xC9);
    private static readonly SolidColorBrush Running = Frozen(0xC3, 0xA9, 0xFF);
    private static readonly SolidColorBrush Done = Frozen(0x6F, 0xD8, 0x9B);
    private static readonly SolidColorBrush Warning = Frozen(0xFF, 0xC1, 0x07);
    private static readonly SolidColorBrush Failed = Frozen(0xF3, 0x67, 0x34);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is UpdateStepStatus status
            ? status switch
            {
                UpdateStepStatus.Running => Running,
                UpdateStepStatus.Done => Done,
                UpdateStepStatus.Warning => Warning,
                UpdateStepStatus.Failed => Failed,
                _ => Pending
            }
            : Pending;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
