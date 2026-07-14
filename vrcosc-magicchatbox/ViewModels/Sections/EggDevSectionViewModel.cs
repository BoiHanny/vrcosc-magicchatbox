using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for the easter egg / dev options.
/// </summary>
public partial class EggDevSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public IAppState AppState { get; }

    public EggDevSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        IAppState appState)
    {
        AppSettings = appSettingsProvider.Value;
        AppState = appState;
    }
}
