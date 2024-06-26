using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MagicChatboxV2.Startup.Windows
{
    public partial class LoadingWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string progressMessage;

        [ObservableProperty]
        private double progressValue;

        public LoadingWindowViewModel()
        {
            CancelCommand = new RelayCommand(Cancel);
        }

        public IRelayCommand CancelCommand { get; }

        private void Cancel()
        {
            Environment.Exit(1);
        }
    }
}
