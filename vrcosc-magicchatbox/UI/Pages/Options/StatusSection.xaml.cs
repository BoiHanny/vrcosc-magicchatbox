using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.ViewModels.Sections;

namespace vrcosc_magicchatbox.UI.Pages.Options;

/// <summary>Code-behind for the status message settings section.</summary>
public partial class StatusSection : UserControl
{
    private StatusSectionViewModel VM => (StatusSectionViewModel)DataContext;

    public StatusSection()
    {
        InitializeComponent();
    }

    private void AddEmojiButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        bool added = VM.Emojis.AddEmoji(EmojiNew.Text);
        if (added)
            EmojiNew.Clear();
    }

    private void EmojiNew_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            AddEmojiButton_Click(sender, e);
    }
}
