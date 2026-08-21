using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Voicemod;
using vrcosc_magicchatbox.UI.Controls.Voicemod;
using vrcosc_magicchatbox.ViewModels.Sections;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.UI;

public class VoicemodControlPanelTests
{
    [Fact]
    public void Holding_and_releasing_the_bleep_key_sends_start_then_stop()
    {
        // Getting stuck bleeping is the worst failure this control can have, so the pairing matters
        // more than either half on its own.
        var harness = new Harness();

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            panel =>
            {
                var button = (Button)panel.FindName("BleepButton")!;
                RaiseMouse(button, down: true);
                RaiseMouse(button, down: false);
                WaitForCount(() => harness.BadLanguageValues.Count, 2);
            });

        Assert.True(failure == null, "the bleep button could not be driven: " + failure);
        Assert.Equal(new[] { 1, 0 }, harness.BadLanguageValues);
    }

    [Fact]
    public void A_repeated_key_does_not_send_a_second_start()
    {
        var harness = new Harness();

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            panel =>
            {
                var button = (Button)panel.FindName("BleepButton")!;
                RaiseMouse(button, down: true);
                RaiseMouse(button, down: true);
                RaiseMouse(button, down: false);
                WaitForCount(() => harness.BadLanguageValues.Count, 2);
            });

        Assert.True(failure == null, "the bleep button could not be driven: " + failure);
        Assert.Equal(new[] { 1, 0 }, harness.BadLanguageValues);
    }

    [Fact]
    public void Every_sound_blob_realizes_its_template()
    {
        // A blob's template only ever runs when there is at least one sound to render, so a panel
        // built against an empty board proves nothing. A StaticResource that resolves nowhere throws
        // here and nowhere earlier - not at build, not on an empty panel.
        var harness = new Harness();
        harness.SeedSounds("Air horn", "Nuke alert", "Bruh");

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            panel =>
            {
                panel.UpdateLayout();
                int blobs = CountVisualChildren<Button>(panel, b => b.Tag as string != "chrome");
                Assert.True(blobs > 0, "no buttons were realized at all");
            });

        Assert.True(failure == null, "a sound blob failed to build: " + failure);
        Assert.Equal(3, harness.ViewModel.FilteredSounds.Count);
    }

    [Fact]
    public void Pinning_a_sound_moves_it_to_the_front_and_survives_a_rebuild()
    {
        var harness = new Harness();
        harness.SeedSounds("Zebra", "Apple", "Mango");

        VoicemodSoundItem mango = harness.ViewModel.FilteredSounds.Single(s => s.Name == "Mango");
        harness.ViewModel.ToggleSoundFavoriteCommand.Execute(mango);

        Assert.Equal("Mango", harness.ViewModel.FilteredSounds[0].Name);
        Assert.True(harness.ViewModel.FilteredSounds[0].IsFavorite);
        Assert.Contains("Mango", harness.Settings.FavoriteSoundIds);
    }

    [Fact]
    public void Pinned_sounds_stay_reachable_no_matter_which_board_is_selected()
    {
        // The whole point of the favourites scope: a sound pinned on board A has to still be one
        // click away while board B is the one showing.
        var harness = new Harness();
        harness.SeedBoards(
            ("board-a", "Board A", ["Airhorn", "Vine boom"]),
            ("board-b", "Board B", ["Sad trombone"]));

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Board A"));
        vm.ToggleSoundFavoriteCommand.Execute(vm.FilteredSounds.Single(s => s.Name == "Airhorn"));

        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Board B"));
        Assert.DoesNotContain(vm.FilteredSounds, s => s.Name == "Airhorn");

        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes[0]);
        Assert.True(vm.ShowingFavorites);
        Assert.Equal("Airhorn", Assert.Single(vm.FilteredSounds).Name);
    }

    [Fact]
    public void A_long_board_is_paged_rather_than_dumped_in_one_wall()
    {
        var harness = new Harness();
        harness.SeedSounds(Enumerable.Range(1, 60).Select(i => $"Sound {i:D2}").ToArray());

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));

        Assert.Equal(vm.SoundsPerPage, vm.FilteredSounds.Count);
        Assert.Equal(3, vm.SoundPageCount);
        Assert.True(vm.HasMultipleSoundPages);
        Assert.False(vm.CanGoToPreviousSoundPage);

        vm.NextSoundPageCommand.Execute(null);
        vm.NextSoundPageCommand.Execute(null);

        Assert.Equal("3 / 3", vm.SoundPageText);
        Assert.Equal(60 - (2 * vm.SoundsPerPage), vm.FilteredSounds.Count);
        Assert.False(vm.CanGoToNextSoundPage);
    }

    [Fact]
    public void The_page_size_setting_changes_how_many_sounds_a_page_holds()
    {
        var harness = new Harness();
        harness.SeedSounds(Enumerable.Range(1, 60).Select(i => $"Sound {i:D2}").ToArray());

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));

        harness.Settings.SoundsPerPage = 8;

        Assert.Equal(8, vm.FilteredSounds.Count);
        Assert.Equal(8, vm.SoundPageCount);
        Assert.Equal(0, vm.SoundPageIndex);
    }

    [Fact]
    public void Turning_thumbnails_off_drops_the_ones_already_loaded()
    {
        var harness = new Harness();
        harness.SeedSounds("Airhorn", "Vine boom");

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));

        harness.Settings.ShowSoundThumbnails = false;

        Assert.All(vm.FilteredSounds, item => Assert.False(item.HasArtwork));
    }

    [Fact]
    public void Compact_mode_shrinks_the_blobs()
    {
        var harness = new Harness();
        VoicemodSectionViewModel vm = harness.ViewModel;

        double roomyHeight = vm.SoundBlobHeight;
        double roomyWidth = vm.SoundBlobMaxWidth;
        harness.Settings.CompactSoundBlobs = true;

        Assert.True(vm.SoundBlobHeight < roomyHeight);
        Assert.True(vm.SoundBlobMaxWidth < roomyWidth);
    }

    [Fact]
    public void The_page_size_setting_is_clamped_to_something_usable()
    {
        var settings = new VoicemodSettings { SoundsPerPage = 5000 };
        Assert.Equal(VoicemodSettings.MaximumSoundsPerPage, settings.SoundsPerPage);

        settings.SoundsPerPage = 0;
        Assert.Equal(VoicemodSettings.MinimumSoundsPerPage, settings.SoundsPerPage);
    }

    [Fact]
    public void Sorting_by_name_offers_a_letter_jump_that_lands_on_the_right_page()
    {
        // 70 pages of sounds is not navigable by paging alone; the letter row is what makes an
        // alphabetical library usable.
        var harness = new Harness();
        harness.SeedSounds(
            [.. Enumerable.Range(1, 30).Select(i => $"A{i:D2}"),
             .. Enumerable.Range(1, 30).Select(i => $"Z{i:D2}")]);

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));
        if (vm.SoundSortButtonText != "A-Z")
            vm.CycleSoundSortCommand.Execute(null);

        Assert.True(vm.ShowsAlphabetJumps);
        VoicemodAlphabetJump z = vm.AlphabetJumps.Single(j => j.Letter == "Z");

        vm.JumpToAlphabetCommand.Execute(z);

        Assert.Equal(z.PageIndex, vm.SoundPageIndex);
        Assert.Contains(vm.FilteredSounds, s => s.Name.StartsWith('Z'));
    }

    [Fact]
    public void Playing_a_sound_moves_it_to_the_front_straight_away_under_recent_sort()
    {
        // Recording the play into settings is not enough - the list has to be rebuilt or the sound
        // only appears to move the next time something else happens to trigger one.
        var harness = new Harness();
        harness.SeedSounds("Apple", "Banana", "Cherry");

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));
        if (vm.SoundSortButtonText != "Recent")
            vm.CycleSoundSortCommand.Execute(null);

        VoicemodSoundItem cherry = vm.FilteredSounds.Single(s => s.Name == "Cherry");
        vm.PlaySoundCommand.Execute(cherry);

        Assert.Equal("Cherry", vm.FilteredSounds[0].Name);
        Assert.Equal("Cherry", harness.Settings.RecentSoundIds[0]);
    }

    [Fact]
    public void Playing_a_sound_leaves_alphabetical_order_alone()
    {
        var harness = new Harness();
        harness.SeedSounds("Apple", "Banana", "Cherry");

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));
        if (vm.SoundSortButtonText != "A-Z")
            vm.CycleSoundSortCommand.Execute(null);

        vm.PlaySoundCommand.Execute(vm.FilteredSounds.Single(s => s.Name == "Cherry"));

        Assert.Equal("Apple", vm.FilteredSounds[0].Name);
    }

    [Fact]
    public void Sorting_by_recent_hides_the_letter_jump()
    {
        var harness = new Harness();
        harness.SeedSounds("Apple", "Banana", "Cherry");

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Test board"));
        if (vm.SoundSortButtonText != "Recent")
            vm.CycleSoundSortCommand.Execute(null);

        Assert.False(vm.ShowsAlphabetJumps);
        Assert.Empty(vm.AlphabetJumps);
    }

    [Fact]
    public void Changing_scope_returns_to_the_first_page()
    {
        var harness = new Harness();
        harness.SeedBoards(
            ("board-a", "Board A", Enumerable.Range(1, 60).Select(i => $"A{i:D2}").ToArray()),
            ("board-b", "Board B", ["Only one"]));

        VoicemodSectionViewModel vm = harness.ViewModel;
        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Board A"));
        vm.NextSoundPageCommand.Execute(null);
        Assert.Equal(1, vm.SoundPageIndex);

        vm.SelectSoundScopeCommand.Execute(vm.SoundScopes.Single(s => s.Label == "Board B"));

        Assert.Equal(0, vm.SoundPageIndex);
        Assert.Equal("1 / 1", vm.SoundPageText);
    }

    private static int CountVisualChildren<T>(DependencyObject root, Func<T, bool> predicate)
        where T : DependencyObject
    {
        int count = 0;
        int children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < children; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed && predicate(typed))
                count++;
            count += CountVisualChildren(child, predicate);
        }

        return count;
    }

    [Fact]
    public void Nothing_is_sent_until_the_button_is_actually_pressed()
    {
        var harness = new Harness();

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            _ => Thread.Sleep(120));

        Assert.True(failure == null, "the panel did not build: " + failure);
        Assert.Empty(harness.BadLanguageValues);
    }

    [Fact]
    public void Losing_mouse_capture_mid_hold_releases_the_bleep()
    {
        // The capture can be yanked away by anything - an alt-tab, another window stealing focus.
        // Whatever the cause, the stop command still has to go out.
        var harness = new Harness();

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            panel =>
            {
                var button = (Button)panel.FindName("BleepButton")!;
                RaiseMouse(button, down: true);
                WaitForCount(() => harness.BadLanguageValues.Count, 1);

                button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0)
                {
                    RoutedEvent = UIElement.LostMouseCaptureEvent,
                });
                WaitForCount(() => harness.BadLanguageValues.Count, 2);
            });

        Assert.True(failure == null, "the bleep button could not be driven: " + failure);
        Assert.Equal(new[] { 1, 0 }, harness.BadLanguageValues);
    }

    [Fact]
    public void Unloading_the_panel_while_held_still_releases_the_bleep()
    {
        var harness = new Harness();

        Exception? failure = WpfHost.RunInWindow(
            () => new VoicemodControlPanel { ViewModel = harness.ViewModel },
            panel =>
            {
                var button = (Button)panel.FindName("BleepButton")!;
                RaiseMouse(button, down: true);

                // Tearing the panel out of the tree mid-hold is the case a user hits by navigating
                // away with the key still down.
                WaitForCount(() => harness.BadLanguageValues.Count, 1);

                var host = (Window)Window.GetWindow(panel)!;
                host.Content = null;

                // Unloaded is raised through the dispatcher queue, and the test window has no
                // message loop of its own, so the queue has to be drained by hand.
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Loaded);
                WaitForCount(() => harness.BadLanguageValues.Count, 2);
            });

        Assert.True(failure == null, "the panel could not be unloaded: " + failure);
        Assert.Equal(new[] { 1, 0 }, harness.BadLanguageValues);
    }

    // The test window is never shown, so it has no PresentationSource and KeyEventArgs cannot be
    // built. Mouse events need only the device, and press-and-hold with the mouse is the path
    // people actually use anyway.
    private static void RaiseMouse(IInputElement target, bool down)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = down
                ? UIElement.PreviewMouseLeftButtonDownEvent
                : UIElement.PreviewMouseLeftButtonUpEvent,
        };
        target.RaiseEvent(args);
    }

    /// <summary>
    /// The commands are async, so the socket write lands after the handler returns. Everything runs
    /// with ConfigureAwait(false), which matters because the test window has no dispatcher loop.
    /// </summary>
    private static void WaitForCount(Func<int> actual, int expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (actual() < expected && DateTime.UtcNow < deadline)
            Thread.Sleep(10);
    }

    /// <summary>Builds a real view-model over a scripted socket so commands reach real protocol writes.</summary>
    private sealed class Harness
    {
        private readonly RecordingSocket _socket = new();

        public VoicemodSectionViewModel ViewModel { get; }

        public VoicemodSettings Settings { get; }

        public VoicemodDisplayState Display { get; }

        public IReadOnlyList<int> BadLanguageValues => _socket.BadLanguageValues;

        public void SeedSounds(params string[] names)
            => SeedBoards(("board-1", "Test board", names));

        public void SeedBoards(params (string Id, string Name, string[] Sounds)[] boards)
        {
            Display.ReplaceSoundboards(
                boards.Select(board => new VoicemodSoundboard(
                    board.Id,
                    board.Name,
                    Enabled: true,
                    IsCustom: false,
                    ShowProLogo: false,
                    board.Sounds.Select(Sound).ToArray())).ToArray());
        }

        private static VoicemodSound Sound(string name) => new(
            name,
            name,
            Enabled: true,
            IsCustom: false,
            PlaybackMode: "PlayRestart",
            Loop: false,
            MuteOtherSounds: false,
            MuteVoice: false,
            StopOtherSounds: false,
            ShowProLogo: false,
            BitmapChecksum: string.Empty);

        public Harness()
        {
            var display = new VoicemodDisplayState();
            Display = display;
            var integrations = new IntegrationSettings { IntgrVoicemod = true };
            var features = new VoicemodSettings();
            Settings = features;
            var artwork = new VoicemodArtworkCache();

            var module = new VoicemodModule(
                new StubProvider<IntegrationSettings>(integrations),
                new StubProvider<VoicemodSettings>(features),
                display,
                new StubKeyProvider(),
                new SingleSocketFactory(_socket),
                new InlineDispatcher(),
                new ApprovedConsent(),
                artwork);

            var host = new ModuleHost { Voicemod = module };
            module.StartAsync().GetAwaiter().GetResult();
            WaitForConnected(display);

            ViewModel = new VoicemodSectionViewModel(
                new Lazy<IModuleHost>(() => host),
                new StubKeyProvider(),
                display,
                new StubProvider<IntegrationSettings>(integrations),
                new StubProvider<AppSettings>(new AppSettings()),
                new StubProvider<VoicemodSettings>(features),
                new ApprovedConsent(),
                new NoOpMenuNavigation(),
                new NoOpToast(),
                artwork);
        }

        private static void WaitForConnected(VoicemodDisplayState display)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (display.ConnectionState != VoicemodConnectionState.Connected
                   && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
        }
    }

    private sealed class RecordingSocket : IVoicemodSocket
    {
        private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
        private readonly List<int> _badLanguageValues = new();

        public IReadOnlyList<int> BadLanguageValues
        {
            get
            {
                lock (_badLanguageValues)
                    return _badLanguageValues.ToArray();
            }
        }

        public WebSocketState State { get; private set; } = WebSocketState.None;

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            State = WebSocketState.Open;
            return Task.CompletedTask;
        }

        public async Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            using var document = System.Text.Json.JsonDocument.Parse(message);
            string action = document.RootElement.GetProperty("action").GetString()!;

            if (action == "setBeepSound")
            {
                int value = document.RootElement
                    .GetProperty("payload")
                    .GetProperty("badLanguage")
                    .GetInt32();
                lock (_badLanguageValues)
                    _badLanguageValues.Add(value);
                return;
            }

            if (action != "registerClient")
                return;

            await _incoming.Writer.WriteAsync(
                """
                {
                  "action": "registerClient",
                  "payload": { "status": { "code": 200, "description": "Authorized" } }
                }
                """,
                cancellationToken);
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _incoming.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            State = WebSocketState.Closed;
            _incoming.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            State = WebSocketState.Closed;
            _incoming.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SingleSocketFactory(IVoicemodSocket socket) : IVoicemodSocketFactory
    {
        public IVoicemodSocket Create() => socket;
    }

    private sealed class StubProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public event EventHandler? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        public void FlushPendingSave() { }
        public void Reload() { }
    }

    private sealed class StubKeyProvider : IVoicemodClientKeyProvider
    {
        public bool HasLocalClientKey => false;

        public bool TryGetClientKey(out string clientKey)
        {
            clientKey = "test-client-key";
            return true;
        }

        public void SaveLocalClientKey(string clientKey) { }
        public void ClearLocalClientKey() { }
    }

    private sealed class ApprovedConsent : IPrivacyConsentService
    {
        public event EventHandler<ConsentChangedEventArgs>? ConsentChanged;

        public bool IsApproved(PrivacyHook hook) => hook == PrivacyHook.VoicemodControl;
        public ConsentState GetState(PrivacyHook hook) =>
            IsApproved(hook) ? ConsentState.Approved : ConsentState.Denied;
        public void Approve(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Approved));
        public void Deny(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Denied));
        public void Reset(PrivacyHook hook) =>
            ConsentChanged?.Invoke(this, new ConsentChangedEventArgs(hook, ConsentState.Unknown));
        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks) =>
            hooks.Where(hook => !IsApproved(hook)).ToArray();
    }

    private sealed class InlineDispatcher : IUiDispatcher
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

    private sealed class NoOpToast : IToastService
    {
        public System.Collections.ObjectModel.ObservableCollection<ToastItemViewModel> Toasts { get; } = new();

        public void Show(
            string title,
            string message,
            ToastType type = ToastType.Info,
            ToastAction? action = null,
            int durationMs = 5000,
            string? key = null)
        {
        }

        public void Dismiss(ToastItemViewModel item) { }
    }

    private sealed class NoOpMenuNavigation : IMenuNavigationService
    {
        public void ActivateSetting(string settingName) { }
        public void NavigateToPage(int pageIndex) { }
        public void NavigateBack() { }
        public void NavigateForward() { }
        public void NavigateToPrivacy() { }
    }
}
