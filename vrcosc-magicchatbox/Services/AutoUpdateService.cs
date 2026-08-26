using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Core.Updates;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Installs updates without being asked. The download and the checksum happen while the app is
/// running; swapping the files never does, because that always means restarting. A package is
/// staged and then applied at the next cold start, which is the one moment nothing is connected.
/// </summary>
public sealed class AutoUpdateService : IAutoUpdateService
{
    private const int FailedStartsBeforeRollback = 3;

    private readonly AppUpdateState _updateState;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly Lazy<IToastService> _toast;

    private readonly SemaphoreSlim _stagingGate = new(1, 1);
    private IReadOnlyList<string> _blockedVersions = [];
    private string _dataPath = string.Empty;

    public AutoUpdateService(
        AppUpdateState updateState,
        IHttpClientFactory httpClientFactory,
        IUiDispatcher dispatcher,
        ISettingsProvider<AppSettings> appSettingsProvider,
        Lazy<IToastService> toast)
    {
        _updateState = updateState;
        _httpClientFactory = httpClientFactory;
        _dispatcher = dispatcher;
        _appSettingsProvider = appSettingsProvider;
        _toast = toast;
    }

    public IReadOnlyList<string> BlockedVersions => _blockedVersions;

    public StartupUpdateOutcome PrepareForStartup(bool launchedBySteamVr)
    {
        UpdateApp updater;

        try
        {
            updater = CreateUpdater();
            _dataPath = updater.DataDirectory;
            _blockedVersions = UpdateBlocklist.Read(_dataPath);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return StartupUpdateOutcome.Continue;
        }

        StartupHealthCheck health = StartupHealthBeacon.MarkStarting(_dataPath);

        if (ShouldRollBack(health) && TryRollBack(updater, health))
            return StartupUpdateOutcome.HandingOff;

        if (_appSettingsProvider.Value.UseCustomProfile)
            return StartupUpdateOutcome.Continue;

        // Being started by SteamVR is the opening moment of a session, not a quiet one. The
        // package keeps until a launch that is not about to put someone in a headset.
        if (launchedBySteamVr)
            return StartupUpdateOutcome.Continue;

        return TryApplyStagedUpdate(updater)
            ? StartupUpdateOutcome.HandingOff
            : StartupUpdateOutcome.Continue;
    }

    public void ReportStartupHealthy()
    {
        if (string.IsNullOrEmpty(_dataPath))
            return;

        StartupHealth health = StartupHealthBeacon.Read(_dataPath);
        StartupHealthBeacon.MarkHealthy(_dataPath);

        // Someone who never clicked anything is now on a different version than they closed on.
        // Saying so once is the difference between a feature and an unexplained change.
        if (!string.IsNullOrWhiteSpace(health.AutoInstalledVersion))
        {
            Announce(
                "MagicChatbox updated itself",
                $"You are now on {health.AutoInstalledVersion}. Options has a button to go back.",
                ToastType.Success,
                $"auto-update-installed-{health.AutoInstalledVersion}");
        }
    }

    public async Task ConsiderAsync(UpdateVerdict verdict)
    {
        if (verdict is not { Action: UpdateAction.AutoInstall, Standing: UpdateStanding.UpdateAvailable })
            return;

        if (string.IsNullOrWhiteSpace(verdict.Url) || string.IsNullOrWhiteSpace(verdict.Version))
            return;

        if (_appSettingsProvider.Value.UseCustomProfile)
            return;

        if (!await _stagingGate.WaitAsync(0))
            return;

        try
        {
            UpdateApp updater = CreateUpdater();
            _dataPath = updater.DataDirectory;

            PendingUpdateInfo? staged = PendingUpdate.Read(_dataPath);
            if (staged != null && ReleaseVersion.Compare(staged.Version, verdict.Version) >= 0)
                return;

            if (staged != null)
                updater.DiscardStagedUpdate();

            Logging.WriteInfo($"Downloading {verdict.Version} in the background for an unattended install.");

            await updater.PrepareUpdate(unattended: true);

            if (PendingUpdate.Read(_dataPath) == null)
                return;

            _updateState.CanUpdate = false;
            _updateState.CanUpdateLabel = false;

            Announce(
                "Update ready",
                $"MagicChatbox {verdict.Version} is downloaded and installs the next time you start it.",
                ToastType.Success,
                $"auto-update-staged-{verdict.Version}");
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);

            Announce(
                "Update could not be downloaded",
                "MagicChatbox will try again later. You can still install it from Options.",
                ToastType.Warning,
                "auto-update-failed");
        }
        finally
        {
            _stagingGate.Release();
        }
    }

    private bool TryApplyStagedUpdate(UpdateApp updater)
    {
        PendingUpdateInfo? staged = PendingUpdate.Read(_dataPath);
        if (staged == null)
            return false;

        if (IsBlocked(staged.Version))
        {
            Logging.WriteInfo($"Discarding the staged {staged.Version}: it failed to start before.");
            updater.DiscardStagedUpdate();
            return false;
        }

        // Cleared first so the deliberate exit below is not counted as a failed start, then the
        // incoming version is put on probation until it reports a healthy launch of its own.
        StartupHealthBeacon.MarkHealthy(_dataPath);
        StartupHealthBeacon.RecordAutoInstall(_dataPath, staged.Version);

        if (updater.TryStartStagedInstall())
            return true;

        StartupHealthBeacon.MarkHealthy(_dataPath);
        return false;
    }

    private bool ShouldRollBack(StartupHealthCheck health)
        => health.PreviousStartFailed
           && health.WasAutoInstalled
           && health.ConsecutiveFailures >= FailedStartsBeforeRollback;

    private bool TryRollBack(UpdateApp updater, StartupHealthCheck health)
    {
        try
        {
            Logging.WriteInfo(
                $"{health.AutoInstalledVersion} failed to start {health.ConsecutiveFailures} times running. " +
                "Going back to the previous version and switching automatic installs off.");

            _blockedVersions = UpdateBlocklist.Add(_dataPath, health.AutoInstalledVersion);

            AppSettings settings = _appSettingsProvider.Value;
            if (settings.StableUpdateMode == UpdateChannelMode.Auto)
                settings.StableUpdateMode = UpdateChannelMode.Notify;
            if (settings.PreReleaseUpdateMode == UpdateChannelMode.Auto)
                settings.PreReleaseUpdateMode = UpdateChannelMode.Notify;

            updater.DiscardStagedUpdate();
            StartupHealthBeacon.MarkHealthy(_dataPath);

            if (!updater.CheckIfBackupExists())
            {
                Logging.WriteInfo("No backup was available to go back to.");
                return false;
            }

            updater.StartRollback();
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            StartupHealthBeacon.MarkHealthy(_dataPath);
            return false;
        }
    }

    private bool IsBlocked(string version)
    {
        foreach (string blocked in _blockedVersions)
        {
            if (ReleaseVersion.Compare(blocked, version) == 0)
                return true;
        }

        return false;
    }

    private UpdateApp CreateUpdater()
        => new(_updateState, _httpClientFactory, _dispatcher);

    private void Announce(string title, string message, ToastType type, string key)
    {
        try
        {
            _toast.Value.Show(title, message, type, key: key);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Could not show the update notice: {ex.Message}");
        }
    }
}
