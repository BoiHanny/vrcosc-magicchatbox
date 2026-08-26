using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Vr;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Services.Vr;

public class SteamVrAutoStartServiceTests : IDisposable
{
    private const string SteamVrProcessName = "vrmonitor";

    /// <summary>Long enough that a loaded build agent still gets there.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(5);

    /// <summary>Only ever used to prove that nothing arrives.</summary>
    private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(500);

    private readonly string _root;

    public SteamVrAutoStartServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mcb-steamvr-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private Harness NewHarness() => new(_root);

    [Fact]
    public void Starting_with_the_setting_off_takes_the_registration_away()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = false;

        h.Service.Start();
        h.WaitForReconcile();

        Assert.Empty(h.Applications.Registers);
        Assert.Single(h.Applications.Unregisters);
    }

    [Fact]
    public void Starting_with_the_setting_on_writes_the_manifest_and_registers_it()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;

        h.Service.Start();
        h.WaitForReconcile();

        Call call = Assert.Single(h.Applications.Registers);
        Assert.Equal(h.ManifestPath, call.ManifestPath);
        Assert.Equal("boihanny.magicchatbox", call.AppKey);
        Assert.StartsWith(h.LocalAppData, call.ManifestPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(h.ManifestPath));
        Assert.Empty(h.Applications.Unregisters);
    }

    [Fact]
    public void A_registration_that_worked_is_written_down_for_next_time()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;

        h.Service.Start();
        h.WaitForReconcile();

        Assert.Equal(h.ManifestPath, h.Settings.SteamVrManifestPath);
    }

    [Fact]
    public void Unregistering_clears_the_written_down_path_and_removes_the_manifest()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Service.Start();
        h.WaitForReconcile();
        Assert.True(File.Exists(h.ManifestPath));

        h.Settings.StartWithSteamVr = false;
        h.WaitForReconcile();

        Assert.Equal(string.Empty, h.Settings.SteamVrManifestPath);
        Assert.False(File.Exists(h.ManifestPath));
    }

    [Fact]
    public void Flipping_the_setting_reconciles_again_without_a_restart()
    {
        using var h = NewHarness();
        h.Service.Start();
        h.WaitForReconcile();
        Assert.Single(h.Applications.Unregisters);

        h.Settings.StartWithSteamVr = true;
        h.WaitForReconcile();
        Assert.Single(h.Applications.Registers);

        h.Settings.StartWithSteamVr = false;
        h.WaitForReconcile();
        Assert.Equal(2, h.Applications.Unregisters.Count);
    }

    [Fact]
    public void Unregistering_follows_the_path_SteamVR_was_actually_given()
    {
        // SteamVR keys removal on the absolute path it was registered with. A copy that has moved
        // since then computes a different path, so handing back the recorded one is the only thing
        // that stops the old entry outliving the setting.
        using var h = NewHarness();
        string stale = Path.Combine(h.LocalAppData, "moved", SteamVrManifest.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "{}");

        h.Settings.StartWithSteamVr = false;
        h.Settings.SteamVrManifestPath = stale;

        h.Service.Start();
        h.WaitForReconcile();

        Call call = Assert.Single(h.Applications.Unregisters);
        Assert.Equal(stale, call.ManifestPath);
        Assert.NotEqual(h.ManifestPath, call.ManifestPath);
        Assert.False(File.Exists(stale));
    }

    [Fact]
    public void SteamVR_not_being_up_defers_the_change_rather_than_undoing_it()
    {
        // Nothing failed, so the recorded path has to stand: clearing it here would lose the only
        // handle on an entry SteamVR is still holding.
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = false;
        h.Settings.SteamVrManifestPath = h.ManifestPath;
        h.Applications.NextResult = SteamVrResult.Unavailable("no session");

        h.Service.Start();
        h.WaitForReconcile();

        Assert.Single(h.Applications.Unregisters);
        Assert.Equal(h.ManifestPath, h.Settings.SteamVrManifestPath);
    }

    [Fact]
    public void A_deferred_registration_is_retried_when_SteamVR_turns_up()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Applications.NextResult = SteamVrResult.Unavailable("no session");

        h.Service.Start();
        h.WaitForReconcile();
        Assert.Single(h.Applications.Registers);
        Assert.Equal(string.Empty, h.Settings.SteamVrManifestPath);

        h.Applications.NextResult = SteamVrResult.Done();
        h.Processes.SetRunning(SteamVrProcessName, true);
        h.AppState.IsVRRunning = true;
        h.WaitForReconcile();

        Assert.Equal(2, h.Applications.Registers.Count);
        Assert.Equal(h.ManifestPath, h.Settings.SteamVrManifestPath);
    }

    [Fact]
    public void Closing_SteamVR_closes_the_app_once()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Settings.QuitWithSteamVr = true;
        h.Processes.SetRunning(SteamVrProcessName, true);

        h.Service.Start();
        h.WaitForReconcile();

        h.AppState.IsVRRunning = true;
        h.Processes.SetRunning(SteamVrProcessName, false);
        h.AppState.IsVRRunning = false;

        Assert.Equal(1, h.ShutdownRequests);

        // A later session that never gets as far as SteamVR must not ask again.
        h.AppState.IsVRRunning = true;
        h.AppState.IsVRRunning = false;

        Assert.Equal(1, h.ShutdownRequests);
    }

    [Fact]
    public void The_app_stays_open_when_quitting_with_SteamVR_is_off()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Settings.QuitWithSteamVr = false;
        h.Processes.SetRunning(SteamVrProcessName, true);

        h.Service.Start();
        h.WaitForReconcile();

        h.AppState.IsVRRunning = true;
        h.Processes.SetRunning(SteamVrProcessName, false);
        h.AppState.IsVRRunning = false;

        Assert.Equal(0, h.ShutdownRequests);
    }

    [Fact]
    public void The_app_stays_open_when_only_the_headset_stopped()
    {
        // IsVRRunning also covers the Oculus runtime. SteamVR itself is still up, so closing here
        // would take the app down while the user is still in VR.
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Settings.QuitWithSteamVr = true;
        h.Processes.SetRunning(SteamVrProcessName, true);

        h.Service.Start();
        h.WaitForReconcile();

        h.AppState.IsVRRunning = true;
        h.AppState.IsVRRunning = false;

        Assert.Equal(0, h.ShutdownRequests);
    }

    [Fact]
    public void Stopping_the_service_stops_it_listening()
    {
        using var h = NewHarness();
        h.Service.Start();
        h.WaitForReconcile();

        h.Service.Stop();
        h.Settings.StartWithSteamVr = true;

        Assert.False(h.Applications.WaitForCall(Brief));
        Assert.Empty(h.Applications.Registers);
    }

    [Fact]
    public void Disposing_the_service_stops_it_listening()
    {
        using var h = NewHarness();
        h.Settings.StartWithSteamVr = true;
        h.Settings.QuitWithSteamVr = true;
        h.Processes.SetRunning(SteamVrProcessName, true);
        h.Service.Start();
        h.WaitForReconcile();

        h.AppState.IsVRRunning = true;
        h.Service.Dispose();

        h.Settings.StartWithSteamVr = false;
        h.Processes.SetRunning(SteamVrProcessName, false);
        h.AppState.IsVRRunning = false;

        Assert.False(h.Applications.WaitForCall(Brief));
        Assert.Single(h.Applications.Registers);
        Assert.Equal(0, h.ShutdownRequests);
    }

    #region Harness

    private sealed record Call(string ManifestPath, string AppKey);

    private sealed class Harness : IDisposable
    {
        /// <summary>
        /// The re-entrancy guard the service uses to drop overlapping reconciles. It is cleared
        /// after the fake has already been called, so a flip made before it clears is swallowed.
        /// </summary>
        private static readonly FieldInfo ReconcileFlag =
            typeof(SteamVrAutoStartService).GetField("_reconcileInProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private int _shutdownRequests;

        public Harness(string root)
        {
            LocalAppData = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(LocalAppData);

            ExecutablePath = Path.Combine(LocalAppData, "MagicChatbox.exe");
            File.WriteAllText(ExecutablePath, "exe");

            ManifestPath = SteamVrManifest.PathFor(LocalAppData);

            Service = new SteamVrAutoStartService(
                Applications,
                new FakeSettingsProvider(Settings),
                AppState,
                Processes,
                () => Interlocked.Increment(ref _shutdownRequests),
                LocalAppData,
                () => ExecutablePath);
        }

        public string LocalAppData { get; }

        public string ExecutablePath { get; }

        public string ManifestPath { get; }

        public FakeSteamVrApplications Applications { get; } = new();

        public AppSettings Settings { get; } = new();

        public FakeAppState AppState { get; } = new();

        public FakeProcessPresence Processes { get; } = new();

        public SteamVrAutoStartService Service { get; }

        public int ShutdownRequests => Volatile.Read(ref _shutdownRequests);

        /// <summary>
        /// Waits for the reconcile that is running on a background task to reach SteamVR, and then
        /// for it to finish tidying up afterwards.
        /// </summary>
        public void WaitForReconcile()
        {
            Assert.True(Applications.WaitForCall(Generous), "SteamVR was never asked to reconcile.");

            // The fake is signalled from inside the reconcile, which still has to record the
            // manifest path, delete files and release its guard. Everything after the call is
            // in-memory work, so this settles immediately in practice.
            Assert.True(
                SpinWait.SpinUntil(() => (int)ReconcileFlag.GetValue(Service)! == 0, Generous),
                "The reconcile never finished.");
        }

        public void Dispose() => Service.Dispose();
    }

    private sealed class FakeSteamVrApplications : ISteamVrApplications
    {
        private readonly SemaphoreSlim _calls = new(0);
        private readonly object _gate = new();
        private readonly List<Call> _registers = new();
        private readonly List<Call> _unregisters = new();

        public SteamVrResult NextResult { get; set; } = SteamVrResult.Done();

        public IReadOnlyList<Call> Registers
        {
            get { lock (_gate) return _registers.ToList(); }
        }

        public IReadOnlyList<Call> Unregisters
        {
            get { lock (_gate) return _unregisters.ToList(); }
        }

        public SteamVrResult Register(string manifestPath, string appKey)
        {
            lock (_gate)
            {
                _registers.Add(new Call(manifestPath, appKey));
            }

            _calls.Release();
            return NextResult;
        }

        public SteamVrResult Unregister(string manifestPath, string appKey)
        {
            lock (_gate)
            {
                _unregisters.Add(new Call(manifestPath, appKey));
            }

            _calls.Release();
            return NextResult;
        }

        public bool IsAutoLaunchEnabled(string appKey) => true;

        public bool WaitForCall(TimeSpan timeout) => _calls.Wait(timeout);
    }

    private sealed class FakeProcessPresence : IProcessPresenceService
    {
        private readonly Dictionary<string, bool> _running = new(StringComparer.OrdinalIgnoreCase);

        public bool IsRunning(string processName)
            => _running.TryGetValue(processName, out bool running) && running;

        public void SetRunning(string processName, bool running) => _running[processName] = running;

        public void Invalidate(string processName) { }

        public void InvalidateAll() { }
    }

    private sealed class FakeSettingsProvider : ISettingsProvider<AppSettings>
    {
        public FakeSettingsProvider(AppSettings value) => Value = value;

        public AppSettings Value { get; }

        public void Save() { }

        public void FlushPendingSave() { }

        public void Reload() { }

        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class FakeAppState : IAppState
    {
        private bool _isVRRunning;

        public bool MasterSwitch { get; set; } = true;

        public bool BussyBoysMode { get; set; }

        public bool Egg_Dev { get; set; }

        public bool PulsoidAuthConnected { get; set; }

        public PulsoidAuthState PulsoidAuthState { get; set; }

        public int MainWindowBlurEffect { get; set; }

        public bool IsVRRunning
        {
            get => _isVRRunning;
            set
            {
                if (_isVRRunning == value)
                    return;

                _isVRRunning = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVRRunning)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    #endregion
}
