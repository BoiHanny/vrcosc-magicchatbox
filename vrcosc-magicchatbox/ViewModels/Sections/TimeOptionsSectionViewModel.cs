using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class TimeOptionsSectionViewModel : ObservableObject
{
    private readonly ITimeFormattingService _timeFormatting;

    public AppSettings AppSettings { get; }
    public TimeSettings TimeSettings { get; }

    public TimeOptionsSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<TimeSettings> timeSettingsProvider,
        ITimeFormattingService timeFormatting)
    {
        AppSettings = appSettingsProvider.Value;
        TimeSettings = timeSettingsProvider.Value;
        _timeFormatting = timeFormatting;

        TimeSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OutputPreview));
    }

    public string OutputPreview =>
        TimeSegmentFormatter.Compose(_timeFormatting.GetFormattedCurrentTime(), TimeSettings.PrefixTime);
}
