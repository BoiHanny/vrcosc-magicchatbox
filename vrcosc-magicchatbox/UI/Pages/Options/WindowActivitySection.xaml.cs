using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the active window / application activity settings section.</summary>
public partial class WindowActivitySection : UserControl
{
    public WindowActivitySection()
    {
        InitializeComponent();
    }

    private WindowActivitySectionViewModel? VM => DataContext as WindowActivitySectionViewModel;

    private void ScannedAppTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => VM?.WindowActivity.BeginScannedAppTextEdit();

    private void ScannedAppTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => VM?.WindowActivity.EndScannedAppTextEdit();
}
