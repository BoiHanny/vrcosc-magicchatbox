using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services.Vr;

public interface ISteamVrAutoStartService
{
    void Start();

    void Stop();
}

/// <summary>
/// Keeps SteamVR's copy of the startup registration in step with the setting, and with wherever
/// this copy of MagicChatbox currently lives.
/// </summary>
public sealed class SteamVrAutoStartService : ISteamVrAutoStartService, IDisposable
{
    private const string SteamVrProcessName = "vrmonitor";

    private readonly ISteamVrApplications _applications;
    private readonly ISettingsProvider<AppSettings> _settingsProvider;
    private readonly IAppState _appState;
    private readonly IProcessPresenceService _processes;
    private readonly Action _requestShutdown;
    private readonly Func<string?> _executablePath;
    private readonly string _manifestPath;

    private int _reconcileInProgress;
    private int _reconcileRequested;
    private bool _retryPending;
    private bool _sawSteamVrRunning;
    private bool _started;
    private bool _disposed;

    public SteamVrAutoStartService(
        ISteamVrApplications applications,
        ISettingsProvider<AppSettings> settingsProvider,
        IAppState appState,
        IProcessPresenceService processes,
        Action requestShutdown,
        string? localAppDataPath = null,
        Func<string?>? executablePath = null)
    {
        _applications = applications;
        _settingsProvider = settingsProvider;
        _appState = appState;
        _processes = processes;
        _requestShutdown = requestShutdown;
        _executablePath = executablePath ?? ResolveExecutablePath;
        _manifestPath = SteamVrManifest.PathFor(
            localAppDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    public void Start()
    {
        if (_started || _disposed)
            return;

        _started = true;
        _settingsProvider.Value.PropertyChanged += OnSettingChanged;

        if (_appState != null)
        {
            _sawSteamVrRunning = _appState.IsVRRunning && IsSteamVrPresent();
            _appState.PropertyChanged += OnAppStateChanged;
        }

        Reconcile();
    }

    public void Stop()
    {
        if (!_started)
            return;

        _started = false;
        _settingsProvider.Value.PropertyChanged -= OnSettingChanged;

        if (_appState != null)
            _appState.PropertyChanged -= OnAppStateChanged;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.StartWithSteamVr))
            Reconcile();
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IAppState.IsVRRunning))
            return;

        if (_appState.IsVRRunning)
        {
            if (!IsSteamVrPresent())
                return;

            _sawSteamVrRunning = true;

            if (_retryPending)
            {
                _retryPending = false;
                Reconcile();
            }

            return;
        }

        if (!_sawSteamVrRunning)
            return;

        _sawSteamVrRunning = false;

        AppSettings settings = _settingsProvider.Value;
        if (!settings.StartWithSteamVr || !settings.QuitWithSteamVr)
            return;

        // IsVRRunning also covers the Oculus runtime, so this confirms it was SteamVR that went
        // away before taking the app down with it.
        _processes?.Invalidate(SteamVrProcessName);
        if (IsSteamVrPresent())
            return;

        Logging.WriteInfo("SteamVR closed, so MagicChatbox is closing with it.");
        _requestShutdown?.Invoke();
    }

    private bool IsSteamVrPresent()
        => _processes?.IsRunning(SteamVrProcessName) ?? true;

    private void Reconcile()
    {
        if (_disposed)
            return;

        if (Interlocked.CompareExchange(ref _reconcileInProgress, 1, 0) == 1)
        {
            // A change that arrives while one of these is already running is remembered rather
            // than dropped. Without it a quick double-toggle leaves SteamVR holding the state
            // the user just moved away from.
            Interlocked.Exchange(ref _reconcileRequested, 1);
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                ReconcileCore();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
            finally
            {
                Interlocked.Exchange(ref _reconcileInProgress, 0);
            }

            if (Interlocked.Exchange(ref _reconcileRequested, 0) == 1)
                Reconcile();
        });
    }

    private void ReconcileCore()
    {
        AppSettings settings = _settingsProvider.Value;

        SteamVrResult result = settings.StartWithSteamVr
            ? Register(settings)
            : Unregister(settings);

        switch (result.Outcome)
        {
            case SteamVrOutcome.Done:
                Logging.WriteInfo(settings.StartWithSteamVr
                    ? "MagicChatbox is registered to start with SteamVR."
                    : "MagicChatbox no longer starts with SteamVR.");
                break;

            case SteamVrOutcome.SteamVrUnavailable:
                // Nothing is wrong: SteamVR simply is not up yet. The setting stands and the
                // registration is applied the next time a session appears.
                Logging.WriteInfo($"Leaving the SteamVR startup setting until SteamVR is running. {result.Detail}");
                ArmRetry();
                break;

            case SteamVrOutcome.Failed:
                Logging.WriteInfo($"Could not update the SteamVR startup setting: {result.Detail}");
                ArmRetry();
                break;
        }
    }

    private SteamVrResult Register(AppSettings settings)
    {
        string? executable = _executablePath();
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return SteamVrResult.Failed("Could not work out where MagicChatbox is running from.");

        if (!SteamVrManifest.TryWrite(_manifestPath, executable, App.SteamVrLaunchArgument))
            return SteamVrResult.Failed($"Could not write {_manifestPath}");

        // Re-asserted on every launch rather than once, because SteamVR is known to lose the
        // auto-launch flag across its own restarts and the call costs nothing when it is already
        // set. It also picks up an executable that moved since last time.
        SteamVrResult result = _applications.Register(_manifestPath, SteamVrManifest.AppKey);

        if (result.Succeeded)
            settings.SteamVrManifestPath = _manifestPath;

        return result;
    }

    private SteamVrResult Unregister(AppSettings settings)
    {
        string registered = string.IsNullOrWhiteSpace(settings.SteamVrManifestPath)
            ? _manifestPath
            : settings.SteamVrManifestPath;

        SteamVrResult result = _applications.Unregister(registered, SteamVrManifest.AppKey);

        if (result.Outcome == SteamVrOutcome.SteamVrUnavailable)
            return result;

        SteamVrManifest.Delete(registered);
        if (!string.Equals(registered, _manifestPath, StringComparison.OrdinalIgnoreCase))
            SteamVrManifest.Delete(_manifestPath);

        settings.SteamVrManifestPath = string.Empty;
        return result;
    }

    private void ArmRetry() => _retryPending = true;

    private static string? ResolveExecutablePath()
    {
        try
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
                return Path.GetFullPath(path);

            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
        {
            return null;
        }
    }
}
