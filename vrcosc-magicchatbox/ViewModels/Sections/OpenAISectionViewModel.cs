using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for OpenAI integration options.
/// Complete binding surface for OpenAISection.xaml.
/// </summary>
public partial class OpenAISectionViewModel : ObservableObject
{
    private readonly ISettingsProvider<OpenAISettings> _settingsProvider;
    private readonly Lazy<OpenAIModule> _openAIModule;
    private readonly INavigationService _nav;

    public ISettingsProvider<OpenAISettings> OpenAISettingsProvider => _settingsProvider;
    public OpenAIModule OpenAIModuleInstance => _openAIModule.Value;

    public AppSettings AppSettings { get; }
    public OpenAIDisplayState OpenAI { get; }
    public ChatSettings ChatSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public INavigationService Navigation => _nav;

    private readonly Lazy<IModuleHost> _moduleHost;

    /// <summary>
    /// Initializes the OpenAI section ViewModel with the IntelliChat module, settings,
    /// app-state, and supporting services.
    /// </summary>
    public OpenAISectionViewModel(
        ISettingsProvider<OpenAISettings> settingsProvider,
        OpenAIDisplayState displayState,
        Lazy<OpenAIModule> openAIModule,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider,
        Lazy<IModuleHost> moduleHost,
        INavigationService nav)
    {
        _settingsProvider = settingsProvider;
        OpenAI = displayState;
        _openAIModule = openAIModule;
        AppSettings = appSettingsProvider.Value;
        ChatSettings = chatSettingsProvider.Value;
        _moduleHost = moduleHost;
        _nav = nav;
    }

    [RelayCommand]
    private void DisconnectOpenAI()
    {
        var settings = _settingsProvider.Value;
        settings.AccessToken = string.Empty;
        settings.OrganizationID = string.Empty;
        settings.OrganizationIDEncrypted = string.Empty;
        settings.AccessTokenEncrypted = string.Empty;
        _settingsProvider.Save();
        OpenAI.Connected = false;
        _openAIModule.Value.OpenAIClient = null;
    }

    [RelayCommand]
    private void LearnMoreOpenAI()
        => _nav.OpenUrl(Core.Constants.OpenAiTermsUrl);

    [RelayCommand]
    private void OpenAIUsage()
        => _nav.OpenUrl(Core.Constants.OpenAiUsageUrl);
}
