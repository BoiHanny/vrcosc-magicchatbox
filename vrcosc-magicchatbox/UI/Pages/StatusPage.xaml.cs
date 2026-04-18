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
                var item = button?.Tag as StatusItem;
                if (item == null) return;

                if ((bool)button.IsChecked)
                {
                    VM.BeginEdit(item);

                    var parent = VisualTreeHelper.GetParent(button);
                    while (!(parent is ContentPresenter))
                        parent = VisualTreeHelper.GetParent(parent);
                    var contentPresenter = parent as ContentPresenter;
                    var editTextBox = (TextBox)contentPresenter.ContentTemplate.FindName("EditTextBox", contentPresenter);
                    editTextBox.Focus();
                    editTextBox.CaretIndex = editTextBox.Text.Length;
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

        // ── Sort ──────────────────────────────────────────────────────────────

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not StatusPageViewModel vm) return;
            if (sender is ComboBox combo && combo.SelectedIndex >= 0)
                vm.SortByFieldCommand.Execute((StatusSortField)combo.SelectedIndex);
        }

        // ── Group rename ──────────────────────────────────────────────────────

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

        // ── New group text box ────────────────────────────────────────────────

        private void NewGroupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                VM.ConfirmAddGroupCommand.Execute(null);
            else if (e.Key == Key.Escape)
                VM.NewGroupName = string.Empty;
        }

        // ── Selection mode checkboxes ─────────────────────────────────────────

        private void StatusItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is StatusItem item)
                VM.ToggleItemSelectedCommand.Execute(item);
        }
    }
}
