using System.Windows;
using System.Windows.Controls;

namespace vrcosc_magicchatbox.UI.Controls;

public partial class OptionGroupHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(OptionGroupHeader), new PropertyMetadata(string.Empty));

    public OptionGroupHeader() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
