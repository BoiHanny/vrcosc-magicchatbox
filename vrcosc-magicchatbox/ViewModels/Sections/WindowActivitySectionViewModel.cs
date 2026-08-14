using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// The focused-app line written from a stand-in app, so the wording boxes can be judged while the
/// app being looked at is the settings window itself.
/// </summary>
public static class WindowActivityPreview
{
    /// <summary>An app and a title long enough to show what shortening does, and no longer.</summary>
    public const string SampleApp = "Firefox";
    public const string SampleTitle = "Weather forecast for the weekend";

    /// <summary>
    /// The same three parts the builder assembles, in the same order and with the same spacing: the
    /// heading, the word joining it to the app, and the app itself. The heading and the joining word
    /// are the user's own text, already styled the way they typed it, so neither is raised again.
    /// </summary>
    public static string Render(string? heading, string? focusWord, string? app, bool nameTheApp)
        => new SegmentWriter()
            .Field(
                OscText.Raw(heading),
                OscText.Raw(nameTheApp ? focusWord : null),
                OscText.Value(nameTheApp ? app : null))
            .Text;

    /// <summary>What a title looks like once the per-app length limit has had it.</summary>
    public static string Title(bool limitOn, int configured)
        => SegmentWriter.Truncate(SampleTitle, WindowActivityText.TitleCap(limitOn, configured));
}

public partial class WindowActivitySectionViewModel : ObservableObject
{
    private readonly IWindowActivityService _windowActivitySvc;

    public AppSettings AppSettings { get; }
    public WindowActivityDisplayState WindowActivity { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public WindowActivitySettings WindowActivitySettings { get; }

    [ObservableProperty] private string _desktopPreviewLine = string.Empty;
    [ObservableProperty] private string _vrPreviewLine = string.Empty;

    public WindowActivitySectionViewModel(
        IWindowActivityService windowActivitySvc,
        WindowActivityDisplayState windowActivity,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<WindowActivitySettings> windowActivitySettingsProvider)
    {
        _windowActivitySvc = windowActivitySvc;
        WindowActivity = windowActivity;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        WindowActivitySettings = windowActivitySettingsProvider.Value;

        WindowActivitySettings.PropertyChanged += OnAnythingChanged;
        IntegrationSettings.PropertyChanged += OnAnythingChanged;
        RefreshPreviews();
    }

    private void OnAnythingChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreviews();

    private void RefreshPreviews()
    {
        // The app name is what the module puts in the slot; the window title rides along behind it
        // when the user asked for it, and is cut to the length they set.
        string title = WindowActivitySettings.TitleScan
            ? WindowActivityPreview.Title(WindowActivitySettings.LimitTitleOnApp, WindowActivitySettings.MaxShowTitleCount)
            : string.Empty;

        string app = WindowActivityText.Compose(WindowActivityPreview.SampleApp, title);

        DesktopPreviewLine = WindowActivityPreview.Render(
            WindowActivitySettings.DesktopTitle,
            WindowActivitySettings.DesktopFocusTitle,
            app,
            WindowActivitySettings.ShowFocusedApp);

        VrPreviewLine = WindowActivityPreview.Render(
            WindowActivitySettings.VrTitle,
            WindowActivitySettings.VrFocusTitle,
            WindowActivitySettings.TitleOnAppVR ? app : WindowActivityText.Compose(WindowActivityPreview.SampleApp, null),
            IntegrationSettings.IntgrScanForce);
    }

    [RelayCommand]
    private void ResetWindowActivity()
        => ExecuteCleanup(_windowActivitySvc.ResetWindowActivity, "All apps from history");

    [RelayCommand]
    private void SmartCleanup()
        => ExecuteCleanup(_windowActivitySvc.SmartCleanup);

    [RelayCommand]
    private void CleanupKeepSettings()
        => ExecuteCleanup(_windowActivitySvc.CleanAndKeepAppsWithSettings);

    [RelayCommand]
    private void AddTitleFilter()
    {
        WindowActivitySettings.TitleFilters.Add(new TitleFilterRule());
        _windowActivitySvc.SaveSettings();
    }

    [RelayCommand]
    private void RemoveTitleFilter(TitleFilterRule? rule)
    {
        if (rule != null && WindowActivitySettings.TitleFilters.Remove(rule))
            _windowActivitySvc.SaveSettings();
    }

    private void ExecuteCleanup(Func<int> cleanupAction, string? allRemovedLabel = null)
    {
        int removed = cleanupAction();
        if (removed > 0)
            WindowActivity.DeletedAppslabel = allRemovedLabel ?? $"Removed {removed} apps from history";
        else if (allRemovedLabel == null)
            WindowActivity.DeletedAppslabel = "No apps removed from history";
    }
}
