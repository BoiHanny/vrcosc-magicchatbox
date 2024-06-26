using System;
using System.Windows;

namespace MagicChatboxV2.Services
{
    public interface IDialogService
    {
        bool ShowErrorDialog(Exception ex, string message, int timeout, bool autoclose, bool allowContinue);
        void ShowInfoDialog(string message, int timeout, bool autoclose);
    }

    public class DialogService : IDialogService
    {
        public bool ShowErrorDialog(Exception ex, string message, int timeout, bool autoclose, bool allowContinue)
        {
            MessageBoxButton buttons = allowContinue ? MessageBoxButton.YesNo : MessageBoxButton.OK;
            MessageBoxResult result = MessageBox.Show(message, "Error", buttons, MessageBoxImage.Error);
            return result == MessageBoxResult.Yes;
        }

        public void ShowInfoDialog(string message, int timeout, bool autoclose)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
