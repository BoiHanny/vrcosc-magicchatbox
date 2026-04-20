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

        private Action? _scrollToEndHandler;

        public ChattingPage()
        {
            InitializeComponent();
            // Wire scroll-to-end when DataContext arrives (may be deferred past Show).
            DataContextChanged += (_, args) =>
            {
                // Unsubscribe from old DataContext
                if (args.OldValue is ChattingPageViewModel oldVm && _scrollToEndHandler != null)
                    oldVm.ScrollToEndRequested -= _scrollToEndHandler;

                if (args.NewValue is ChattingPageViewModel vm)
                {
                    _scrollToEndHandler = () => RecentScroll.ScrollToEnd();
                    vm.ScrollToEndRequested += _scrollToEndHandler;
                }
            };
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
                if (button == null) return;
                var item = button.Tag as ChatItem;
                if (item == null) return;

                if (button.IsChecked == true)
                {
                    VM.BeginChatEdit(item);

                    // Focus the edit textbox (UI concern)
                    var parent = VisualTreeHelper.GetParent(button);
                    while (parent != null && parent is not ContentPresenter)
                        parent = VisualTreeHelper.GetParent(parent);
                    if (parent is ContentPresenter contentPresenter)
                    {
                        var editTextBox = contentPresenter.ContentTemplate.FindName("EditChatTextBox", contentPresenter) as TextBox;
                        if (editTextBox != null)
                        {
                            editTextBox.Focus();
                            editTextBox.CaretIndex = editTextBox.Text.Length;
                        }
                    }
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
