using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public static class ChatLinePreview
{
    public const string DefaultIcon = "💬";

    public const string SampleMessage = "anyone up for a world hop?";

    public static string Build(bool prefixIcon, bool shuffleEnabled, bool shuffleInChats, IEnumerable<string>? icons, string message)
        => prefixIcon
            ? ResolveIcon(shuffleEnabled, shuffleInChats, icons) + " " + message
            : message;

    public static string ResolveIcon(bool shuffleEnabled, bool shuffleInChats, IEnumerable<string>? icons)
    {
        if (!shuffleEnabled || !shuffleInChats)
            return DefaultIcon;

        string? first = icons?.FirstOrDefault(icon => !string.IsNullOrWhiteSpace(icon));
        return string.IsNullOrWhiteSpace(first) ? DefaultIcon : first.Trim();
    }
}

public partial class ChattingOptionsSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _modules;

    public AppSettings AppSettings { get; }
    public ChatSettings ChatSettings { get; }
    public IntelliChatModuleSettings IntelliChatSettings => _modules.Value.IntelliChat.Settings;
    public IEnumerable<IntelliGPTModel> AvailableChatModels => _modules.Value.IntelliChat.AvailableChatModels;
    public IEnumerable<ChatAutocompleteMode> AvailableAutocompleteModes => vrcosc_magicchatbox.Classes.Modules.ChatSettings.AvailableChatAutocompleteModes;

    public ChattingOptionsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider,
        Lazy<IModuleHost> modules)
    {
        _modules = modules;
        AppSettings = appSettingsProvider.Value;
        ChatSettings = chatSettingsProvider.Value;

        ChatSettings.PropertyChanged += OnChatSettingChanged;
        AppSettings.PropertyChanged += OnAppSettingChanged;

        AppSettings.EmojiCollection.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ChatPreview));
    }

    public string ChatPreview => ChatLinePreview.Build(
        ChatSettings.PrefixChat,
        AppSettings.EnableEmojiShuffle,
        AppSettings.EnableEmojiShuffleInChats,
        AppSettings.EmojiCollection,
        ChatLinePreview.SampleMessage);

    private void OnChatSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatSettings.PrefixChat))
            OnPropertyChanged(nameof(ChatPreview));
    }

    private void OnAppSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.EnableEmojiShuffle) or nameof(AppSettings.EnableEmojiShuffleInChats))
            OnPropertyChanged(nameof(ChatPreview));
    }
}
