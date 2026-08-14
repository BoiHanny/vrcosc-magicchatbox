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

        // Every setting here changes the shape of one short line, so the preview is the whole
        // explanation - re-read it whenever any of them moves.
        TimeSettings.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OutputPreview));
    }

    /// <summary>
    /// The clock exactly as the chatbox would receive it right now. No network and no scan loop is
    /// involved, so it reads correctly whether or not the integration is switched on.
    /// </summary>
    public string OutputPreview =>
        TimeSegmentFormatter.Compose(_timeFormatting.GetFormattedCurrentTime(), TimeSettings.PrefixTime);
}
