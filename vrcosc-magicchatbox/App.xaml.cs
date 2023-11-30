using NLog;
using System;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox
{
    public partial class App : Application
    {
        public static MediaLinkModule ApplicationMediaController { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var loadingWindow = new StartUp();
            loadingWindow.Show();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException; ; ;
            DispatcherUnhandledException += App_DispatcherUnhandledException; ; ;

            if (e.Args != null && e.Args.Length > 0)
            {
                if (e.Args[0] == "-update")
                {
                    loadingWindow.UpdateProgress("Go, go, go! Update, update, update!", 75);
                    UpdateApp updater = new UpdateApp();
                    await Task.Run(() => updater.UpdateApplication());
                    Shutdown();
                    return;
                }
                if (e.Args[0] == "-updateadmin")
                {
                    loadingWindow.UpdateProgress("Admin style update, now that's fancy!", 85);
                    UpdateApp updater = new UpdateApp();
                    await Task.Run(() => updater.UpdateApplication(true));
                    Shutdown();
                    return;
                }
            }

            loadingWindow.UpdateProgress("Rousing the logging module... It's coffee time, logs!", 10);
            await Task.Run(() => LogManager.LoadConfiguration("NLog.config"));

            loadingWindow.UpdateProgress("Sifting through your ancient settings... Indiana Jones, is that you?", 20);
            await Task.Run(() => DataController.ManageSettingsXML());

            loadingWindow.UpdateProgress("Gathering status items like a squirrel with nuts!", 30);
            await Task.Run(() => DataController.LoadStatusList());

            loadingWindow.UpdateProgress("Detective on the hunt for last session's chat messages... Elementary, my dear Watson!", 40);
            await Task.Run(() => DataController.LoadChatList());

            loadingWindow.UpdateProgress("Going on a treasure hunt for MediaLink settings... Ahoy, Captain!", 50);
            await Task.Run(() => DataController.LoadMediaSessions());

            loadingWindow.UpdateProgress("Selecting recent apps for window integration, like picking the A-Team!", 60);
            await Task.Run(() => DataController.LoadAppList());

            if (ViewModel.Instance.IntgrComponentStats)
            {
                loadingWindow.UpdateProgress("Lighting up ComponentStats like it's the 4th of July. Ka-boom!", 65);
                await Task.Run(() => ViewModel.Instance._statsManager.StartModule());
            }

            loadingWindow.UpdateProgress("Initializing OpenAI like a rocket launch. 3... 2... 1... Blast off!", 70);
            await Task.Run(() => OpenAIModule.Instance.InitializeClient(ViewModel.Instance.OpenAIAccessToken, ViewModel.Instance.OpenAIOrganizationID));

            loadingWindow.UpdateProgress("Warming up the TTS voices. Ready for the vocal Olympics!", 75);
            ViewModel.Instance.TikTokTTSVoices = await Task.Run(() => DataController.ReadTkTkTTSVoices());

            loadingWindow.UpdateProgress("Selecting your audio devices like a DJ choosing beats. Drop the bass!", 80);
            await Task.Run(() => DataController.PopulateOutputDevices());

            loadingWindow.UpdateProgress("Turbocharging MediaLink engines... Fast & Furious: Data Drift!", 95);
            ApplicationMediaController = new MediaLinkModule(ViewModel.Instance.IntgrScanMediaLink);

            MainWindow mainWindow = new MainWindow();
            mainWindow.DataContext = ViewModel.Instance;

            loadingWindow.UpdateProgress("Setting up the hotkeys... Hotkey, hotkey, hotkey!", 97);

            HotkeyManagement.Instance.Initialize(mainWindow);
            mainWindow.Show();

            loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 100);
            loadingWindow.Close();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logging.WriteException(e.Exception, makeVMDump: false, MSGBox: true, exitapp:true);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logging.WriteException(ex: e.ExceptionObject as Exception, makeVMDump: false, MSGBox: true, exitapp: true);
        }
    }
}
