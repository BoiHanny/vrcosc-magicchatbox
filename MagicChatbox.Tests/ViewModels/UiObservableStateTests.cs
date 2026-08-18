using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

/// <summary>
/// Who is allowed to say the UI is worth updating, and who decides whether a page exists.
/// </summary>
/// <remarks>
/// These started as one flag with two writers - the window set it from its own visibility, and the
/// tray service overwrote it with "window or menu" off a cached copy of the window's half. Showing
/// the main window from the tray menu then raced: the window set the value it already held, so no
/// notification fired to refresh the cache, and closing the menu wrote the stale false back while
/// the window was on screen. Every page host was bound to that flag, so the window came up empty.
///
/// They are two inputs now, each with exactly one writer, and the old flag is derived. A page host
/// follows the window alone - a tray menu is not a reason to build a page nobody can see.
/// </remarks>
public class UiObservableStateTests
{
    [Fact]
    public void The_menu_can_make_the_ui_worth_updating_without_the_window_being_up()
    {
        IAppState state = new FakeState { IsWindowOnScreen = false, IsTrayMenuOpen = true };

        Assert.True(state.IsUiObservable);
        Assert.False(state.IsWindowOnScreen);
    }

    [Fact]
    public void Closing_the_menu_cannot_switch_the_window_off()
    {
        IAppState state = new FakeState();

        // In the tray, menu opened, page picked, window shown, menu closes behind it.
        state.IsWindowOnScreen = false;
        state.IsTrayMenuOpen = true;
        state.IsWindowOnScreen = true;
        state.IsTrayMenuOpen = false;

        Assert.True(state.IsWindowOnScreen);
        Assert.True(state.IsUiObservable);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Observable_is_either_of_the_two(bool window, bool menu, bool expected)
    {
        IAppState state = new FakeState { IsWindowOnScreen = window, IsTrayMenuOpen = menu };

        Assert.Equal(expected, state.IsUiObservable);
    }

    private sealed class FakeState : IAppState
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public PulsoidAuthState PulsoidAuthState { get; set; } = PulsoidAuthState.NoToken;
        public int MainWindowBlurEffect { get; set; }
        public bool IsWindowOnScreen { get; set; } = true;
        public bool IsTrayMenuOpen { get; set; }
    }
}
