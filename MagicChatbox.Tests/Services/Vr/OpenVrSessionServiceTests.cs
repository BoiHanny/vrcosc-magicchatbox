using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Valve.VR;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services.Vr;
using Xunit;

namespace MagicChatbox.Tests.Services.Vr;

public class OpenVrSessionServiceTests
{
    private sealed class FakeRuntime : IOpenVrRuntime
    {
        public int InitCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public bool IsOpen { get; private set; }
        public EVRInitError NextError { get; set; } = EVRInitError.None;
        public Exception? ThrowOnInit { get; set; }

        public bool TryInit(out EVRInitError error)
        {
            InitCount++;
            if (ThrowOnInit != null)
                throw ThrowOnInit;

            error = NextError;
            IsOpen = error == EVRInitError.None;
            return IsOpen;
        }

        public void Shutdown()
        {
            ShutdownCount++;
            IsOpen = false;
        }

        public CVRSystem? System => null;
        public CVRCompositor? Compositor => null;
    }

    private sealed class FakeAppState : IAppState
    {
        private bool _isVRRunning;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool MasterSwitch { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public int MainWindowBlurEffect { get; set; }

        public bool IsVRRunning
        {
            get => _isVRRunning;
            set
            {
                if (_isVRRunning == value) return;
                _isVRRunning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVRRunning)));
            }
        }
    }

    private sealed class FakeConsent : IPrivacyConsentService
    {
        private readonly HashSet<PrivacyHook> _approved = new();

        public event EventHandler<ConsentChangedEventArgs>? ConsentChanged;

        public bool IsApproved(PrivacyHook hook) => _approved.Contains(hook);
        public ConsentState GetState(PrivacyHook hook) => IsApproved(hook) ? ConsentState.Approved : ConsentState.Unknown;

        public void Approve(PrivacyHook hook)
        {
            _approved.Add(hook);
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Approved));
        }

        public void Deny(PrivacyHook hook)
        {
            _approved.Remove(hook);
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Denied));
        }

        public void Reset(PrivacyHook hook) => Deny(hook);

        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
            => hooks.Where(h => !IsApproved(h)).ToList();
    }

    private sealed class Harness
    {
        public FakeRuntime Runtime { get; } = new();
        public FakeAppState AppState { get; } = new();
        public FakeConsent Consent { get; } = new();
        public DateTime Now { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public OpenVrSessionService Service { get; }

        public Harness(bool vrRunning = true, params PrivacyHook[] approved)
        {
            foreach (var hook in approved)
                Consent.Approve(hook);

            AppState.IsVRRunning = vrRunning;
            Service = new OpenVrSessionService(Runtime, AppState, Consent, () => Now);
        }
    }

    [Fact]
    public void NoLeaseMeansNoSession()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);

        Assert.False(h.Service.IsAttached);
        Assert.Equal(0, h.Runtime.InitCount);
    }

    [Fact]
    public void ALeaseWithConsentAttachesOnFirstAccess()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");

        _ = h.Service.Compositor;

        Assert.True(h.Service.IsAttached);
        Assert.Equal(1, h.Runtime.InitCount);
    }

    [Fact]
    public void SecondConsumerReusesTheSameSession()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Consent.Approve(PrivacyHook.VrTrackerBattery);

        using var perf = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;
        using var tracker = h.Service.AcquireLease(PrivacyHook.VrTrackerBattery, "tracker");
        _ = h.Service.System;

        Assert.Equal(1, h.Runtime.InitCount);
        Assert.Equal(0, h.Runtime.ShutdownCount);
    }

    [Fact]
    public void OneConsumerReleasingDoesNotTearDownTheOther()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Consent.Approve(PrivacyHook.VrTrackerBattery);

        var perf = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        using var tracker = h.Service.AcquireLease(PrivacyHook.VrTrackerBattery, "tracker");
        _ = h.Service.Compositor;
        Assert.True(h.Service.IsAttached);

        perf.Dispose();

        Assert.Equal(0, h.Runtime.ShutdownCount);
        Assert.True(h.Service.IsAttached);
    }

    [Fact]
    public void TheLastConsumerReleasingShutsDownTheSession()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;

        lease.Dispose();

        Assert.Equal(1, h.Runtime.ShutdownCount);
        Assert.False(h.Service.IsAttached);
    }

    [Fact]
    public void DisposingALeaseTwiceOnlyReleasesOnce()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Consent.Approve(PrivacyHook.VrTrackerBattery);

        var perf = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        using var tracker = h.Service.AcquireLease(PrivacyHook.VrTrackerBattery, "tracker");
        _ = h.Service.Compositor;

        perf.Dispose();
        perf.Dispose();

        Assert.True(h.Service.IsAttached);
        Assert.Equal(0, h.Runtime.ShutdownCount);
    }

    [Fact]
    public void NoConsentMeansNoSessionEvenWithALease()
    {
        var h = new Harness();
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");

        _ = h.Service.Compositor;

        Assert.False(h.Service.IsAttached);
        Assert.Equal(0, h.Runtime.InitCount);
    }

    [Fact]
    public void RevokingConsentDetachesTheSession()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;
        Assert.True(h.Service.IsAttached);

        h.Consent.Deny(PrivacyHook.VrPerformance);

        Assert.False(h.Service.IsAttached);
        Assert.Equal(1, h.Runtime.ShutdownCount);
    }

    [Fact]
    public void VrStoppingDetachesAndRestartingReattaches()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;
        Assert.True(h.Service.IsAttached);

        h.AppState.IsVRRunning = false;
        Assert.False(h.Service.IsAttached);
        Assert.Equal(1, h.Runtime.ShutdownCount);

        h.AppState.IsVRRunning = true;
        _ = h.Service.Compositor;
        Assert.True(h.Service.IsAttached);
        Assert.Equal(2, h.Runtime.InitCount);
    }

    [Fact]
    public void SteamVrNotRunningIsRetriedOnACooldownRatherThanEveryRead()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Runtime.NextError = EVRInitError.Init_NoServerForBackgroundApp;
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");

        for (int i = 0; i < 20; i++)
            _ = h.Service.Compositor;

        Assert.Equal(1, h.Runtime.InitCount);

        h.Now = h.Now.AddSeconds(6);
        _ = h.Service.Compositor;
        Assert.Equal(2, h.Runtime.InitCount);
    }

    [Fact]
    public void AThrowingRuntimeIsSwallowedAndRetriedLater()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Runtime.ThrowOnInit = new InvalidOperationException("openvr_api missing");
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");

        _ = h.Service.Compositor;
        Assert.False(h.Service.IsAttached);

        h.Now = h.Now.AddSeconds(6);
        h.Runtime.ThrowOnInit = null;
        _ = h.Service.Compositor;

        Assert.True(h.Service.IsAttached);
    }

    [Fact]
    public void DisposeReleasesTheSession()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        _ = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;

        h.Service.Dispose();

        Assert.False(h.Service.IsAttached);
        Assert.Equal(1, h.Runtime.ShutdownCount);
    }

    [Fact]
    public void StatusExplainsWhyItIsNotAttached()
    {
        var h = new Harness(approved: PrivacyHook.VrPerformance);
        h.Runtime.NextError = EVRInitError.Init_NoServerForBackgroundApp;
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");
        _ = h.Service.Compositor;

        string status = h.Service.DescribeStatus();

        Assert.Contains("not attached", status);
        Assert.Contains("SteamVR is not running", status);
        Assert.Contains("perf", status);
    }

    [Fact]
    public void StatusNamesConsumersLackingConsent()
    {
        var h = new Harness();
        using var lease = h.Service.AcquireLease(PrivacyHook.VrPerformance, "perf");

        Assert.Contains("no consent", h.Service.DescribeStatus());
    }
}
