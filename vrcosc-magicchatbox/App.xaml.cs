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
            Services.GetRequiredService<Setti ngsProvider<WeatherSettings>>().Value);
        private static WeatherSettings _weatherSettings => _lazyWeatherSettings.Value;

        public static IMediaLinkService ApplicationMediaController { get; private set; }

        private readonly Stopwatch _startupStopwatch = new();
        private static readonly TimeSpan StartupWatchdogTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan RequiredStartupTaskTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan OptionalStartupTaskTimeout = TimeSpan.FromSeconds(10);
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;
        private bool _loggingReady;
        private volatile bool _startupCompleted;
        private string _lastStartupPhase = "Process created.";

        public static MainWindow mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _ = StartupAsync(e);
        }

        private async Task StartupAsync(StartupEventArgs e)
        {
            _startupStopwatch.Start();
            LogStartupPhase($"Process started. PID={Environment.ProcessId}, Args='{string.Join(" ", e.Args ?? Array.Empty<string>())}'.");

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
                LogStartupPhase($"Second instance detected for profile {startupProfileNumber}. Exiting this instance.");
                MessageBox.Show(
                    "MagicChatbox is already running for this profile.",
                    "MagicChatbox",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
#if DEBUG
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
#endif

            var startupCancellation = new CancellationTokenSource();
            Task startupWatchdogTask = RunStartupWatchdogAsync(startupCancellation.Token);

            StartUp? loadingWindow = null;
            try
            {
                loadingWindow = new StartUp(startupCancellation.Cancel);
                loadingWindow.UpdateProgress("Opening startup window...", 1, "Configuring services...");
                loadingWindow.Show();
                loadingWindow.Activate();
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

                // Initialize static Logging with DI services (eliminates service locator in Logging.cs)
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
                    // Initialize static defaults for model classes (eliminates service locator in models)
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
                                case "-rollbackadmin":
                                    loadingWindow.UpdateProgress("Rollback with admin powers engaged.", 55);
                                    await Task.Run(() => updater.RollbackApplication(loadingWindow, true));
                                    Shutdown();
                                    return;
                                case "-clearbackup":
                                    loadingWindow.UpdateProgress("Rolling back and clearing the slate. Fresh start!", 50);
                                    await Task.Run(() => updater.ClearBackUp());
                                    break;
                                default:
                                    loadingWindow.Close();
                                    LogStartupPhase($"Invalid command line argument '{arg}'.");
                                    Logging.WriteException(new Exception($"Invalid command line argument '{arg}'"), MSGBox: true, exitapp: true);
                                    return;
                            }
                        }
                    }
                }

                bool tosJustAccepted = false;
                {
                    LogStartupPhase("Checking TOS/privacy wizard state.");
                    var appSettingsProvider = Services.GetRequiredService<ISettingsProvider<AppSettings>>();
                    var consentService = Services.GetRequiredService<IPrivacyConsentService>();
                    if (appSettingsProvider.Value.AcceptedTosVersion != Core.Constants.TosVersion)
                    {
                        var wizard = new TosAndPrivacyWizard(consentService, appSettingsProvider);
                        DialogWindowHelper.PrepareModal(wizard, loadingWindow);
                        tosJustAccepted = wizard.ShowDialog() == true;
                        // If wizard was dismissed, fall through to per-hook consent dialog below
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
                        DialogWindowHelper.PrepareModal(dialog, loadingWindow);
                        dialog.ShowDialog();
                    }
                }

                await InitializeComponentsWithProgress(loadingWindow, startupCancellation.Token);
                LogStartupPhase("Component initialization completed.");
                startupCancellation.Token.ThrowIfCancellationRequested();

                loadingWindow.UpdateProgress("Building the main window shell... Hammer, nails, UI!", 98.5, "Rolling out the red carpet... Here comes the UI!");
                Logging.WriteInfo("Creating MainWindow instance.");

                mainWindow = new MainWindow(
                    Services.GetRequiredService<ScanLoopService>(),
                    Services.GetRequiredService<ModuleBootstrapper>(),
                    Services.GetRequiredService<Core.Services.IModuleHost>(),
                    Services.GetRequiredService<IStatePersistenceCoordinator>(),
                    Services.GetRequiredService<ITrayIconService>(),
                    Services.GetRequiredService<HotkeyManagement>());
                // DataContext is NOT set yet — Show() renders an empty shell in ~570ms
                // instead of hanging while WPF evaluates every binding + automation peer.
                Logging.WriteInfo("MainWindow instance created.");

                loadingWindow.UpdateProgress("Rolling out the red carpet... Here comes the UI!", 99, "Wiring up the final UI bits... Almost there!");
                loadingWindow.Topmost = true;
                Logging.WriteInfo("[Startup] Showing MainWindow (empty shell)...");
                mainWindow.Show();
                Logging.WriteInfo("[Startup] MainWindow shown.");

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

                Logging.WriteInfo("[Startup] Waiting for initial render...");
                await Task.Delay(150);
                Logging.WriteInfo("[Startup] Initial render completed.");

                mainWindow.UpdateOverlayProgress("Rendering interface...", 95, "Restoring open page...");

                loadingWindow.Close();
                Logging.WriteInfo("[Startup] Splash closed.");

                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                mainWindow.Activate();
                mainWindow.Focus();
                if (vm.AppSettingsInstance.StartInBackground)
                    mainWindow.Hide();

                mainWindow.HideStartupOverlay();

                // Signal that startup is complete — modules waiting for auto-start can now proceed
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
                InitializeUserMonitoring();
                Logging.WriteInfo("[Startup] User monitoring initialized.");

                // Start background scan loop LAST — after window is visible and splash is gone
                Logging.WriteInfo("[Startup] Starting background scan loop...");
                mainWindow.StartBackgroundProcessing();
                Logging.WriteInfo("[Startup] Background processing started.");

                if (vm.AppSettingsInstance.CheckUpdateOnStartup)
                {
                    _ = RunDeferredStartupUpdateCheckAsync();
                }

                Services.GetRequiredService<ITrayIconService>().Initialize(mainWindow);
            }
            catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested)
            {
                LogStartupPhase("Startup cancelled by user.");
                try
                {
                    loadingWindow?.Close();
                }
                catch { /* window may already be closing */ }

                Shutdown();
            }
            catch (Exception ex)
            {
                LogStartupPhase($"Startup failed: {ex}");
                // If logging is available, use it; otherwise fall back to MessageBox
                try
                {
                    Logging.WriteException(ex, MSGBox: false);
                }
                catch { /* logging itself may have failed */ }

                try
                {
                    loadingWindow?.Close();
                }
                catch { /* window may not be open */ }

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
                    // Mutex was not owned at shutdown; no recovery is needed.
                }
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
                await Task.Delay(StartupWatchdogTimeout, cancellationToken).ConfigureAwait(false);
                if (_startupCompleted || cancellationToken.IsCancellationRequested)
                    return;

                string message = $"Startup watchdog timed out after {StartupWatchdogTimeout.TotalSeconds:0}s. Last phase: {_lastStartupPhase}";
                WriteEarlyStartupLog(message);
                try
                {
                    Logging.WriteInfo(message);
                }
                catch
                {
                    // Logging may not be ready if startup stalled early.
                }

                await Task.Yield();
                if (_startupCompleted || cancellationToken.IsCancellationRequested)
                    return;

                Environment.Exit(1);
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
                LogManager.LoadConfiguration(nlogConfigPath);
            }

            // After configuration is loaded, cache a logger instance for the
            // static Logging helper to avoid initializing NLog during
            // FirstChanceException handling which can cause recursive errors.
            try
            {
                var logger = LogManager.GetCurrentClassLogger();
                Logging.SetLoggerInstance(logger);
            }
            catch
            {
                // ignore - Logging will fallback to Console.Error when needed
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
                    // Fall through to early file logging.
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
                Dispatcher.BeginInvoke(() =>
                {
                    Services.GetRequiredService<IBanEnforcementService>().ProcessBan(args.UserId, args.Reason);
                });
            };
            allowedService.StartUserMonitoring(Core.Constants.AutoUpdateCheckInterval);
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

            // ── Wave 1: File I/O (all independent, run in parallel) ──
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

            // ── Wave 2: Module initialization (independent, run in parallel) ──
            loadingWindow.UpdateProgress("Firing up modules...", 55, "Finishing the last startup modules...");
            await Task.WhenAll(
                RunOptionalStartupTaskAsync("ComponentStats", async () =>
                {
                    var bootMods = Services.GetRequiredService<ModuleBootstrapper>();
                    var componentStats = Services.GetRequiredService<ComponentStatsModule>();
                    Services.GetRequiredService<ComponentStatsViewModel>();
                    await bootMods.RegisterComponentStatsAsync(componentStats).ConfigureAwait(false);
                    componentStats.StartModule();
                    LogStep("ComponentStats");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("NetworkStats", () =>
                {
                    var netStats = Services.GetRequiredService<NetworkStatisticsModule>();
                    Services.GetRequiredService<IModuleHost>().RegisterModule(netStats);
                    LogStep("NetworkStats");
                }, cancellationToken),
                RunOptionalStartupTaskAsync("OpenAI", async () =>
                {
                    var openAIModule = Services.GetRequiredService<OpenAIModule>();
                    var openAISettings = Services.GetRequiredService<ISettingsProvider<OpenAISettings>>().Value;
                    await openAIModule.InitializeClient(openAISettings.AccessToken, openAISettings.OrganizationID);
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

            // ── Wave 3: Final wiring (MediaLink + runtime modules + seekbar) ──
            loadingWindow.UpdateProgress("Finishing the last startup modules...", 85, "Starting runtime modules...");
            ApplicationMediaController = new MediaLinkModule(
                _integrationSettings.IntgrScanMediaLink,
                Services.GetRequiredService<IPrivacyConsentService>(),
                Services.GetRequiredService<IAppState>(),
                Services.GetRequiredService<MediaLinkDisplayState>(),
                Services.GetRequiredService<ISettingsProvider<IntegrationSettings>>(),
                Services.GetRequiredService<ISettingsProvider<MediaLinkSettings>>(),
                Services.GetRequiredService<IUiDispatcher>(),
                Services.GetRequiredService<IToastService>());
            LogStep("MediaLinkModule");

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
