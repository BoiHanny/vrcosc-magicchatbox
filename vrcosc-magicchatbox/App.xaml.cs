using NLog;
using System;
using System.Runtime.ExceptionServices;
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
        // Singleton instance for managing media links within the application
        public static MediaLinkModule ApplicationMediaController { get; private set; }

        // Main entry point for the application
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show the startup/loading window
            StartUp loadingWindow = new StartUp();
            loadingWindow.Show();

            // Set up global exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            // Initialize the update application logic
            UpdateApp updater = new UpdateApp();

            // Process command-line arguments
            if (e.Args != null && e.Args.Length > 0)
            {
                switch (e.Args[0])
                {
                    case "-update":
                        loadingWindow.UpdateProgress("Go, go, go! Update, update, update!", 75);
                        await Task.Run(() => updater.UpdateApplication());
                        Shutdown();
                        return;
                    case "-updateadmin":
                        loadingWindow.UpdateProgress("Admin style update, now that's fancy!", 85);
                        await Task.Run(() => updater.UpdateApplication(true));
                        Shutdown();
                        return;
                    case "-rollback":
                        loadingWindow.UpdateProgress("Oops! Let's roll back.", 50);
                        await Task.Run(() => updater.RollbackApplication(loadingWindow));
                        Shutdown();
                        return;
                    case "-clearbackup":
                        loadingWindow.UpdateProgress("Rolling back and clearing the slate. Fresh start!", 50);
                        await Task.Run(() => updater.ClearBackUp());
                        break;
                    default:
                        loadingWindow.Hide();
                        Logging.WriteException(new Exception($"Invalid command line argument '{e.Args[0]}'"), MSGBox: true, exitapp: true);
                        return;
                }
            }

            // Initialize various components with progress updates
            await InitializeComponentsWithProgress(loadingWindow);

            // Show the main window
            MainWindow mainWindow = new MainWindow();
            mainWindow.DataContext = ViewModel.Instance;

            loadingWindow.UpdateProgress("Setting up the hotkeys... Hotkey, hotkey, hotkey!", 97);
            HotkeyManagement.Instance.Initialize(mainWindow);
            mainWindow.Show();

            // Close the loading window once initialization is complete
            loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 100);
            loadingWindow.Close();
        }

        // Handle first-chance exceptions (before they are thrown)
        private void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {

            Logging.WriteInfo(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);
        }

        // Handle unhandled exceptions in the application's dispatcher thread
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logging.WriteException(e.Exception, MSGBox: true, exitapp: true);
        }

        // Handle unhandled exceptions in the application domain
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logging.WriteException(ex: e.ExceptionObject as Exception, MSGBox: true, exitapp: true, log: false);
        }

        // Helper method to initialize components with progress updates
        private async Task InitializeComponentsWithProgress(StartUp loadingWindow)
        {
            loadingWindow.UpdateProgress("Making sure log folder exists... hell yeah, logs!", 5);
            await Task.Run(() => DataController.CheckLogFolder());

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

            loadingWindow.UpdateProgress("Initializing Network Statistics Module", 66);
            await Task.Run(() => DataController.networkStatisticsModule = new NetworkStatisticsModule(1000));

            loadingWindow.UpdateProgress("Initializing OpenAI like a rocket launch. 3... 2... 1... Blast off!", 70);
            await Task.Run(() => OpenAIModule.Instance.InitializeClient(ViewModel.Instance.OpenAIAccessToken, ViewModel.Instance.OpenAIOrganizationID));

            loadingWindow.UpdateProgress("Initializing IntelliSense like a psychic. What's on your mind?", 72);
            await Task.Run(() => ViewModel.Instance.IntelliChatModule = new IntelliChatModule());

            loadingWindow.UpdateProgress("Warming up the TTS voices. Ready for the vocal Olympics!", 75);
            ViewModel.Instance.TikTokTTSVoices = await Task.Run(() => DataController.ReadTkTkTTSVoices());

            loadingWindow.UpdateProgress("Selecting your audio devices like a DJ choosing beats. Drop the bass!", 80);
            await Task.Run(() => DataController.PopulateOutputDevices());

            loadingWindow.UpdateProgress("Turbocharging MediaLink engines... Fast & Furious: Data Drift!", 95);
            ApplicationMediaController = new MediaLinkModule(ViewModel.Instance.IntgrScanMediaLink);

            loadingWindow.UpdateProgress("Loading MediaLink styles... Fashion show, here we come!", 96);
            await Task.Run(() => DataController.LoadAndSaveMediaLinkStyles());

            loadingWindow.UpdateProgress("Initializing Soundpad Module... Let's get this party started!", 96);
            await Task.Run(() => DataController.soundpadModule = new SoundpadModule(1500));
        }
    }
}
