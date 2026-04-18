using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for status options (status cycling, AFK, emojis, BussyBoys).
/// </summary>
public partial class StatusSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public TimeSettings TimeSettings { get; }
    public EmojiService Emojis { get; }
    public IAppState AppState { get; }
    public AfkModule Afk => _moduleHost.Value.Afk;

    /// <summary>
    /// Initializes the status-section ViewModel with its module, navigation, chat state,
    /// app-state, and settings.
    /// </summary>
    public StatusSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        EmojiService emojis,
        IAppState appState,
        Lazy<IModuleHost> moduleHost)
    {
        AppSettings = appSettingsProvider.Value;
        TimeSettings = timeSettingsProvider.Value;
        Emojis = emojis;
        AppState = appState;
        _moduleHost = moduleHost;
    }

    [RelayCommand]
    private void AddEmoji(string text)
    {
        Emojis.AddEmoji(text);
    }
}
