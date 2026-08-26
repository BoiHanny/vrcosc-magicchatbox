using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services.Vr;

public interface IOpenVrSessionService : IDisposable
{
    IDisposable AcquireLease(PrivacyHook reason, string consumerName);

    CVRSystem? System { get; }

    CVRCompositor? Compositor { get; }

    bool IsAttached { get; }

    string DescribeStatus();
}

public sealed class OpenVrSessionService : IOpenVrSessionService
{
    private static readonly TimeSpan AttachRetryInterval = TimeSpan.FromSeconds(5);

    private readonly IOpenVrRuntime _runtime;
    private readonly IAppState? _appState;
    private readonly IPrivacyConsentService? _consent;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<bool> _isUiThread;
    private readonly object _lock = new();
    private readonly List<Lease> _leases = new();

    private bool _attached;
    private EVRInitError _lastInitError = EVRInitError.None;
    private OpenXrRuntimeInfo? _runtimeInfo;
    private DateTime _nextAttachAttemptUtc = DateTime.MinValue;
    private int _attachInProgress;
    private bool _loggedAttachFailure;
    private bool _disposed;

    public OpenVrSessionService(
        IOpenVrRuntime runtime,
        IAppState? appState,
        IPrivacyConsentService? consent,
        Func<DateTime>? utcNow = null,
        Func<bool>? isUiThread = null)
    {
        _runtime = runtime;
        _appState = appState;
        _consent = consent;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _isUiThread = isUiThread ?? (() => false);

        if (_appState != null)
            _appState.PropertyChanged += OnAppStatePropertyChanged;
        if (_consent != null)
            _consent.ConsentChanged += OnConsentChanged;
    }

    public bool IsAttached
    {
        get { lock (_lock) return _attached; }
    }

    public CVRSystem? System
    {
        get
        {
            EnsureAttached();
            lock (_lock) return _attached ? _runtime.System : null;
        }
    }

    public CVRCompositor? Compositor
    {
        get
        {
            EnsureAttached();
            lock (_lock) return _attached ? _runtime.Compositor : null;
        }
    }

    public IDisposable AcquireLease(PrivacyHook reason, string consumerName)
    {
        var lease = new Lease(this, reason, consumerName);
        lock (_lock)
        {
            if (_disposed)
                return lease;
            _leases.Add(lease);
        }

        return lease;
    }

    private void EnsureAttached()
    {
        bool shouldDetach;
        bool shouldAttempt;

        lock (_lock)
        {
            if (_disposed)
                return;

            bool wanted = HasConsentedLeaseNoLock() && (_appState?.IsVRRunning ?? false);

            if (!wanted)
            {
                shouldDetach = _attached;
                shouldAttempt = false;
            }
            else
            {
                shouldDetach = false;
                shouldAttempt = !_attached && _utcNow() >= _nextAttachAttemptUtc;
            }
        }

        if (shouldDetach)
        {
            Detach("no consented lease or VR stopped");
            return;
        }

        if (shouldAttempt)
            StartAttach();
    }

    private void StartAttach()
    {
        if (Interlocked.CompareExchange(ref _attachInProgress, 1, 0) == 1)
            return;

        if (_isUiThread())
        {
            _ = Task.Run(AttachAndRelease);
            return;
        }

        AttachAndRelease();
    }

    private void AttachAndRelease()
    {
        try
        {
            Attach();
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"OpenVR attach threw: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _attachInProgress, 0);
        }
    }

    private void Attach()
    {
        bool ok;
        EVRInitError error;

        try
        {
            ok = _runtime.TryInit(out error);
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _nextAttachAttemptUtc = _utcNow() + AttachRetryInterval;
                _lastInitError = EVRInitError.Unknown;
            }

            Logging.WriteInfo($"OpenVR attach threw: {ex.Message}");
            return;
        }

        bool logFailure = false;
        lock (_lock)
        {
            _lastInitError = error;

            if (!ok || error != EVRInitError.None)
            {
                _attached = false;
                _nextAttachAttemptUtc = _utcNow() + AttachRetryInterval;

                logFailure = error != EVRInitError.Init_NoServerForBackgroundApp && !_loggedAttachFailure;
                if (logFailure)
                    _loggedAttachFailure = true;
            }
            else
            {
                _attached = true;
                _loggedAttachFailure = false;
            }
        }

        if (logFailure)
            Logging.WriteInfo($"OpenVR attach failed: {error}");
    }

    private void Detach(string reason)
    {
        bool wasAttached;
        lock (_lock)
        {
            wasAttached = _attached;
            _attached = false;
            _nextAttachAttemptUtc = DateTime.MinValue;
        }

        if (!wasAttached)
            return;

        try
        {
            _runtime.Shutdown();
            Logging.WriteInfo($"OpenVR session released ({reason}).");
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"OpenVR shutdown error (non-fatal): {ex.Message}");
        }
    }

    private bool HasConsentedLeaseNoLock()
        => _leases.Any(lease => _consent?.IsApproved(lease.Reason) ?? false);

    private void Release(Lease lease)
    {
        lock (_lock)
        {
            _leases.Remove(lease);
        }

        EnsureAttached();
    }

    private void OnAppStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAppState.IsVRRunning))
            EnsureAttached();
    }

    private void OnConsentChanged(object? sender, ConsentChangedEventArgs e) => EnsureAttached();

    private OpenXrRuntimeInfo? RuntimeInfo
    {
        get
        {
            if (_runtimeInfo != null)
                return _runtimeInfo;

            try
            {
                _runtimeInfo = OpenXrRuntimeDetector.Detect();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"OpenXR runtime detection failed: {ex.Message}");
            }

            return _runtimeInfo;
        }
    }

    public string DescribeStatus()
    {
        string consumers;
        bool attached;
        EVRInitError lastError;

        lock (_lock)
        {
            consumers = _leases.Count == 0
                ? "no consumers"
                : string.Join(", ", _leases.Select(l =>
                    $"{l.ConsumerName}{(_consent?.IsApproved(l.Reason) ?? false ? string.Empty : " (no consent)")}"));
            attached = _attached;
            lastError = _lastInitError;
        }

        if (attached)
            return $"SteamVR: attached | {consumers}";

        string why = !(_appState?.IsVRRunning ?? false)
            ? "VR is not running"
            : lastError switch
            {
                EVRInitError.Init_NoServerForBackgroundApp => "SteamVR is not running",
                EVRInitError.None => "not attached yet",
                _ => lastError.ToString(),
            };

        string runtimeNote = string.Empty;
        if (lastError == EVRInitError.Init_NoServerForBackgroundApp && (_appState?.IsVRRunning ?? false))
        {
            var runtime = RuntimeInfo;
            if (runtime != null && !runtime.SupportsFrameTiming)
                runtimeNote = $" {runtime.DescribeForUser()}";
        }

        return $"SteamVR: not attached ({why}){runtimeNote} | {consumers}";
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _leases.Clear();
        }

        if (_appState != null)
            _appState.PropertyChanged -= OnAppStatePropertyChanged;
        if (_consent != null)
            _consent.ConsentChanged -= OnConsentChanged;

        Detach("service disposed");
    }

    private sealed class Lease : IDisposable
    {
        private readonly OpenVrSessionService _owner;
        private bool _released;

        public Lease(OpenVrSessionService owner, PrivacyHook reason, string consumerName)
        {
            _owner = owner;
            Reason = reason;
            ConsumerName = consumerName;
        }

        public PrivacyHook Reason { get; }
        public string ConsumerName { get; }

        public void Dispose()
        {
            if (_released)
                return;
            _released = true;
            _owner.Release(this);
        }
    }
}
