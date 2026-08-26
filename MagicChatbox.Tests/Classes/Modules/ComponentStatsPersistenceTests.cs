using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class ComponentStatsPersistenceTests : IDisposable
{
    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T>
        where T : class, new()
    {
        public T Value { get; } = value;

        public int SaveCount { get; private set; }

        public event EventHandler SettingsChanged
        {
            add { }
            remove { }
        }

        public void Save() => SaveCount++;

        public void FlushPendingSave() => Save();

        public void Reload() { }
    }

    private sealed class TestEnvironment(string dataPath) : IEnvironmentService
    {
        public string DataPath { get; } = dataPath;

        public string LogPath => DataPath;

        public string VrcPath => DataPath;

        public void SetCustomProfile(int profileNumber) { }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();

        public T Invoke<T>(Func<T> func) => func();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());

        public bool CheckAccess() => true;

        public void BeginInvoke(Action action) => action();

        public void Shutdown() { }
    }

    private sealed class FakeAppState : IAppState
    {
        public bool MasterSwitch { get; set; }

        public bool IsVRRunning { get; set; }

        public bool BussyBoysMode { get; set; }

        public bool Egg_Dev { get; set; }

        public bool PulsoidAuthConnected { get; set; }

        public PulsoidAuthState PulsoidAuthState { get; set; }

        public int MainWindowBlurEffect { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class ApprovedConsent : IPrivacyConsentService
    {
        public bool IsApproved(PrivacyHook hook) => true;

        public ConsentState GetState(PrivacyHook hook) => ConsentState.Approved;

        public void Approve(PrivacyHook hook) { }

        public void Deny(PrivacyHook hook) { }

        public void Reset(PrivacyHook hook) { }

        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks)
            => Array.Empty<PrivacyHook>();

        public event EventHandler<ConsentChangedEventArgs> ConsentChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class NoOpPersistence : IStatePersistenceCoordinator
    {
        public void PersistAllState() { }

        public Task PrepareForShutdownAsync() => Task.CompletedTask;
    }

    private readonly string _dataPath = Path.Combine(
        Path.GetTempPath(),
        $"magicchatbox-component-stats-{Guid.NewGuid():N}");

    [Fact]
    public void Module_save_restores_all_custom_names_after_a_restart()
    {
        Directory.CreateDirectory(_dataPath);
        var settings = new StubSettingsProvider<ComponentStatsSettings>(new ComponentStatsSettings());

        using (ComponentStatsModule module = CreateModule(settings))
        {
            module.LoadComponentStats();
            module.SetCustomHardwareName(StatsComponentType.CPU, "Main processor");
            module.SetCustomHardwareName(StatsComponentType.GPU, "Rendering card");
            module.SetCustomHardwareName(StatsComponentType.RAM, "System memory");
            module.SetCustomHardwareName(StatsComponentType.VRAM, "Graphics memory");

            module.SaveSettings();
        }

        Assert.Equal(1, settings.SaveCount);

        using ComponentStatsModule restored = CreateModule(
            new StubSettingsProvider<ComponentStatsSettings>(new ComponentStatsSettings()));
        restored.LoadComponentStats();

        Assert.Equal("Main processor", restored.GetCustomHardwareName(StatsComponentType.CPU));
        Assert.Equal("Rendering card", restored.GetCustomHardwareName(StatsComponentType.GPU));
        Assert.Equal("System memory", restored.GetCustomHardwareName(StatsComponentType.RAM));
        Assert.Equal("Graphics memory", restored.GetCustomHardwareName(StatsComponentType.VRAM));
    }

    private ComponentStatsModule CreateModule(
        ISettingsProvider<ComponentStatsSettings> componentSettings)
        => new(
            componentSettings,
            new StubSettingsProvider<TimeSettings>(new TimeSettings()),
            new StubSettingsProvider<AppSettings>(new AppSettings()),
            new FakeAppState(),
            new TestEnvironment(_dataPath),
            new IntegrationDisplayState(),
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings()),
            new ImmediateDispatcher(),
            new Lazy<IStatePersistenceCoordinator>(() => new NoOpPersistence()),
            new HardwareMonitorService(),
            new ApprovedConsent());

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }
}
