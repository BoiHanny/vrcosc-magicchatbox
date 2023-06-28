using NLog;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox
{
    public partial class App : Application
    {
        public MediaLinkController MediaController { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var loadingWindow = new StartUp();
            loadingWindow.Show();

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


            loadingWindow.UpdateProgress("Waking up the logging module... Rise and shine!", 10);
            await Task.Run(() => LogManager.LoadConfiguration("NLog.config"));

            loadingWindow.UpdateProgress("Digging through your old settings... Let's hope there's no dust!", 15);
            await Task.Run(() => DataController.ManageSettingsXML());

            loadingWindow.UpdateProgress("Collecting all those juicy status items!", 20);
            await Task.Run(() => DataController.LoadStatusList());

            loadingWindow.UpdateProgress("Hunting down the last session's chat messages... Gotcha!", 25);
            await Task.Run(() => DataController.LoadChatList());

            loadingWindow.UpdateProgress("Seaking hard to locate the last MediaLink's settings... WHOAAH!", 30);
            await Task.Run(() => DataController.LoadMediaSessions());

            loadingWindow.UpdateProgress("Picking out your recent apps for the window integration!", 35);
            await Task.Run(() => DataController.LoadAppList());

            loadingWindow.UpdateProgress("Tuning up the TTS voices. Get ready to hear!", 40);
            ViewModel.Instance.TikTokTTSVoices = await Task.Run(() => DataController.ReadTkTkTTSVoices());

            loadingWindow.UpdateProgress("Warming up the OpenAI client... Get set for takeoff!", 50);
            await Task.Run(() => OpenAIClient.LoadOpenAIClient());

            loadingWindow.UpdateProgress("Revving up the OpenAI engines... Can you hear the roar?", 60);
            await Task.Run(() => DataController.LoadIntelliChatBuiltInActions());

            loadingWindow.UpdateProgress("Setting up your concert - choosing the best audio devices!", 70);
            await Task.Run(() => DataController.PopulateOutputDevices());

            loadingWindow.UpdateProgress("Dialing GitHub... Looking for shiny new updates!", 80);
            await Task.Run(() => DataController.CheckForUpdateAndWait());

            if (ViewModel.Instance.IntgrScanMediaLink)
                loadingWindow.UpdateProgress("Revving up the MediaLink engines... Ready for some action!", 90);
            //await Task.Run(() => MediaLinkController.Start());
            MediaController = new MediaLinkController(ViewModel.Instance.IntgrScanMediaLink);

            loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 100);

            MainWindow mainWindow = new MainWindow();
            mainWindow.DataContext = ViewModel.Instance;
            mainWindow.Show();

            loadingWindow.Close();


        }
    }
}
