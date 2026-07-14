using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for chatting options.
/// </summary>
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
    }
}
