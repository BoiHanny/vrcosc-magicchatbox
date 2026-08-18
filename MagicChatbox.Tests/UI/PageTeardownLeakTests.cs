using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.UI.Pages;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// A page that is destroyed on the way out, against a view model that is not.
/// </summary>
/// <remarks>
/// Destroying pages is only worth doing if nothing still points at them. The page view models are
/// DI singletons, so a handler a page hands one and never takes back outlives every visit: the tree
/// stays in memory, the handler keeps running against a page nobody can see, and because a fresh
/// page is built each time, it happens again on the next visit. That is strictly worse than leaving
/// the page alone.
///
/// This asserts on the subscriber list rather than on a weak reference and a collect. The weak
/// reference version was written first and did find something - keyboard focus pinning the torn-down
/// page, which LazyPageHost now releases - but it only held while its own class ran alone. Enough
/// residue survives between test classes sharing one WPF host to root a page for reasons that have
/// nothing to do with this code, and a test that fails for reasons it is not about is worse than no
/// test at all. The subscriber list is the leak itself, and it is exact.
/// </remarks>
public class PageTeardownLeakTests
{
    [Fact]
    public void Leaving_the_page_takes_its_handler_off_the_view_model()
    {
        ChattingPageViewModel vm = BuildChatViewModel();

        Exception? failure = WpfHost.Run(() =>
        {
            var window = new Window
            {
                Width = 900,
                Height = 620,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Left = -20000,
                Top = -20000,
            };

            var host = new System.Windows.Controls.Decorator();
            window.Content = host;

            try
            {
                window.Show();
                host.Child = new ChattingPage { DataContext = vm };
                window.UpdateLayout();
                PumpUnloaded();

                Assert.NotNull(Subscribers(vm));

                host.Child = null;
                window.UpdateLayout();
                PumpUnloaded();

                Assert.Null(Subscribers(vm));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(failure == null, "a handler outlived the page: " + failure);
    }

    // WPF raises Unloaded as a queued dispatcher operation rather than inline with the tree change,
    // and this host thread runs no message loop of its own, so without draining the queue the detach
    // has simply not happened yet and the page looks leaked when it is not.
    private static void PumpUnloaded()
    {
        for (int i = 0; i < 8; i++)
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => { }));
        }
    }

    // The event is only raised from inside the view model, so the subscriber list itself is the
    // thing to assert on: a page that did not take its handler back leaves a delegate here.
    private static Delegate? Subscribers(ChattingPageViewModel vm)
    {
        System.Reflection.FieldInfo? field = typeof(ChattingPageViewModel).GetField(
            "ScrollToEndRequested",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        return (Delegate?)field!.GetValue(vm);
    }

    private static ChattingPageViewModel BuildChatViewModel()
        => new(
            new ChatStatusDisplayState(),
            new FakeAppState(),
            Unused<IModuleHost>(),
            new StubSettingsProvider<ChatSettings>(new ChatSettings()),
            new StubSettingsProvider<TtsSettings>(new TtsSettings()),
            Unused<ScanLoopService>(),
            Unused<OSCController>(),
            Unused<IChatHistoryService>(),
            Unused<IAudioService>(),
            Unused<IOscSender>(),
            Unused<ITtsPlaybackService>(),
            new Lazy<ILiveTypingService>(() => new IdleLiveTyping()),
            null!,
            new InlineDispatcher());

    // Every one of these is behind a Lazy the page never forces by being built and dropped. Throwing
    // rather than stubbing keeps the test honest: if a teardown path ever does reach one, it fails
    // here instead of quietly passing against a stand-in.
    private static Lazy<T> Unused<T>() where T : class
        => new(() => throw new NotSupportedException(typeof(T).Name + " should not be needed to build and drop a page"));

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class FakeAppState : IAppState
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public PulsoidAuthState PulsoidAuthState { get; set; } = PulsoidAuthState.NoToken;
        public int MainWindowBlurEffect { get; set; }
    }

    private sealed class IdleLiveTyping : ILiveTypingService
    {
        public event Action? FinalizeRequested { add { } remove { } }
        public bool IsHolding => false;
        public void Show(string text) { }
        public void Release(bool clearChatbox) { }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }
}
