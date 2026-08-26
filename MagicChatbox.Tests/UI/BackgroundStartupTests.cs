using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Guards the path taken when "Start hidden in the tray" is on.
/// </summary>
/// <remarks>
/// Nobody sees this path go wrong at the time it goes wrong, which is what makes it worth pinning
/// down here. The window is hidden the moment startup finishes, so a mistake surfaces minutes later
/// when the user opens it from the tray and gets whatever state it was left in.
/// </remarks>
public class BackgroundStartupTests
{
    [Fact]
    public void Starting_hidden_takes_the_startup_overlay_down_with_the_window()
    {
        // The overlay sits at ZIndex 200 over the whole window and swallows clicks until it is
        // collapsed. Hiding the window without dismissing it leaves that frozen sheet waiting, so
        // opening from the tray shows a dead progress bar over a UI that cannot be clicked.
        string branch = BackgroundBranch();

        Assert.Contains("HideStartupOverlay", branch, StringComparison.Ordinal);
    }

    [Fact]
    public void Starting_hidden_does_not_wait_on_a_fade_nobody_can_see()
    {
        // The animated path only collapses the overlay when the fade completes. On a window that is
        // about to be hidden that is a needless dependency on an animation finishing, so this path
        // asks for the immediate one.
        string branch = BackgroundBranch();

        Assert.Matches(@"HideStartupOverlay\(\s*animate\s*:\s*false\s*\)", branch);
    }

    [Fact]
    public void The_tray_icon_exists_before_the_window_is_hidden()
    {
        // With the window hidden the tray icon is the only way back into the app. Created after the
        // hide, any failure in between would leave a running process with no window and no icon.
        string startup = AppStartupSource();

        int trayInit = startup.IndexOf("ITrayIconService>().Initialize(mainWindow)", StringComparison.Ordinal);
        int hiddenBranch = Regex.Match(startup, HiddenBranchPattern) is { Success: true } branch
            ? branch.Index
            : -1;

        Assert.True(trayInit >= 0, "the tray icon is no longer initialized during startup");
        Assert.True(hiddenBranch >= 0, "the start-hidden branch was not found");
        Assert.True(
            trayInit < hiddenBranch,
            "the tray icon is created after the window is hidden, leaving no way back into the app if a later step fails");
    }

    /// <summary>
    /// Matches the start-hidden branch while tolerating extra reasons to take it, so that adding
    /// another way in does not read as the branch having disappeared.
    /// </summary>
    private const string HiddenBranchPattern = @"if \(vm\.AppSettingsInstance\.StartInBackground[^)]*\)";

    /// <summary>Source of the <c>StartInBackground</c> branch, up to its <c>else</c>.</summary>
    private static string BackgroundBranch()
    {
        string startup = AppStartupSource();

        var match = Regex.Match(
            startup,
            HiddenBranchPattern + @"\s*\{(?<body>.*?)\}\s*else",
            RegexOptions.Singleline);

        Assert.True(match.Success, "the start-hidden branch was not found in App.xaml.cs");

        return match.Groups["body"].Value;
    }

    private static string AppStartupSource()
        => File.ReadAllText(Path.Combine(AppDir(), "App.xaml.cs"));

    private static string AppDir() => Path.Combine(FindRepoRoot(), "vrcosc-magicchatbox");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "vrcosc-magicchatbox", "Core")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
