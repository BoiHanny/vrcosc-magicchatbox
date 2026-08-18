using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Status;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class StatusSectionViewModel : ObservableObject
{
    internal const string SampleStatus = "chilling in the plaza";

    private readonly Lazy<IModuleHost> _moduleHost;

    public AppSettings AppSettings { get; }
    public TimeSettings TimeSettings { get; }
    public EmojiService Emojis { get; }
    public IAppState AppState { get; }
    public AfkModule Afk => _moduleHost.Value.Afk;

    public AfkStyleViewModel AfkStyles { get; }

    public StatusSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        EmojiService emojis,
        IAppState appState,
        Lazy<IModuleHost> moduleHost,
        AfkStyleViewModel afkStyles)
    {
        AppSettings = appSettingsProvider.Value;
        TimeSettings = timeSettingsProvider.Value;
        Emojis = emojis;
        AppState = appState;
        _moduleHost = moduleHost;
        AfkStyles = afkStyles;

        AppSettings.PropertyChanged += OnAppSettingChanged;

        AppSettings.EmojiCollection.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StatusPreview));
    }

    public string StatusPreview => StatusLine.Compose(
        SampleStatus,
        ChatLinePreview.ResolveIcon(AppSettings.EnableEmojiShuffle, shuffleInChats: true, AppSettings.EmojiCollection),
        AppSettings.PrefixIconStatus,
        OscBuildContext.MaxOscLength);

    [RelayCommand]
    private void AddEmoji(string text)
    {
        Emojis.AddEmoji(text);
    }

    private void OnAppSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.PrefixIconStatus) or nameof(AppSettings.EnableEmojiShuffle))
            OnPropertyChanged(nameof(StatusPreview));
    }
}
