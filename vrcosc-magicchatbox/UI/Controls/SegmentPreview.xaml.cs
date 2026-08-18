using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using vrcosc_magicchatbox.Core.Osc;

namespace vrcosc_magicchatbox.UI.Controls;

public partial class SegmentPreview : UserControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(SegmentPreview), new PropertyMetadata(string.Empty, OnCaptionChanged));

    public static readonly DependencyProperty LineProperty = DependencyProperty.Register(
        nameof(Line), typeof(string), typeof(SegmentPreview), new PropertyMetadata(string.Empty, OnLineChanged));

    public static readonly DependencyProperty CostTextProperty = DependencyProperty.Register(
        nameof(CostText), typeof(string), typeof(SegmentPreview), new PropertyMetadata("0/144"));

    public static readonly DependencyProperty HasCaptionProperty = DependencyProperty.Register(
        nameof(HasCaption), typeof(bool), typeof(SegmentPreview), new PropertyMetadata(false));

    public SegmentPreview()
    {
        InitializeComponent();
        Refresh();
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Line
    {
        get => (string)GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    public string CostText
    {
        get => (string)GetValue(CostTextProperty);
        private set => SetValue(CostTextProperty, value);
    }

    public bool HasCaption
    {
        get => (bool)GetValue(HasCaptionProperty);
        private set => SetValue(HasCaptionProperty, value);
    }

    private static void OnLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SegmentPreview)d).Refresh();

    private static void OnCaptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SegmentPreview)d).HasCaption = !string.IsNullOrWhiteSpace((string)e.NewValue);

    private void Refresh()
    {
        int length = Line?.Length ?? 0;
        CostText = $"{length}/{OscBuildContext.MaxOscLength}";

        CostChip.Background = OscPreviewFillLevel.Classify(length) switch
        {
            OscPreviewFill.Full => new SolidColorBrush(Color.FromRgb(0x7A, 0x2E, 0x3E)),
            OscPreviewFill.Tight => new SolidColorBrush(Color.FromRgb(0x6B, 0x54, 0x22)),
            _ => new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
        };
    }
}
