using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>Persisted settings for the IntelliChat AI text-enhancement module.</summary>
public partial class IntelliChatModuleSettings : VersionedSettings
{
    [ObservableProperty]
    private bool autolanguageSelection = true;

    [ObservableProperty]
    private IntelliGPTModel generateConversationStarterModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private bool intelliChatError = false;

    [ObservableProperty]
    private string intelliChatErrorTxt = string.Empty;

    [ObservableProperty]
    private bool intelliChatPerformModeration = true;

    [ObservableProperty]
    private int intelliChatPerformModerationTimeout = 7;

    [ObservableProperty]
    private int intelliChatTimeout = 10;

    [ObservableProperty]
    private string intelliChatTxt = string.Empty;

    [ObservableProperty]
    private bool intelliChatUILabel = false;

    [ObservableProperty]
    private string intelliChatUILabelTxt = string.Empty;

    [ObservableProperty]
    private bool intelliChatWaitingToAccept = false;

    [ObservableProperty]
    private IntelliGPTModel performBeautifySentenceModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private IntelliGPTModel performLanguageTranslationModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private IntelliGPTModel performModerationCheckModel = IntelliGPTModel.Moderation_Latest;

    [ObservableProperty]
    private IntelliGPTModel performShortenTextModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private IntelliGPTModel performSpellingCheckModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private IntelliGPTModel performTextCompletionModel = IntelliGPTModel.gpt5_nano;

    [ObservableProperty]
    private List<SupportedIntelliChatLanguage> selectedSupportedLanguages = new List<SupportedIntelliChatLanguage>();

    [ObservableProperty]
    private SupportedIntelliChatLanguage selectedTranslateLanguage;

    [ObservableProperty]
    private IntelliChatWritingStyle selectedWritingStyle;

    [ObservableProperty]
    private List<SupportedIntelliChatLanguage> supportedLanguages = new List<SupportedIntelliChatLanguage>();

    [ObservableProperty]
    private List<IntelliChatWritingStyle> supportedWritingStyles = new List<IntelliChatWritingStyle>();

    [ObservableProperty]
    private TokenUsageData tokenUsageData = new TokenUsageData();
}
