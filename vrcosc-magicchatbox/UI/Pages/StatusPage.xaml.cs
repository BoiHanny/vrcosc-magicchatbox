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
    /// <summary>Code-behind for the status page, handling inline editing, favorite toggling, groups, sorting, and selection mode.</summary>
    public partial class StatusPage : UserControl
    {
        private StatusPageViewModel VM => (StatusPageViewModel)DataContext;

        // Debounce for popup close→toggle reopen issue
        private DateTime _groupPopupClosedAt;
        private DateTime _movePopupClosedAt;

        public StatusPage()
        {
            InitializeComponent();
        }

        private void AddFav_Click(object sender, RoutedEventArgs e)
            => VM.AddStatusCommand.Execute(null);

        private void CancelEditbutton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            VM.CancelEditCommand.Execute(button?.Tag as StatusItem);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            VM.DeleteStatusCommand.Execute(button?.Tag as StatusItem);
        }

        private void Editbutton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as ToggleButton;
                if (button == null) return;
                var item = button.Tag as StatusItem;
                if (item == null) return;

                if (button.IsChecked == true)
                {
                    VM.BeginEdit(item);

                    var parent = VisualTreeHelper.GetParent(button);
                    while (parent != null && parent is not ContentPresenter)
                        parent = VisualTreeHelper.GetParent(parent);
                    if (parent is ContentPresenter contentPresenter)
                    {
                        var editTextBox = contentPresenter.ContentTemplate.FindName("EditTextBox", contentPresenter) as TextBox;
                        if (editTextBox != null)
                        {
                            editTextBox.Focus();
                            editTextBox.CaretIndex = editTextBox.Text.Length;
                        }
                    }
                }
                else
                {
                    VM.ConfirmEdit(item);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private void FavBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM.AddStatusCommand.Execute(null);
            if (e.Key == Key.Escape)
                VM.ClearStatusInputCommand.Execute(null);
        }

        private void Favbutton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StatusItem item)
                VM.ToggleFavoriteCommand.Execute(item);
        }

        private void NewFavText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            VM.UpdateStatusBoxCount(textBox?.Text.Length ?? 0);
        }

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not StatusPageViewModel vm) return;
            if (sender is ComboBox combo && combo.SelectedIndex >= 0)
                vm.SortByFieldCommand.Execute((StatusSortField)combo.SelectedIndex);
        }

        private void BeginRenameGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StatusGroup group)
                VM.BeginRenameGroupCommand.Execute(group);
        }

        private void RenameGroupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is StatusGroup group)
            {
                if (e.Key == Key.Enter)
                    VM.ConfirmRenameGroupCommand.Execute(group);
                else if (e.Key == Key.Escape)
                    VM.CancelRenameGroupCommand.Execute(group);
            }
        }

        private void NewGroupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM.ConfirmAddGroupCommand.Execute(null);
            else if (e.Key == Key.Escape)
                VM.NewGroupName = string.Empty;
        }

        private void StatusItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // The TwoWay binding on IsChecked already toggled IsSelected.
            // We only need to sync the SelectedItems collection here.
            if (sender is CheckBox cb && cb.Tag is StatusItem item)
            {
                if (item.IsSelected && !VM.SelectedItems.Contains(item))
                    VM.SelectedItems.Add(item);
                else if (!item.IsSelected)
                    VM.SelectedItems.Remove(item);
            }
        }

        private void StatusItemRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!VM.IsSelectionMode) return;
            // Don't toggle if the click was on a button or checkbox
            if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase
                || e.OriginalSource is CheckBox)
                return;
            if (sender is Border border && border.Tag is StatusItem item)
            {
                VM.ToggleItemSelectedCommand.Execute(item);
                e.Handled = true;
            }
        }

        private void GroupPopup_Closed(object sender, EventArgs e)
            => _groupPopupClosedAt = DateTime.UtcNow;

        private void GroupToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.IsChecked == true
                && (DateTime.UtcNow - _groupPopupClosedAt).TotalMilliseconds < 300)
            {
                tb.IsChecked = false;
            }
        }

        private void MovePopup_Closed(object sender, EventArgs e)
            => _movePopupClosedAt = DateTime.UtcNow;

        private void MoveToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb && tb.IsChecked == true
                && (DateTime.UtcNow - _movePopupClosedAt).TotalMilliseconds < 300)
            {
                tb.IsChecked = false;
            }
        }
    }
}
