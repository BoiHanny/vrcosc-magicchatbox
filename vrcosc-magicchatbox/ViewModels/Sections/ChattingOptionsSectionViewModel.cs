using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for chatting options.
/// </summary>
public partial class ChattingOptionsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public ChatSettings ChatSettings { get; }

    public ChattingOptionsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<ChatSettings> chatSettingsProvider)
    {
        AppSettings = appSettingsProvider.Value;
        ChatSettings = chatSettingsProvider.Value;
    }
}
