using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.Classes;

public sealed class ReadinessStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is ReadinessState state
            ? state switch
            {
                ReadinessState.Driving => "StatusSuccessBrush",
                ReadinessState.Faulted => "StatusErrorBrush",
                ReadinessState.FoundOtherPrefab => "StatusWarningBrush",
                ReadinessState.RouteOff => "StatusWarningBrush",
                _ => "TextMutedBrush",
            }
            : "TextMutedBrush";

        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
