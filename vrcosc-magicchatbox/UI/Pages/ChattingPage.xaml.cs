using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.UI.Pages
{
    public partial class ChattingPage : UserControl
    {
        private ChattingPageViewModel VM => (ChattingPageViewModel)DataContext;

        private Action? _scrollToEndHandler;

        private ChattingPageViewModel? _attachedVm;

        public ChattingPage()
        {
            InitializeComponent();
            Loaded += (_, _) => FocusChatInput();
            IsVisibleChanged += (_, args) =>
            {
                if (args.NewValue is true)
                    FocusChatInput();
            };

            DataContextChanged += (_, args) =>
            {
                if (args.OldValue is ChattingPageViewModel oldVm)
                    Detach(oldVm);

                if (args.NewValue is ChattingPageViewModel)
                    Attach();
            };

            Loaded += (_, _) => Attach();
            Unloaded += (_, _) => Detach(_attachedVm);
        }

        private void Attach()
        {
            if (_scrollToEndHandler != null || DataContext is not ChattingPageViewModel vm)
                return;

            _scrollToEndHandler = () => RecentScroll.ScrollToEnd();
            vm.ScrollToEndRequested += _scrollToEndHandler;
            _attachedVm = vm;
        }

        private void Detach(ChattingPageViewModel? vm)
        {
            if (_scrollToEndHandler == null)
                return;

            if (vm != null)
                vm.ScrollToEndRequested -= _scrollToEndHandler;

            _scrollToEndHandler = null;
            _attachedVm = null;
        }

        public void SendChat() => ButtonChattingTxt_Click(null, null);

        public void FocusChatInput()
            => Dispatcher.BeginInvoke(() =>
            {
                if (!IsVisible)
                    return;

                NewChattingTxt.Focus();
                Keyboard.Focus(NewChattingTxt);
                NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
            }, DispatcherPriority.Input);

        private void ButtonChattingTxt_Click(object? sender, RoutedEventArgs? e)
            => VM.SendChat();

        private void CancelEditChatbutton_Click(object sender, RoutedEventArgs e)
            => VM.CancelEditCommand.Execute(EditedItem(sender));

        private static ChatItem? EditedItem(object sender)
            => (sender as FrameworkElement)?.DataContext as ChatItem;

        private void ClearChat_Click(object sender, RoutedEventArgs e)
            => VM.ClearChatCommand.Execute(null);

        private void StopChat_Click(object sender, RoutedEventArgs e)
            => VM.StopChatCommand.Execute(null);

        private void NewChattingTxt_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if ((e.Key == Key.Tab || e.Key == Key.Right)
                && textBox != null
                && textBox.CaretIndex == textBox.Text.Length
                && VM.AcceptAutocompleteSuggestion())
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(() =>
                {
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                    NewChattingTxt.Focus();
                });
                return;
            }

            if (e.Key == Key.Enter)
            {
                ButtonChattingTxt_Click(sender, e);
                e.Handled = true;
            }
            if (e.Key == Key.Escape)
            {
                VM.ClearAutocompleteSuggestion();
                VM.ClearChatInputCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void NewChattingTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            VM.UpdateChatBoxCount(textBox?.Text ?? string.Empty);
        }

        private void NewChattingTxt_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is not ChattingPageViewModel vm)
                return;

            if (e.NewFocus is DependencyObject moved && IsOnThisPage(moved))
                return;

            vm.FinishLiveLine();
        }

        private bool IsOnThisPage(DependencyObject element)
        {
            for (DependencyObject? node = element; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (ReferenceEquals(node, this))
                    return true;
            }

            return false;
        }

        private void EditChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textbox = sender as TextBox;
            if (e.Key == Key.Enter)
            {
                if (VM.HandleEditEnter(EditedItem(sender), textbox?.Text ?? ""))
                {
                    NewChattingTxt.Focus();
                    NewChattingTxt.CaretIndex = NewChattingTxt.Text.Length;
                }
                e.Handled = true;
            }
            if (e.Key == Key.Escape)
            {
                VM.HandleEditEscape(EditedItem(sender));
                e.Handled = true;
            }
        }

        private void EditChatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textbox)
                VM.HandleEditTextChanged(EditedItem(sender), textbox.Text);
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
