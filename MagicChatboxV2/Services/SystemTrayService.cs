using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static MagicChatboxV2.App;

namespace MagicChatboxV2.Services
{
    public class SystemTrayService
    {
        private readonly PrimaryInterfaceFactory _mainWindowFactory;
        private TaskbarIcon tbIcon;

        public SystemTrayService(PrimaryInterfaceFactory mainWindowFactory)
        {
            _mainWindowFactory = mainWindowFactory;
        }

        public void InitializeTrayIcon()
        {
            tbIcon = new TaskbarIcon
            {
                Icon = LoadIconFromResource("UIVM/Images/MagicOSC_icon.png"),
                ToolTipText = "MagicChatboxV2"
            };

            tbIcon.ContextMenu = new ContextMenu
            {
                Items =
                {
                    new MenuItem { Header = "Start", Command = new RelayCommand(StartApplication) },
                    new MenuItem { Header = "Exit", Command = new RelayCommand(ExitApplication) }
                }
            };
        }

        public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.None)
        {
            tbIcon.ShowBalloonTip(title, message, icon);
        }


        private Icon LoadIconFromResource(string resourcePath)
        {
            Uri iconUri = new Uri($"pack://application:,,,/{resourcePath}", UriKind.RelativeOrAbsolute);
            BitmapImage bitmapImage = new BitmapImage(iconUri);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Load the bitmap from stream and create an Icon
                using (var bitmap = new Bitmap(memoryStream))
                {
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
        }

        public void StartApplication()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = _mainWindowFactory();
                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }
                else
                {
                    mainWindow.Activate();
                }
            });
        }


        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }
    }
}
