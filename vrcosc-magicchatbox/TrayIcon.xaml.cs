using CommunityToolkit.Mvvm.Input;
using NullSoftware.ToolKit;
using System.Windows;
using System.Windows.Input;

namespace vrcosc_magicchatbox
{
    /// <summary>
    /// Interaction logic for TrayIcon.xaml
    /// </summary>
    public partial class TrayIcon : Window
    {
        private MainWindow mainWindow = App.mainWindow;
        public INotificationService NotificationService { get; set; }

        public ICommand ShowMainWindow { get; }

        public ICommand ShowIntegrations { get; }
        public ICommand ShowStatus { get; }
        public ICommand ShowChatting { get; }
        public ICommand ShowOptions { get; }

        public ICommand ToggleMasterSwitch { get; }
        public ICommand ToggleAFK { get; }
        public ICommand ToggleVoiceChat { get; }

        public ICommand CloseApplication { get; }

        public TrayIcon()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                DataContext = this;
                this.Hide();
            };

            ShowMainWindow = new RelayCommand(() =>
            {
                ShowMainWindows();
            });

            //----------------
            //----------------

            ShowIntegrations = new RelayCommand(() =>
            {
                if (!mainWindow.IsVisible)
                    ShowMainWindows();
                mainWindow.VM.SelectedMenuIndex = 0;
            });

            ShowStatus = new RelayCommand(() =>
            {
                if (!mainWindow.IsVisible)
                    ShowMainWindows();
                mainWindow.VM.SelectedMenuIndex = 1;
            });

            ShowChatting = new RelayCommand(() =>
            {
                if (!mainWindow.IsVisible)
                    ShowMainWindows();
                mainWindow.VM.SelectedMenuIndex = 2;
            });

            ShowOptions = new RelayCommand(() =>
            {
                if (!mainWindow.IsVisible)
                    ShowMainWindows();
                mainWindow.VM.SelectedMenuIndex = 3;
            });

            //----------------
            //----------------

            MasterSwitch.IsChecked = mainWindow.VM.MasterSwitch;
            ToggleMasterSwitch = new RelayCommand(() =>
            {
                mainWindow.VM.MasterSwitch = !mainWindow.VM.MasterSwitch;
                mainWindow.VM.MasterSwitchToggledCommand.Execute(true);
                MasterSwitch.IsChecked = mainWindow.VM.MasterSwitch;
            });

            AFKSwitch.IsChecked = mainWindow.VM.Modules.Afk.Settings.OverrideAfk;
            ToggleAFK = new RelayCommand(() =>
            {
                mainWindow.VM.Modules.Afk.Settings.OverrideAfk = !mainWindow.VM.Modules.Afk.Settings.OverrideAfk;
                AFKSwitch.IsChecked = mainWindow.VM.Modules.Afk.Settings.OverrideAfk;
            });

            VoiceChat.Header = mainWindow.VM.TtsAudio.ToggleVoiceText;
            ToggleVoiceChat = new RelayCommand(() =>
            {
                mainWindow.VM.ToggleVoiceCommand.Execute(true);
            });

            //----------------
            //----------------

            CloseApplication = new RelayCommand(() =>
            {
                mainWindow._isTrayClosing = true;
                mainWindow.Close();
            });
        }

        private void ShowMainWindows()
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Focus();
        }

        public void Notify(string text)
        {
            NotificationService.Notify("MagicChatbox", text);
        }
    }
}
