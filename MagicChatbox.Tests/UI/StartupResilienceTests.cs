using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MagicChatbox.Tests.UI;

/// <summary>
/// Keeps startup able to finish when something it talks to does not answer.
/// </summary>
/// <remarks>
/// Every optional step runs through a helper that gives up after a while and carries on. A step
/// added outside that helper is the failure worth guarding: the splash sits on whatever phase it
/// had just announced, with no error and nothing to click, and the app never reaches its window.
/// </remarks>
public class StartupResilienceTests
{
    [Fact]
    public void Attaching_to_the_windows_media_session_is_not_done_inline()
    {
        // Reaching the media session can stop answering rather than fail, and the try/catch around
        // it only helps when something is thrown. Constructed with the listener off, it cannot
        // block the thread that builds the window.
        string startup = AppStartupSource();

        var construction = Regex.Match(
            startup,
            @"ApplicationMediaController = new MediaLinkModule\(\s*(?<first>[^,]+),",
            RegexOptions.Singleline);

        Assert.True(construction.Success, "MediaLinkModule is no longer constructed during startup");
        Assert.Equal("shouldStart: false", construction.Groups["first"].Value.Trim());
    }

    [Fact]
    public void The_media_session_listener_starts_under_a_time_limit()
    {
        // Without this the whole app waits on a Windows service that may never come back.
        string startup = AppStartupSource();

        Assert.Matches(
            @"RunOptionalStartupTaskAsync\(\s*""MediaLink session listener"",\s*\(\)\s*=>\s*ApplicationMediaController\.StartIfEnabled\(\)",
            startup);
    }

    [Fact]
    public void A_listener_that_never_attached_is_written_to_the_log()
    {
        // The chatbox simply shows no music, which looks identical to nothing playing. The log is
        // what tells the difference, so a support ticket does not start from scratch.
        string startup = AppStartupSource();

        Assert.Contains("did not attach", startup, StringComparison.Ordinal);
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
