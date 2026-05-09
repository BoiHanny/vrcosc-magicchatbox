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

        public TrayIcon()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                DataContext = this;
                this.Hide();
            };

        }
        public void Notify(string text)
        {
            NotificationService.Notify("MagicChatbox", text);
        }
    }
}
