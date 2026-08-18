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

public static class WindowActivityPreview
{
    public const string SampleApp = "Firefox";
    public const string SampleTitle = "Weather forecast for the weekend";

    public static string Render(string? heading, string? focusWord, string? app, bool nameTheApp)
        => new SegmentWriter()
            .Field(
                OscText.Raw(heading),
                OscText.Raw(nameTheApp ? focusWord : null),
                OscText.Value(nameTheApp ? app : null))
            .Text;

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
