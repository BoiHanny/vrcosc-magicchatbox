using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for Window Activity options.
/// Acts as the complete binding surface for WindowActivitySection.xaml.
/// Exposes settings, display state, and cleanup commands.
/// </summary>
public partial class WindowActivitySectionViewModel : ObservableObject
{
    private readonly IWindowActivityService _windowActivitySvc;

    public AppSettings AppSettings { get; }
    public WindowActivityDisplayState WindowActivity { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public WindowActivitySettings WindowActivitySettings { get; }

    /// <summary>
    /// Initializes the window-activity section ViewModel with the window module, app-state,
    /// settings, navigation, and display-state dependencies.
    /// </summary>
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
