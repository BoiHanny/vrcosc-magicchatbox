using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.UI.Pages;

/// <summary>Code-behind for the options page, wiring routed toggle events and delegating TTS output selection.</summary>
public partial class OptionsPage : UserControl
{
    private OptionsPageViewModel VM => (OptionsPageViewModel)DataContext;

    public OptionsPage()
    {
        InitializeComponent();

        // Handle all checkbox/toggle state changes via routed event bubbling
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
            new RoutedEventHandler(OnSettingToggled));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
            new RoutedEventHandler(OnSettingToggled));
    }

    private void OnSettingToggled(object sender, RoutedEventArgs e)
        => VM.OnSettingToggled();

    public void SelectTTSOutput()
        => TtsOptionsSectionControl.SelectTTSOutput();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        VM.Navigation.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
