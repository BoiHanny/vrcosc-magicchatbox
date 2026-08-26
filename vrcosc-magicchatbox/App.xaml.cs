using MagicChatboxAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
    public partial class App : Application
    {
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

        // Assigned during InitializeComponentsWithProgress, before any consumer can run.
        public static IMediaLinkService ApplicationMediaController { get; private set; } = null!;

        private readonly Stopwatch _startupStopwatch = new();
        private static readonly TimeSpan StartupWatchdogTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan RequiredStartupTaskTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan OptionalStartupTaskTimeout = TimeSpan.FromSeconds(10);
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        private bool _loggingReady;
        private volatile bool _startupCompleted;
        private volatile bool _interactiveStartupPhase;
        private long _watchdogBaselineTicks = Environment.TickCount64;
        private string _lastStartupPhase = "Process created.";

        private static readonly TimeSpan HandledDispatcherExceptionWindow = TimeSpan.FromSeconds(60);
        private const int MaxHandledDispatcherExceptionsInWindow = 3;
        private readonly System.Collections.Generic.Queue<DateTime> _handledDispatcherExceptionTimes = new();

        public const string SteamVrLaunchArgument = "-steamvr";

        public static MainWindow? mainWindow;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Before any element exists: property metadata cannot be overridden once a type is in use.
            UI.Controls.ReducedVisuals.Install();

            _startupStopwatch.Start();
            LogStartupPhase($"Process started. PID={Environment.ProcessId}, Args='{string.Join(" ", e.Args ?? Array.Empty<string>())}'.");

            bool launchedBySteamVr = WasLaunchedBySteamVr(e.Args);

            if (!TryGetProfileNumberFromArgs(e.Args, out int startupProfileNumber, out string? invalidProfileNumber))
            {
                MessageBox.Show(
                    $"Invalid profile number '{invalidProfileNumber}'.",
                    "MagicChatbox",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (!ShouldSkipSingleInstanceGuard(e.Args) && !TryAcquireSingleInstance(startupProfileNumber))
            {
                // SteamVR launches its startup apps without checking whether they are already
                // there. Pulling a window to the front while someone is putting a headset on is
                // the last thing they want, so this launch simply stands down.
                if (launchedBySteamVr)
                {
                    LogStartupPhase("SteamVR started a copy that was already running. Leaving the running one alone.");
                    Shutdown();
                    return;
                }

                // Launching it again is how someone asks for the window back, not a mistake to be
                // told off for. Wake the copy that already exists and leave quietly.
                LogStartupPhase($"Second instance detected for profile {startupProfileNumber}. Waking the running one and exiting.");
                if (!TrySignalRunningInstance(startupProfileNumber))
                {
                    MessageBox.Show(
                        "MagicChatbox is already running for this profile, but it could not be brought to the front.",
                        "MagicChatbox",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                Shutdown();
                return;
            }

            StartListeningForActivationRequests(startupProfileNumber);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#if DEBUG
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
#endif

            var startupCancellation = new CancellationTokenSource();
            Task startupWatchdogTask = RunStartupWatchdogAsync(startupCancellation.Token);

            StartUp? loadingWindow = null;
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                loadingWindow = StartUp.CreateOnOwnThread(startupCancellation.Cancel);
                loadingWindow.UpdateProgress("Opening startup window...", 1, "Configuring services...");
                await PumpStartupUiAsync();
                LogStartupPhase("Splash window shown.");
                startupCancellation.Token.ThrowIfCancellationRequested();
                await UpdateStartupProgressAsync(loadingWindow, "Configuring services...", 3, "Warming up logging...");
                LogStartupPhase("Configuring services...");
                Services = await RunRequiredStartupTaskAsync(
                    "service configuration",
                    () => ServiceRegistration.ConfigureServices(startupProfileNumber),
                    startupCancellation.Token);
                LogStartupPhase("Services configured.");
                if (startupProfileNumber > 0)
                {
                    LogStartupPhase($"Custom profile path selected for profile {startupProfileNumber}.");
                }

                await UpdateStartupProgressAsync(loadingWindow, "Warming up logging...", 7, "Migrating settings...");
                ConfigureLogging(Services.GetRequiredService<IEnvironmentService>());
                _loggingReady = true;
                LogStartupPhase("Logging configured.");

                if (Core.Diagnostics.PerfProbe.IsEnabled)
                {
                    Core.Diagnostics.PerfProbe.ReportDirectory =
                        Services.GetRequiredService<IEnvironmentService>().LogPath;
                    Core.Diagnostics.BindingErrorProbe.Start();
                    Logging.WriteInfo("[Perf] Instrumentation enabled (--perf). Ctrl+Shift+F12 dumps a snapshot.");
                }

                Logging.Initialize(
                    Services.GetRequiredService<AppUpdateState>(),
                    Services.GetRequiredService<IEnvironmentService>(),
                    Services.GetRequiredService<IHttpClientFactory>(),
                    Services.GetRequiredService<IUiDispatcher>(),
                    Services.GetRequiredService<IVersionService>(),
                    Services.GetRequiredService<INavigationService>());
                LogStartupPhase("Static logging dependencies initialized.");

                await UpdateStartupProgressAsync(loadingWindow, "Migrating settings...", 12, "Preparing core state...");
                await RunRequiredStartupTaskAsync("settings migration", () =>
                {
                    var env = Services.GetRequiredService<IEnvironmentService>();
                    LogStartupPhase($"Running settings migration. DataPath='{env.DataPath}', LogPath='{env.LogPath}'.");
                    SettingsMigrationService.RunAll(env.DataPath);
                    return true;
                }, startupCancellation.Token);
                LogStartupPhase("Settings migration completed.");
                if (startupProfileNumber > 0)
                {
                    _appSettings.ProfileNumber = startupProfileNumber;
                    _appSettings.UseCustomProfile = true;
                }

                await UpdateStartupProgressAsync(loadingWindow, "Preparing core state...", 18, "Loading your saved data...");

                var vm = await RunRequiredStartupTaskAsync("core state preparation", () =>
                {
                    ChatItem.DefaultChatStatus = Services.GetRequiredService<ChatStatusDisplayState>();
                    TrackerDevice.DefaultTrackerSettings = Services.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>().Value;
                    LogStartupPhase("Model defaults initialized.");

                    var rootViewModel = Services.GetRequiredService<ViewModel>();
                    LogStartupPhase("ViewModel resolved.");
                    return rootViewModel;
                }, startupCancellation.Token);

                await RunOptionalStartupTaskAsync("app history start marker", () =>
                {
                    var env = Services.GetRequiredService<IEnvironmentService>();
                    var appHistorySvc = Services.GetRequiredService<IAppHistoryService>();
                    if (appHistorySvc.CreateIfMissing(env.DataPath))
                        Logging.WriteInfo("Application started at: " + DateTime.Now);
                }, startupCancellation.Token);

                startupCancellation.Token.ThrowIfCancellationRequested();

                UpdateApp updater = new UpdateApp(
                    Services.GetRequiredService<AppUpdateState>(),
                    Services.GetRequiredService<IHttpClientFactory>(),
                    Services.GetRequiredService<IUiDispatcher>());

                if (e.Args != null && e.Args.Length > 0)
                {
                    foreach (string arg in e.Args)
                    {
                        if (arg.StartsWith("-profile=", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (Core.Diagnostics.PerfProbe.IsEnableArgument(arg))
                        {
                            continue;
                        }
                        else
                        {
                            switch (arg)
                            {
                                case "-update":
                                    ShowHandoffSteps(loadingWindow, updater);
                                    loadingWindow.UpdateProgress("Installing the new version", 75);
                                    await Task.Run(() => updater.UpdateApplication());
                                    loadingWindow.MarkInstallComplete();
                                    loadingWindow.CloseFromAnyThread();
                                    Shutdown();
                                    return;
                                case "-updateadmin":
                                    ShowHandoffSteps(loadingWindow, updater);
                                    loadingWindow.UpdateProgress("Installing the new version as administrator", 85);
                                    await Task.Run(() => updater.UpdateApplication(true));
                                    loadingWindow.MarkInstallComplete();
                                    loadingWindow.CloseFromAnyThread();
                                    Shutdown();
                                    return;
                                case "-rollback":
                                    ShowHandoffSteps(loadingWindow, updater);
                                    loadingWindow.UpdateProgress("Going back to your previous version", 50);
                                    await Task.Run(() => updater.RollbackApplication(loadingWindow));
                                    loadingWindow.MarkInstallComplete();
                                    loadingWindow.CloseFromAnyThread();
                                    Shutdown();
                                    return;
                                case "-rollbackadmin":
                                    ShowHandoffSteps(loadingWindow, updater);
                                    loadingWindow.UpdateProgress("Going back to your previous version as administrator", 55);
                                    await Task.Run(() => updater.RollbackApplication(loadingWindow, true));
                                    loadingWindow.MarkInstallComplete();
                                    loadingWindow.CloseFromAnyThread();
                                    Shutdown();
                                    return;
                                case "-clearbackup":
                                    loadingWindow.UpdateProgress("Rolling back and clearing the slate. Fresh start!", 50);
                                    await Task.Run(() => updater.ClearBackUp());
                                    break;
                                case SteamVrLaunchArgument:
                                    break;
                                default:
                                    loadingWindow.CloseFromAnyThread();
                                    LogStartupPhase($"Invalid command line argument '{arg}'.");
                                    Logging.WriteException(new Exception($"Invalid command line argument '{arg}'"), MSGBox: true, exitapp: true);
                                    return;
                            }
                        }
                    }
                }

                if (Services.GetRequiredService<IAutoUpdateService>()
                        .PrepareForStartup(launchedBySteamVr) == StartupUpdateOutcome.HandingOff)
                {
                    LogStartupPhase("Handing off to the updater; this process is on its way out.");
                    loadingWindow.CloseFromAnyThread();
                    return;
                }

                bool tosJustAccepted = false;
                _interactiveStartupPhase = true;
                {
                    LogStartupPhase("Checking TOS/privacy wizard state.");
                    var appSettingsProvider = Services.GetRequiredService<ISettingsProvider<AppSettings>>();
                    var consentService = Services.GetRequiredService<IPrivacyConsentService>();
                    if (appSettingsProvider.Value.AcceptedTosVersion != Core.Constants.TosVersion)
                    {
                        var wizard = new TosAndPrivacyWizard(consentService, appSettingsProvider);
                        DialogWindowHelper.PrepareModal(wizard);
                        wizard.Topmost = true;
                        tosJustAccepted = wizard.ShowDialog() == true;
                    }
                }

                {
                    LogStartupPhase("Checking pending privacy hooks.");
                    var consentService = Services.GetRequiredService<IPrivacyConsentService>();
                    var allHooks = System.Enum.GetValues<PrivacyHook>();
                    var pendingHooks = consentService.GetHooksRequiringConsent(allHooks);
                    if (pendingHooks.Count > 0)
                    {
                        var dialog = new PrivacyConsentDialog(consentService, pendingHooks);
                        DialogWindowHelper.PrepareModal(dialog);
                        dialog.Topmost = true;
                        dialog.ShowDialog();
                    }
                }
                _interactiveStartupPhase = false;
                Interlocked.Exchange(ref _watchdogBaselineTicks, Environment.TickCount64);

                await InitializeComponentsWithProgress(loadingWindow, startupCancellation.Token);
                LogStartupPhase("Component initialization completed.");
                startupCancellation.Token.ThrowIfCancellationRequested();

                loadingWindow.UpdateProgress("Building the main window shell... Hammer, nails, UI!", 98.5, "Rolling out the red carpet... Here comes the UI!");
                Logging.WriteInfo("Creating MainWindow instance.");

                MainWindow mainWindow = new MainWindow(
                    Services.GetRequiredService<ScanLoopService>(),
                    Services.GetRequiredService<ModuleBootstrapper>(),
                    Services.GetRequiredService<Core.Services.IModuleHost>(),
                    Services.GetRequiredService<IStatePersistenceCoordinator>(),
                    Services.GetRequiredService<ITrayIconService>(),
                    Services.GetRequiredService<HotkeyManagement>(),
                    Services.GetRequiredService<Core.Configuration.ISettingsProvider<Classes.Modules.AppSettings>>());
                App.mainWindow = mainWindow;
                Logging.WriteInfo("MainWindow instance created.");

                loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 99, "Wiring up the final UI bits... Almost there!");
                loadingWindow.SetTopmostFromAnyThread(true);

                Logging.WriteInfo("[Startup] Showing MainWindow (empty shell)...");
                mainWindow.PrepareHiddenStart();
                mainWindow.Show();
                Logging.WriteInfo("[Startup] MainWindow shown.");
                ShutdownMode = ShutdownMode.OnLastWindowClose;

                mainWindow.UpdateOverlayProgress("Connecting data bindings...", 30, "Wiring up modules...");

                Logging.WriteInfo("[Startup] Assigning DataContext...");
                mainWindow.DataContext = vm;
                Logging.WriteInfo("[Startup] DataContext assigned.");

                mainWindow.UpdateOverlayProgress("Wiring up modules...", 55, "Initializing components...");

                loadingWindow.UpdateProgress("Wiring up the final UI bits... Almost there!", 100);
                await mainWindow.InitializeAsync();
                Logging.WriteInfo("MainWindow.InitializeAsync completed.");

                mainWindow.UpdateOverlayProgress("Initializing components...", 75, "Registering hotkeys...");

                Logging.WriteInfo("[Startup] Registering hotkeys...");
                Services.GetRequiredService<HotkeyManagement>().Initialize(mainWindow);
                Logging.WriteInfo("[Startup] Hotkeys registered.");

                mainWindow.UpdateOverlayProgress("Registering hotkeys...", 85, "Rendering interface...");

                mainWindow.UpdateOverlayProgress("Rendering interface...", 95, "Restoring open page...");

                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                Services.GetRequiredService<ITrayIconService>().Initialize(mainWindow);

                // Being started by SteamVR means a headset is going on, not that someone wants a
                // window. This never writes the preference back; it only applies to this launch.
                if (vm.AppSettingsInstance.StartInBackground || launchedBySteamVr)
                {
                    mainWindow.AbandonHiddenStart();
                    mainWindow.HideStartupOverlay(animate: false);
                    mainWindow.Hide();
                    loadingWindow.CloseFromAnyThread();
                    Logging.WriteInfo("[Startup] Splash closed; started in the background.");
                }
                else
                {
                    mainWindow.FadeInAfterStartup(() =>
                    {
                        loadingWindow.CloseFromAnyThread();
                        Logging.WriteInfo("[Startup] Splash closed.");
                        mainWindow.HideStartupOverlay();

                        mainWindow.Activate();
                        mainWindow.Focus();
                    });
                }

                Services.GetRequiredService<ModuleBootstrapper>().SignalStartupComplete();
                Logging.WriteInfo("[Startup] Startup-complete signal fired.");

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

                Logging.WriteInfo("[Startup] Initializing user monitoring...");
                if (consentSvc.IsApproved(PrivacyHook.InternetAccess))
                {
                    InitializeUserMonitoring();
                }
                consentSvc.ConsentChanged += (_, args) =>
                {
                    if (args.Hook == PrivacyHook.InternetAccess && args.NewState == ConsentState.Approved)
                        InitializeUserMonitoring();
                };
                Logging.WriteInfo("[Startup] User monitoring initialized.");

                Logging.WriteInfo("[Startup] Starting background scan loop...");
                mainWindow.StartBackgroundProcessing();
                Logging.WriteInfo("[Startup] Background processing started.");

                Services.GetRequiredService<IAutoUpdateService>().ReportStartupHealthy();
                LogStartupPhase("Startup reached a working state.");

                Services.GetRequiredService<Services.Vr.ISteamVrAutoStartService>().Start();

                if (vm.AppSettingsInstance.CheckUpdateOnStartup && consentSvc.IsApproved(PrivacyHook.InternetAccess))
                {
                    _ = RunDeferredStartupUpdateCheckAsync();
                }
            }
            catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
            {
                LogStartupPhase("Startup cancelled by user.");
                try
                {
                    loadingWindow?.CloseFromAnyThread();
                }
                catch { }

                Shutdown();
            }
            catch (Exception ex)
            {
                LogStartupPhase($"Startup failed: {ex}");
                try
                {
                    Logging.WriteException(ex, MSGBox: false);
                }
                catch { }

                try
                {
                    loadingWindow?.CloseFromAnyThread();
                }
                catch { }

                MessageBox.Show(
                    $"MagicChatbox failed to start:\n\n{ex.Message}\n\nPlease report this error.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
            finally
            {
                _startupCompleted = true;
                startupCancellation.Cancel();
                try
                {
                    await startupWatchdogTask;
                }
                catch (Exception ex)
                {
                    WriteEarlyStartupLog("Startup watchdog failed during shutdown: " + ex);
                }
                finally
                {
                    startupCancellation.Dispose();
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Services?.GetService<DiscordRichPresenceService>()?.Dispose();
                Services?.GetService<ITrayIconService>()?.Dispose();
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog("Explicit service dispose failed: " + ex);
            }

            try
            {
                if (Services is IDisposable disposableServices)
                    disposableServices.Dispose();
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog("Service provider dispose failed: " + ex);
            }

            if (_ownsSingleInstanceMutex)
            {
                try
                {
                    _singleInstanceMutex?.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }
            }

            try
            {
                _activationListenerCancellation?.Cancel();
                _activationListenerCancellation?.Dispose();
                _activationSignal?.Dispose();
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog($"Could not stop the activation listener: {ex}");
            }

            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        private async Task RunDeferredStartupUpdateCheckAsync()
        {
            try
            {
                if (Dispatcher.HasShutdownStarted)
                    return;

                await Task.Delay(1200);

                if (Dispatcher.HasShutdownStarted)
                    return;

                await Services.GetRequiredService<IVersionService>().CheckForUpdateAndWait();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        private async Task UpdateStartupProgressAsync(StartUp loadingWindow, string message, double value, string? nextHint = null)
        {
            loadingWindow.UpdateProgress(message, value, nextHint);
            await PumpStartupUiAsync();
        }

        private async Task PumpStartupUiAsync()
        {
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
        }

        private async Task<T> RunRequiredStartupTaskAsync<T>(
            string taskName,
            Func<T> action,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan effectiveTimeout = timeout ?? RequiredStartupTaskTimeout;
            Task<T> task = Task.Run(action, cancellationToken);
            Task delay = Task.Delay(effectiveTimeout, cancellationToken);

            Task completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException(
                    $"Startup step '{taskName}' timed out after {effectiveTimeout.TotalSeconds:0}s. Last phase: {_lastStartupPhase}");
            }

            return await task.ConfigureAwait(false);
        }

        private async Task RunStartupWatchdogAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    if (_startupCompleted || cancellationToken.IsCancellationRequested)
                        return;

                    if (_interactiveStartupPhase)
                    {
                        Interlocked.Exchange(ref _watchdogBaselineTicks, Environment.TickCount64);
                        continue;
                    }

                    long elapsed = Environment.TickCount64 - Interlocked.Read(ref _watchdogBaselineTicks);
                    if (elapsed < (long)StartupWatchdogTimeout.TotalMilliseconds)
                        continue;

                    string message = $"Startup watchdog timed out after {StartupWatchdogTimeout.TotalSeconds:0}s. Last phase: {_lastStartupPhase}";
                    WriteEarlyStartupLog(message);
                    try
                    {
                        Logging.WriteInfo(message);
                    }
                    catch
                    {
                    }

                    await Task.Yield();
                    if (_startupCompleted || cancellationToken.IsCancellationRequested)
                        return;
                    if (_interactiveStartupPhase)
                        continue;

                    Environment.Exit(1);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void ConfigureLogging(IEnvironmentService env)
        {
            Directory.CreateDirectory(env.LogPath);

            InternalLogger.LogLevel = LogLevel.Warn;
            InternalLogger.LogFile = Path.Combine(env.LogPath, $"internal-nlog-{Environment.ProcessId}.txt");

            string nlogConfigPath = Path.Combine(AppContext.BaseDirectory, "NLog.config");
            if (File.Exists(nlogConfigPath))
            {
                LogManager.Setup().LoadConfigurationFromFile(nlogConfigPath);
            }

            try
            {
                var logger = LogManager.GetCurrentClassLogger();
                Logging.SetLoggerInstance(logger);
            }
            catch
            {
            }
        }

        private void ShowHandoffSteps(StartUp loadingWindow, UpdateApp updater)
        {
            try
            {
                string dataPath = updater.DataDirectory;
                Core.Updates.UpdateHandoffInfo? handoff = Core.Updates.UpdateHandoff.Read(dataPath);

                if (handoff.HasValue)
                {
                    loadingWindow.ShowUpdateSteps(handoff.Value);
                }

                Core.Updates.UpdateHandoff.Clear(dataPath);
            }
            catch (Exception ex)
            {
                LogStartupPhase($"Could not show the update summary: {ex.Message}");
            }
        }

        private void LogStartupPhase(string message)
        {
            _lastStartupPhase = message;
            string line = $"[Startup +{_startupStopwatch.ElapsedMilliseconds}ms] {message}";
            if (_loggingReady)
            {
                try
                {
                    Logging.WriteInfo(line);
                    return;
                }
                catch
                {
                }
            }

            WriteEarlyStartupLog(line);
        }

        private static void WriteEarlyStartupLog(string line)
        {
            try
            {
                string logRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Vrcosc-MagicChatbox",
                    "logs");
                Directory.CreateDirectory(logRoot);
                File.AppendAllText(
                    Path.Combine(logRoot, "startup-early.log"),
                    DateTimeOffset.Now.ToString("O") + " " + line + Environment.NewLine);
            }
            catch
            {
                Console.Error.WriteLine(line);
            }
        }

        private EventWaitHandle? _activationSignal;
        private CancellationTokenSource? _activationListenerCancellation;

        private static string ActivationSignalName(int profileNumber)
            => $@"Local\VrcoscMagicChatbox_Activate_Profile_{profileNumber}";

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int processId);

        private const int AllowForegroundFromAnyProcess = -1;

        private static bool TrySignalRunningInstance(int profileNumber)
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ActivationSignalName(profileNumber), out EventWaitHandle? signal))
                    return false;

                using (signal)
                {
                    // Windows only lets the foreground process hand focus away. This one is the
                    // foreground process for the moment, so it has to grant that right before the
                    // running copy can raise itself.
                    AllowSetForegroundWindow(AllowForegroundFromAnyProcess);
                    signal.Set();
                }

                return true;
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog($"Could not signal the running instance: {ex}");
                return false;
            }
        }

        private void StartListeningForActivationRequests(int profileNumber)
        {
            try
            {
                _activationSignal = new EventWaitHandle(
                    initialState: false,
                    EventResetMode.AutoReset,
                    ActivationSignalName(profileNumber));

                _activationListenerCancellation = new CancellationTokenSource();
                CancellationToken token = _activationListenerCancellation.Token;
                EventWaitHandle signal = _activationSignal;

                var listener = new Thread(() =>
                {
                    var waits = new WaitHandle[] { signal, token.WaitHandle };
                    while (!token.IsCancellationRequested)
                    {
                        if (WaitHandle.WaitAny(waits) != 0)
                            return;

                        try
                        {
                            Dispatcher.BeginInvoke(new Action(BringMainWindowToFront));
                        }
                        catch (Exception ex)
                        {
                            Logging.WriteInfo($"Could not bring the window to the front: {ex.Message}");
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "MagicChatbox activation listener",
                };

                listener.Start();
                LogStartupPhase("Listening for activation requests from later launches.");
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog($"Could not start the activation listener: {ex}");
            }
        }

        private static void BringMainWindowToFront()
        {
            MainWindow? window = mainWindow;
            if (window is null)
                return;

            window.Show();

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            window.Focus();
        }

        private bool TryAcquireSingleInstance(int profileNumber)
        {
            string mutexName = $@"Local\VrcoscMagicChatbox_Profile_{profileNumber}";
            try
            {
                _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out _ownsSingleInstanceMutex);
                LogStartupPhase(_ownsSingleInstanceMutex
                    ? $"Single-instance mutex acquired: {mutexName}."
                    : $"Single-instance mutex already owned: {mutexName}.");
                return _ownsSingleInstanceMutex;
            }
            catch (Exception ex)
            {
                WriteEarlyStartupLog($"Single-instance mutex failed: {ex}");
                return true;
            }
        }

        public void ShutdownFromSteamVr()
        {
            try
            {
                // Takes the same route as Exit in the tray menu, so "keep running when you close
                // the window" cannot quietly turn this into a hide.
                if (mainWindow is not null)
                {
                    mainWindow._isTrayClosing = true;
                    mainWindow.Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }

            Shutdown();
        }

        private static bool WasLaunchedBySteamVr(string[]? args)
            => args != null && args.Any(arg => arg.Equals(SteamVrLaunchArgument, StringComparison.OrdinalIgnoreCase));

        private static bool ShouldSkipSingleInstanceGuard(string[]? args)
        {
            if (args == null) return false;

            return args.Any(arg =>
                arg.Equals("-update", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-updateadmin", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-rollback", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-rollbackadmin", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-clearbackup", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetProfileNumberFromArgs(string[]? args, out int profileNumber, out string? invalidProfileNumber)
        {
            profileNumber = 0;
            invalidProfileNumber = null;

            if (args == null) return true;

            foreach (string arg in args)
            {
                if (!arg.StartsWith("-profile=", StringComparison.OrdinalIgnoreCase))
                    continue;

                string profileText = arg[9..];
                if (int.TryParse(profileText, out profileNumber) && profileNumber >= 0)
                    return true;

                invalidProfileNumber = profileText;
                profileNumber = 0;
                return false;
            }

            return true;
        }

        private void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (!ShouldLogFirstChanceException(e.Exception))
            {
                return;
            }

            Logging.WriteInfo(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);
        }

        private static bool ShouldLogFirstChanceException(Exception ex)
        {
            if (ex is OperationCanceledException)
                return false;

            if (ex is Win32Exception win32Ex &&
                win32Ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ex is IOException ioEx &&
                ioEx.Message.Contains("The process cannot access the file", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ex is HttpRequestException httpEx &&
                httpEx.Message.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ex is InvalidCastException castEx &&
                castEx.Message.Contains("ComboBoxAutomationPeer", StringComparison.OrdinalIgnoreCase) &&
                castEx.Message.Contains("IScrollProvider", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Debugger.IsAttached;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (!_startupCompleted ||
                e.Exception is OutOfMemoryException ||
                e.Exception is AccessViolationException ||
                e.Exception is InsufficientMemoryException)
            {
                Logging.WriteException(e.Exception, MSGBox: true, exitapp: true);
                return;
            }

            DateTime now = DateTime.UtcNow;
            _handledDispatcherExceptionTimes.Enqueue(now);
            while (_handledDispatcherExceptionTimes.Count > 0 &&
                   now - _handledDispatcherExceptionTimes.Peek() > HandledDispatcherExceptionWindow)
            {
                _handledDispatcherExceptionTimes.Dequeue();
            }

            if (_handledDispatcherExceptionTimes.Count > MaxHandledDispatcherExceptionsInWindow)
            {
                Logging.WriteException(e.Exception, MSGBox: true, exitapp: true);
                return;
            }

            try
            {
                Logging.WriteException(e.Exception, MSGBox: false);
            }
            catch
            {
            }

            e.Handled = true;

            try
            {
                Services?.GetService<IToastService>()?.Show(
                    "Unexpected error",
                    "An internal error occurred and was logged. The app keeps running.",
                    ToastType.Error,
                    key: "dispatcher-unhandled-exception");
            }
            catch
            {
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                Logging.WriteException(e.Exception, MSGBox: false);
            }
            catch
            {
            }

            e.SetObserved();
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
                Dispatcher.BeginInvoke(() =>
                {
                    Services.GetRequiredService<IBanEnforcementService>().ProcessBan(args.UserId, args.Reason);
                });
            };
            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(
                _ => allowedService.StartUserMonitoring(Core.Constants.AutoUpdateCheckInterval),
                TaskScheduler.Default);
        }

        private async Task RunOptionalStartupTaskAsync(
            string taskName,
            Action action,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
            => await RunOptionalStartupTaskAsync(
                taskName,
                () =>
                {
                    action();
                    return Task.CompletedTask;
                },
                cancellationToken,
                timeout);

        private async Task RunOptionalStartupTaskAsync(
            string taskName,
            Func<Task> action,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan effectiveTimeout = timeout ?? OptionalStartupTaskTimeout;
            var sw = Stopwatch.StartNew();
            Logging.WriteInfo($"[Startup] Optional task '{taskName}' started.");
            Task task = Task.Run(action, cancellationToken);
            Task delay = Task.Delay(effectiveTimeout, cancellationToken);

            Task completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = task.ContinueWith(
                    completedTask =>
                    {
                        if (completedTask.Exception != null)
                        {
                            Logging.WriteException(
                                new Exception($"Optional startup task '{taskName}' failed after it timed out.", completedTask.Exception),
                                MSGBox: false);
                        }
                    },
                    TaskContinuationOptions.OnlyOnFaulted);

                Logging.WriteInfo(
                    $"[Startup] Optional task '{taskName}' timed out after {effectiveTimeout.TotalSeconds:0}s; continuing startup.");
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
                Logging.WriteInfo($"[Startup] Optional task '{taskName}' completed in {sw.ElapsedMilliseconds}ms.");
            }
            catch (Exception ex)
            {
                Logging.WriteException(new Exception($"Optional startup task '{taskName}' failed.", ex), MSGBox: false);
            }
        }

        private async Task InitializeComponentsWithProgress(StartUp loadingWindow, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var vm = Services.GetRequiredService<ViewModel>();

            void LogStep(string name) => Logging.WriteInfo($"[Startup] {name} completed in {sw.ElapsedMilliseconds}ms");

            Services.GetRequiredService<WeatherOverrideState>().Initialize(_weatherSettings);

            loadingWindow.UpdateProgress("Loading your saved data...", 20, "Firing up modules...");
            await Task.WhenAll(
                RunOptionalStartupTaskAsync("settings restore", () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var intSettings = Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>().Value;
                    var trackerSettings = Services.GetRequiredService<ISettingsProvider<TrackerBatterySettings>>().Value;
                    var integrationDisplay = Services.GetRequiredService<IntegrationDisplayState>();
                    var trackerDisplay = Services.GetRequiredService<TrackerDisplayState>();
                    Services.GetRequiredService<IUiDispatcher>().BeginInvoke(() =>
                    {
                        integrationDisplay.IntegrationSortOrder = intSettings.SavedSortOrder;
                        trackerDisplay.TrackerDevices = trackerSettings.SavedDevices;
                    });
                    LogStep("Settings restore");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("status list", () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Services.GetRequiredService<IStatusListService>().LoadStatusList();
                    LogStep("Status list");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("chat history", () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Services.GetRequiredService<IChatHistoryService>().LoadChatHistory();
                    LogStep("Chat history");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("app history", () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Services.GetRequiredService<IAppHistoryService>().LoadAppHistory();
                    LogStep("App history");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("MediaLink sessions", async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Services.GetRequiredService<IMediaLinkPersistenceService>().LoadMediaSessionsAsync();
                    LogStep("MediaLink sessions");
                }, cancellationToken)
            );
            LogStep("Wave 1 complete");
            cancellationToken.ThrowIfCancellationRequested();

            loadingWindow.UpdateProgress("Firing up modules...", 55, "Finishing the last startup modules...");
            await Task.WhenAll(
                RunOptionalStartupTaskAsync("ComponentStats", async () =>
                {
                    var bootMods = Services.GetRequiredService<ModuleBootstrapper>();
                    var componentStats = Services.GetRequiredService<ComponentStatsModule>();
                    Services.GetRequiredService<ComponentStatsViewModel>();
                    await bootMods.RegisterComponentStatsAsync(componentStats).ConfigureAwait(false);
                    if (_integrationSettings.IntgrComponentStats
                        && Services.GetRequiredService<IPrivacyConsentService>().IsApproved(PrivacyHook.HardwareMonitor))
                    {
                        componentStats.StartModule();
                    }
                    LogStep("ComponentStats");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("NetworkStats", () =>
                {
                    var netStats = Services.GetRequiredService<NetworkStatisticsModule>();
                    Services.GetRequiredService<IModuleHost>().RegisterModule(netStats);
                    LogStep("NetworkStats");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("OpenAI", () =>
                {
                    var openAIModule = Services.GetRequiredService<OpenAIModule>();
                    var openAISettings = Services.GetRequiredService<ISettingsProvider<OpenAISettings>>().Value;

                    // The client is usable the moment it is constructed. Confirming the credentials costs a
                    // round-trip to the OpenAI API, which held the window back for about two seconds.
                    if (openAIModule.CreateClient(openAISettings.AccessToken, openAISettings.OrganizationID))
                        _ = openAIModule.VerifyConnectionAsync();

                    LogStep("OpenAI");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("IntelliChat", () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Services.GetRequiredService<ModuleBootstrapper>().CreateIntelliChat();
                    LogStep("IntelliChat");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("TTS voices", () =>
                {
                    var voices = Services.GetRequiredService<IAudioService>().ReadTikTokTTSVoices();
                    Services.GetRequiredService<IUiDispatcher>().BeginInvoke(() => vm.TtsAudio.TikTokTTSVoices = voices);
                    LogStep("TTS voices");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("Audio devices", () =>
                {
                    Services.GetRequiredService<IAudioService>().PopulateOutputDevices();
                    LogStep("Audio devices");
                }, cancellationToken)
            );
            LogStep("Wave 2 complete");
            cancellationToken.ThrowIfCancellationRequested();

            loadingWindow.UpdateProgress("Finishing the last startup modules...", 85, "Starting runtime modules...");
            ApplicationMediaController = new MediaLinkModule(
                shouldStart: false,
                Services.GetRequiredService<IPrivacyConsentService>(),
                Services.GetRequiredService<IAppState>(),
                Services.GetRequiredService<MediaLinkDisplayState>(),
                Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
                Services.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
                Services.GetRequiredService<IUiDispatcher>(),
                Services.GetRequiredService<IToastService>());
            LogStep("MediaLinkModule");

            await RunOptionalStartupTaskAsync(
                "MediaLink session listener",
                () => ApplicationMediaController.StartIfEnabled(),
                cancellationToken);

            if (_integrationSettings.IntgrScanMediaLink
                && Services.GetRequiredService<IPrivacyConsentService>().IsApproved(PrivacyHook.MediaSession)
                && !ApplicationMediaController.IsRunning)
            {
                Logging.WriteInfo(
                    "[Startup] MediaLink is enabled but the Windows media session did not attach. " +
                    "Music Display will stay empty this session; the media session service usually needs a reboot to recover.");
            }

            loadingWindow.UpdateProgress("Starting runtime modules...", 90, "Building the main window shell...");
            await Task.WhenAll(
                RunOptionalStartupTaskAsync(
                    "runtime modules",
                    () => Services.GetRequiredService<ModuleBootstrapper>().CreateRuntimeModulesAsync(),
                    cancellationToken,
                    TimeSpan.FromSeconds(20)),
                RunOptionalStartupTaskAsync("seekbar styles", async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Services.GetRequiredService<IMediaLinkPersistenceService>().LoadSeekbarStylesAsync();
                    LogStep("Seekbar styles");
                }, cancellationToken)
            );
            LogStep("Runtime modules + seekbar");
            cancellationToken.ThrowIfCancellationRequested();

            Logging.WriteInfo($"[Startup] All components initialized in {sw.ElapsedMilliseconds}ms");
        }
    }
}
