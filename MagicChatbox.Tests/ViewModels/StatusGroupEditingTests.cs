using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MagicChatbox.Tests.UI;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.ViewModels;

public sealed class StatusGroupEditingTests
{
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
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public PulsoidAuthState PulsoidAuthState { get; set; }
        public int MainWindowBlurEffect { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class NoOpNavigation : IMenuNavigationService
    {
        public void ActivateSetting(string settingName) { }
        public void NavigateToPage(int pageIndex) { }
        public void NavigateBack() { }
        public void NavigateForward() { }
        public void NavigateToPrivacy() { }
    }

    private sealed class RecordingStatusListService : IStatusListService
    {
        public List<string> AddedGroups { get; } = new();
        public List<(string GroupId, string Name)> RenamedGroups { get; } = new();

        public void LoadStatusList() { }
        public void SaveStatusList() { }
        public void RequestSave() { }
        public void AddGroup(string name) => AddedGroups.Add(name);
        public void RenameGroup(string groupId, string newName)
            => RenamedGroups.Add((groupId, newName));
        public void DeleteGroup(string groupId) { }
        public string ExportGroupToJson(string groupId) => string.Empty;
        public string ExportItemsToJson(IEnumerable<StatusItem> items) => string.Empty;
        public int ImportFromJson(string json) => 0;
    }

    private sealed class RecordingToast : IToastService
    {
        public List<string> Messages { get; } = new();
        public ObservableCollection<ToastItemViewModel> Toasts { get; } = new();

        public void Show(
            string title,
            string message,
            ToastType type = ToastType.Info,
            ToastAction? action = null,
            int durationMs = 5000,
            string? key = null)
            => Messages.Add(message);

        public void Dismiss(ToastItemViewModel item) { }
    }

    [Fact]
    public void Invalid_group_names_explain_why_nothing_was_saved()
    {
        Exception? failure = WpfHost.Run(() =>
        {
            var service = new RecordingStatusListService();
            var toast = new RecordingToast();
            StatusPageViewModel viewModel = CreateViewModel(service, toast);

            viewModel.NewGroupName = " ";
            viewModel.ConfirmAddGroupCommand.Execute(null);
            viewModel.NewGroupName = new string('x', 51);
            viewModel.ConfirmAddGroupCommand.Execute(null);

            var group = new StatusGroup
            {
                GroupId = "group-1",
                Name = "Friends",
                RenameBuffer = " ",
                IsRenaming = true,
            };
            viewModel.ConfirmRenameGroupCommand.Execute(group);

            Assert.Empty(service.AddedGroups);
            Assert.Empty(service.RenamedGroups);
            Assert.Contains(toast.Messages, message => message.Contains("Enter", StringComparison.Ordinal));
            Assert.Contains(toast.Messages, message => message.Contains("50", StringComparison.Ordinal));
            Assert.True(group.IsRenaming);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void Valid_group_names_are_trimmed_before_they_are_saved()
    {
        Exception? failure = WpfHost.Run(() =>
        {
            var service = new RecordingStatusListService();
            StatusPageViewModel viewModel = CreateViewModel(service, new RecordingToast());

            viewModel.NewGroupName = "  Friends  ";
            viewModel.ConfirmAddGroupCommand.Execute(null);

            var group = new StatusGroup
            {
                GroupId = "group-1",
                Name = "Friends",
                RenameBuffer = "  Close friends  ",
                IsRenaming = true,
            };
            viewModel.ConfirmRenameGroupCommand.Execute(group);

            Assert.Equal(new[] { "Friends" }, service.AddedGroups);
            Assert.Equal(("group-1", "Close friends"), Assert.Single(service.RenamedGroups));
            Assert.False(group.IsRenaming);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void Rename_mode_moves_focus_into_the_bounded_editor()
    {
        string root = FindRepoRoot();
        string codeBehind = File.ReadAllText(Path.Combine(
            root,
            "vrcosc-magicchatbox",
            "UI",
            "Pages",
            "StatusPage.xaml.cs"));
        string xaml = File.ReadAllText(Path.Combine(
            root,
            "vrcosc-magicchatbox",
            "UI",
            "Pages",
            "StatusPage.xaml"));

        Assert.Contains("FocusRenameTextBox(group)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Focus(textBox)", codeBehind, StringComparison.Ordinal);
        Assert.Contains("textBox.SelectAll()", codeBehind, StringComparison.Ordinal);
        Assert.True(Regex.Matches(xaml, "MaxLength=\"50\"").Count >= 2);
    }

    private static StatusPageViewModel CreateViewModel(
        IStatusListService statusListService,
        IToastService toast)
        => new(
            new ChatStatusDisplayState(),
            new FakeAppState(),
            statusListService,
            new NoOpNavigation(),
            new StubSettingsProvider<AppSettings>(new AppSettings()),
            new ImmediateDispatcher(),
            toast);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !Directory.Exists(Path.Combine(directory.FullName, "vrcosc-magicchatbox", "Core")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("repo root not found from " + AppContext.BaseDirectory);
    }
}
