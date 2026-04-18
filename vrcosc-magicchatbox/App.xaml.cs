using MagicChatboxAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Dialogs;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox
{
    /// <summary>
    /// Application entry point. Bootstraps the DI container, runs startup migration, and
    /// shows the main window after all services and modules are initialized.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// DI service provider — use App.Services to resolve dependencies.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        private static readonly Lazy<AppSettings> _lazyAppSettings = new(() =>
            Services.GetRequiredService<ISettingsProvider<AppSettings>>().Value);
        private static AppSettings _appSettings => _lazyAppSettings.Value;

        private static readonly Lazy<IntegrationSettings> _lazyIntgr = new(() =>
            Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>().Value);
        private static IntegrationSettings _integrationSettings => _lazyIntgr.Value;

        private static readonly Lazy<WeatherSettings> _lazyWeatherSettings = new(() =>
            Services.GetRequiredService<ISettingsProvider<WeatherSettings>>().Value);
        private static WeatherSettings _weatherSettings => _lazyWeatherSettings.Value;

        public static IMediaLinkService ApplicationMediaController { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Services = ServiceRegistration.ConfigureServices();

            // Initialize static Logging with DI services (eliminates service locator in Logging.cs)
            Logging.Initialize(
                Services.GetRequiredService<AppUpdateState>(),
                Services.GetRequiredService<IEnvironmentService>(),
                Services.GetRequiredService<IHttpClientFactory>(),
                Services.GetRequiredService<IUiDispatcher>(),
                Services.GetRequiredService<IVersionService>(),
                Services.GetRequiredService<INavigationService>());

            // Run legacy XML→JSON migration BEFORE any settings provider is resolved from DI.
            // JsonSettingsProvider<T> loads from disk in its constructor — migration must write
            // files first so providers pick up the migrated values immediately.
            {
                var env = Services.GetRequiredService<IEnvironmentService>();
                SettingsMigrationService.RunAll(env.DataPath);
            }

            // Initialize static defaults for model classes (eliminates service locator in models)
            ChatItem.DefaultChatStatus = Services.GetRequiredService<ChatStatusDisplayState>();
            TrackerDevice.DefaultTrackerSettings = Services.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>().Value;

            var vm = Services.GetRequiredService<ViewModel>();

            var bootstrapper = Services.GetRequiredService<ModuleBootstrapper>();
            bootstrapper.RegisterComponentStats(Services.GetRequiredService<ComponentStatsModule>());

            await Task.Run(() =>
            {
                var env = Services.GetRequiredService<IEnvironmentService>();
                var appHistorySvc = Services.GetRequiredService<IAppHistoryService>();
                if (appHistorySvc.CreateIfMissing(env.DataPath))
                    Logging.WriteInfo("Application started at: " + DateTime.Now);
            });

            StartUp loadingWindow = new StartUp();
            loadingWindow.Show();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            UpdateApp updater = new UpdateApp(
                Services.GetRequiredService<AppUpdateState>(),
                Services.GetRequiredService<IHttpClientFactory>(),
                Services.GetRequiredService<IUiDispatcher>());

            if (e.Args != null && e.Args.Length > 0)
            {
                foreach (string arg in e.Args)
                {
                    if (arg.StartsWith("-profile="))
                    {
                        string profileNumberString = arg.Substring(9);
                        if (int.TryParse(profileNumberString, out int profileNumber))
                        {
                            _appSettings.ProfileNumber = profileNumber;
                            _appSettings.UseCustomProfile = true;
                            var env = Services.GetRequiredService<IEnvironmentService>();
                            env.SetCustomProfile(profileNumber);
                        }
                        else
                        {
                            loadingWindow.Hide();
                            Logging.WriteException(new Exception($"Invalid profile number '{profileNumberString}'"), MSGBox: true, exitapp: true);
                            return;
                        }
                    }
                    else
                    {
                        switch (arg)
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
                                Logging.WriteException(new Exception($"Invalid command line argument '{arg}'"), MSGBox: true, exitapp: true);
                                return;
                        }
                    }
                }
            }

            // Show TOS + Privacy wizard if TOS version has changed or was never accepted
            bool tosJustAccepted = false;
            {
                var appSettingsProvider = Services.GetRequiredService<ISettingsProvider<AppSettings>>();
                var consentService = Services.GetRequiredService<IPrivacyConsentService>();
                if (appSettingsProvider.Value.AcceptedTosVersion != Core.Constants.TosVersion)
                {
                    var wizard = new TosAndPrivacyWizard(consentService, appSettingsProvider)
                    {
                        Owner = loadingWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    tosJustAccepted = wizard.ShowDialog() == true;
                    // If wizard was dismissed, fall through to per-hook consent dialog below
                }
            }

            // Show privacy consent dialog for any hooks that have Unknown state
            {
                var consentService = Services.GetRequiredService<IPrivacyConsentService>();
                var allHooks = System.Enum.GetValues<PrivacyHook>();
                var pendingHooks = consentService.GetHooksRequiringConsent(allHooks);
                if (pendingHooks.Count > 0)
                {
                    var dialog = new PrivacyConsentDialog(consentService, pendingHooks)
                    {
                        Owner = loadingWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    dialog.ShowDialog();
                }
            }

            await InitializeComponentsWithProgress(loadingWindow);

            MainWindow mainWindow = new MainWindow(
                Services.GetRequiredService<ScanLoopService>(),
                Services.GetRequiredService<ModuleBootstrapper>(),
                Services.GetRequiredService<Core.Services.IModuleHost>(),
                Services.GetRequiredService<IStatePersistenceCoordinator>());
            mainWindow.DataContext = vm;

            // Initialize the main window now that DataContext (VM) has been assigned.
            // InitializeAsync depends on VM and UI elements and must run after DataContext is set.
            await mainWindow.InitializeAsync();

            loadingWindow.UpdateProgress("Setting up the hotkeys... Hotkey, hotkey, hotkey!", 97);
            Services.GetRequiredService<HotkeyManagement>().Initialize(mainWindow);
            mainWindow.Show();

            // Wire toast notifications for runtime consent changes (subscribed AFTER mainWindow.Show so
            // wizard-time consent saves don't fire premature toasts on a hidden window).
            var toastSvc = Services.GetRequiredService<IToastService>();
            var consentSvc = Services.GetRequiredService<IPrivacyConsentService>();
            consentSvc.ConsentChanged += (_, args) =>
            {
                var (name, icon) = PrivacyHookInfo.Get(args.Hook);
                switch (args.NewState)
                {
                    case ConsentState.Approved:
                        toastSvc.Show($"{icon} Permission Enabled", $"{name} is now active.", ToastType.Privacy,
                            key: $"consent-change-{args.Hook}");
                        break;
                    case ConsentState.Denied:
                        toastSvc.Show("🚫 Permission Revoked", $"{name} has been disabled.", ToastType.Warning,
                            key: $"consent-change-{args.Hook}");
                        break;
                }
            };

            if (tosJustAccepted)
                toastSvc.Show(
                    "Welcome to MagicChatbox! 🎉",
                    "Your permissions are saved. Adjust them anytime in Options → Privacy & Permissions.",
                    ToastType.Success,
                    durationMs: 7000);

            InitializeUserMonitoring();

            loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 100);
            loadingWindow.Close();

        }



        private void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {

            if (e.Exception.Message.Contains("The process cannot access the file"))
            {
                return;
            }
            Logging.WriteInfo(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logging.WriteException(e.Exception, MSGBox: true, exitapp: true);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logging.WriteException(ex: e.ExceptionObject as Exception, MSGBox: true, exitapp: true, log: false);
        }



        private void InitializeUserMonitoring()
        {
            var allowedService = Services.GetRequiredService<IAllowedForUsingService>();
            allowedService.BanDetected += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Services.GetRequiredService<IBanEnforcementService>().ProcessBan(args.UserId, args.Reason);
                });

            };
            allowedService.StartUserMonitoring(Core.Constants.AutoUpdateCheckInterval);
        }


        private async Task InitializeComponentsWithProgress(StartUp loadingWindow)
        {
            var vm = Services.GetRequiredService<ViewModel>();

            loadingWindow.UpdateProgress("Rousing the logging module... It's coffee time, logs!", 7);
            await Task.Run(() => LogManager.LoadConfiguration("NLog.config"));

            loadingWindow.UpdateProgress("Migrating settings to the new world order...", 15);
            await Task.Run(() =>
            {
                var env = Services.GetRequiredService<IEnvironmentService>();
                SettingsMigrationService.RunAll(env.DataPath);
            });

            Services.GetRequiredService<WeatherOverrideState>().Initialize(_weatherSettings);

            loadingWindow.UpdateProgress("Restoring your saved settings...", 20);
            await Task.Run(() =>
            {
                // Sync persisted JSON settings → runtime display states
                var intSettings = Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>().Value;
                Services.GetRequiredService<IntegrationDisplayState>().IntegrationSortOrder = intSettings.SavedSortOrder;

                var trackerSettings = Services.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>().Value;
                Services.GetRequiredService<TrackerDisplayState>().TrackerDevices = trackerSettings.SavedDevices;
            });

            loadingWindow.UpdateProgress("Gathering status items like a squirrel with nuts!", 30);
            await Task.Run(() => Services.GetRequiredService<IStatusListService>().LoadStatusList());

            loadingWindow.UpdateProgress("Detective on the hunt for last session's chat messages... Elementary, my dear Watson!", 40);
            await Task.Run(() => Services.GetRequiredService<IChatHistoryService>().LoadChatHistory());

            loadingWindow.UpdateProgress("Going on a treasure hunt for MediaLink settings... Ahoy, Captain!", 50);
            await Task.Run(() => Services.GetRequiredService<IMediaLinkPersistenceService>().LoadMediaSessions());

            loadingWindow.UpdateProgress("Selecting recent apps for window integration, like picking the A-Team!", 60);
            await Task.Run(() => Services.GetRequiredService<IAppHistoryService>().LoadAppHistory());

            if (_integrationSettings.IntgrComponentStats)
            {
                loadingWindow.UpdateProgress("Lighting up ComponentStats like it's the 4th of July. Ka-boom!", 65);
                await Task.Run(() => App.Services.GetRequiredService<ComponentStatsModule>().StartModule());
            }

            loadingWindow.UpdateProgress("Initializing Network Statistics Module", 66);
            var netStats = await Task.Run(() => App.Services.GetRequiredService<NetworkStatisticsModule>());
            App.Services.GetRequiredService<IModuleHost>().RegisterModule(netStats);

            loadingWindow.UpdateProgress("Initializing OpenAI like a rocket launch. 3... 2... 1... Blast off!", 70);
            var openAIModule = App.Services.GetRequiredService<OpenAIModule>();
            var openAISettings = App.Services.GetRequiredService<ISettingsProvider<OpenAISettings>>().Value;
            await Task.Run(() => openAIModule.InitializeClient(openAISettings.AccessToken, openAISettings.OrganizationID));

            loadingWindow.UpdateProgress("Initializing IntelliSense like a psychic. What's on your mind?", 72);
            var bootMods = Services.GetRequiredService<ModuleBootstrapper>();
            await Task.Run(() => bootMods.CreateIntelliChat());

            loadingWindow.UpdateProgress("Warming up the TTS voices. Ready for the vocal Olympics!", 75);
            vm.TtsAudio.TikTokTTSVoices = await Task.Run(() => Services.GetRequiredService<IAudioService>().ReadTikTokTTSVoices());

            loadingWindow.UpdateProgress("Selecting your audio devices like a DJ choosing beats. Drop the bass!", 80);
            await Task.Run(() => Services.GetRequiredService<IAudioService>().PopulateOutputDevices());

            loadingWindow.UpdateProgress("Turbocharging MediaLink engines... Fast & Furious: Data Drift!", 95);
            ApplicationMediaController = new MediaLinkModule(
                _integrationSettings.IntgrScanMediaLink,
                Services.GetRequiredService<IPrivacyConsentService>(),
                Services.GetRequiredService<IAppState>(),
                Services.GetRequiredService<MediaLinkDisplayState>(),
                Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
                Services.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
                Services.GetRequiredService<IUiDispatcher>());

            loadingWindow.UpdateProgress("Starting the modules... Ready, set, go!", 96);
            await Task.Run(() => bootMods.CreateRuntimeModules());
            // Modules are now accessed via VM.Modules (IModuleHost) — no need to copy refs

            loadingWindow.UpdateProgress("Loading MediaLink styles... Fashion show, here we come!", 98);
            await Task.Run(() => Services.GetRequiredService<IMediaLinkPersistenceService>().LoadSeekbarStyles());
        }
    }
}


