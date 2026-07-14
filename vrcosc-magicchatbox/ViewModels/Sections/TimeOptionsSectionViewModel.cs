using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for time display options.
/// </summary>
public partial class TimeOptionsSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public TimeSettings TimeSettings { get; }

    public TimeOptionsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider)
    {
        AppSettings = appSettingsProvider.Value;
        TimeSettings = timeSettingsProvider.Value;
    }
}
