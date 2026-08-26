using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace MagicChatbox.Tests.Services;

/// <summary>
/// A custom seekbar, from the moment it is made to the moment it is read back.
/// </summary>
/// <remarks>
/// Users reported that a seekbar they built was blank or gone after a restart, and both halves of
/// that were real. A new style was created with none of its three bar characters set, so it rendered
/// as nothing the instant it was selected; and editing a style raised change notifications that
/// nobody was listening to, so the only writers were add, delete, import and a clean shutdown -
/// anything typed was in memory only until the app was closed properly.
/// </remarks>
public class SeekbarStylePersistenceTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(
        Path.GetTempPath(), "mcb-seekbar-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_new_style_is_usable_rather_than_blank()
    {
        var state = StateWithDefaults();
        using var service = Service(state);

        service.AddNewSeekbarStyle();

        MediaLinkStyle? added = state.SelectedMediaLinkSeekbarStyle;
        Assert.NotNull(added);
        Assert.False(added.SystemDefault);
        Assert.True(added.ID >= 100);

        Assert.False(string.IsNullOrEmpty(added.FilledCharacter), "a new seekbar has no filled character, so it renders as nothing");
        Assert.False(string.IsNullOrEmpty(added.NonFilledCharacter), "a new seekbar has no unfilled character, so it renders as nothing");
    }

    [Fact]
    public async Task Editing_a_style_reaches_the_file_without_being_asked()
    {
        var state = StateWithDefaults();
        using var service = Service(state);

        service.AddNewSeekbarStyle();
        MediaLinkStyle? mine = state.SelectedMediaLinkSeekbarStyle;
        Assert.NotNull(mine);

        mine.FilledCharacter = "#";
        mine.NonFilledCharacter = "-";

        await WaitForStyleOnDisk(mine.ID, "#");

        JToken? saved = ReadSavedStyle(mine.ID);
        Assert.NotNull(saved);
        Assert.Equal("#", saved!["FilledCharacter"]?.Value<string>());
        Assert.Equal("-", saved["NonFilledCharacter"]?.Value<string>());
    }

    [Fact]
    public void A_null_selection_is_ignored_rather_than_stored()
    {
        var state = StateWithDefaults();
        MediaLinkStyle chosen = state.MediaLinkSeekbarStyles.First();
        state.SelectedMediaLinkSeekbarStyle = chosen;

        // What a torn-down combo box pushes back through its TwoWay binding.
        state.SelectedMediaLinkSeekbarStyle = null!;

        Assert.Same(chosen, state.SelectedMediaLinkSeekbarStyle);
    }

    private async Task WaitForStyleOnDisk(int id, string expectedFilled)
    {
        for (int i = 0; i < 60; i++)
        {
            JToken? saved = ReadSavedStyle(id);
            if (saved?["FilledCharacter"]?.Value<string>() == expectedFilled)
                return;

            await Task.Delay(100);
        }

        Assert.Fail("the edit never reached MediaLinkStyles.json, so it would be lost on anything but a clean exit");
    }

    private JToken? ReadSavedStyle(int id)
    {
        string path = Path.Combine(_dataPath, "MediaLinkStyles.json");
        if (!File.Exists(path))
            return null;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JObject.Parse(json)["CustomStyles"]?
            .FirstOrDefault(s => s["ID"]?.Value<int>() == id);
    }

    private static MediaLinkDisplayState StateWithDefaults()
    {
        var state = new MediaLinkDisplayState();
        state.MediaLinkSeekbarStyles.Add(new MediaLinkStyle
        {
            ID = 1,
            SystemDefault = true,
            ProgressBarLength = 8,
            FilledCharacter = "=",
            MiddleCharacter = "O",
            NonFilledCharacter = ".",
        });
        return state;
    }

    private MediaLinkPersistenceService Service(MediaLinkDisplayState state)
    {
        var dispatcher = new InlineDispatcher();
        return new MediaLinkPersistenceService(
            new FixedEnvironment(_dataPath), state, new WindowActivityDisplayState(dispatcher),
            new AlwaysCreates(), dispatcher);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataPath))
                Directory.Delete(_dataPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FixedEnvironment(string dataPath) : IEnvironmentService
    {
        public string DataPath { get; } = dataPath;
        public string LogPath => DataPath;
        public string VrcPath => DataPath;
        public void SetCustomProfile(int profileNumber) { }
    }

    private sealed class AlwaysCreates : IAppHistoryService
    {
        public void LoadAppHistory() { }
        public void SaveAppHistory() { }

        public bool CreateIfMissing(string path)
        {
            Directory.CreateDirectory(path);
            return true;
        }
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
