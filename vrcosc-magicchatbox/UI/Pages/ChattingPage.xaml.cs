using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.UI.Pages
{
    /// <summary>Code-behind for the chatting page, handling keyboard input, inline editing, and scroll-to-end.</summary>
    public partial class ChattingPage : UserControl
    {
        private ChattingPageViewModel VM => (ChattingPageViewModel)DataContext;

        public ChattingPage()
        {
            InitializeComponent();
            Loaded += (_, _) => VM.ScrollToEndRequested += () => RecentScroll.ScrollToEnd();
        }

        public void SendChat() => ButtonChattingTxt_Click(null, null);

        private void ButtonChattingTxt_Click(object sender, RoutedEventArgs e)
            => VM.SendChat();

        private void CancelEditChatbutton_Click(object sender, RoutedEventArgs e)
            => VM.CancelEditCommand.Execute(null);

        private void ClearChat_Click(object sender, RoutedEventArgs e)
            => VM.ClearChatCommand.Execute(null);

        private void StopChat_Click(object sender, RoutedEventArgs e)
            => VM.StopChatCommand.Execute(null);

        private void NewChattingTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ButtonChattingTxt_Click(sender, e);
            if (e.Key == Key.Escape)
                VM.ClearChatInputCommand.Execute(null);
        }

        private void NewChattingTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            VM.UpdateChatBoxCount(textBox?.Text.Length ?? 0);
        }

        private void EditChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textbox = sender as TextBox;
            if (e.Key == Key.Enter)
            {
                if (VM.HandleEditEnter(textbox?.Text ?? ""))
                {
                    NewChattingTxt.Focus();
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                }
            }
            if (e.Key == Key.Escape)
                VM.HandleEditEscape();
        }

        private void EditChatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textbox = sender as TextBox;
            if (textbox != null)
                VM.HandleEditTextChanged(textbox.Text);
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ToggleButton;
                var item = button?.Tag as ChatItem;
                if (item == null) return;

                if ((bool)button.IsChecked)
                {
                    VM.BeginChatEdit(item);

                    // Focus the edit textbox (UI concern)
                    var parent = VisualTreeHelper.GetParent(button);
                    while (!(parent is ContentPresenter))
                        parent = VisualTreeHelper.GetParent(parent);
                    var contentPresenter = parent as ContentPresenter;
                    var EditChatTextBox = (TextBox)contentPresenter.ContentTemplate.FindName("EditChatTextBox", contentPresenter);
                    EditChatTextBox.Focus();
                    EditChatTextBox.CaretIndex = EditChatTextBox.Text.Length;
                }
                else
                {
                    if (VM.ConfirmChatEdit(item))
                    {
                        NewChattingTxt.Focus();
                        NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void OnSendAgain(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            VM.SendAgain(button?.Tag as ChatItem);
        }

        private void AcceptAndSentIntelliChat_Click(object sender, RoutedEventArgs e)
            => VM.AcceptIntelliChatAndSendCommand.Execute(null);
    }
}
