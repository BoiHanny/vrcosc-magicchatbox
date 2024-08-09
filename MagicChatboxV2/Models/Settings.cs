using CommunityToolkit.Mvvm.ComponentModel;

namespace MagicChatboxV2.Models
{
    public partial class Settings : ObservableRecipient, ISettings
    {
        [ObservableProperty]
        private bool enabled;

        [ObservableProperty]
        private bool enabledVR;

        [ObservableProperty]
        private bool enabledDesktop;

        [ObservableProperty]
        private string settingVersion;

        [ObservableProperty]
        private int modulePosition;

        [ObservableProperty]
        private int moduleMemberGroupNumbers;

        public void Dispose()
        {
            // Dispose of unmanaged resources here.
        }
    }
}
