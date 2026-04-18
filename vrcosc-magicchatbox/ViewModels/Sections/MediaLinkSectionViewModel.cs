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
/// Section ViewModel for MediaLink options.
/// Complete binding surface for MediaLinkSection.xaml.
/// </summary>
public partial class MediaLinkSectionViewModel : ObservableObject
{
    private readonly Lazy<IMediaLinkPersistenceService> _mediaLinkSvc;
    private readonly INavigationService _nav;

    public AppSettings AppSettings { get; }
    public MediaLinkSettings MediaLinkSettings { get; }
    public MediaLinkDisplayState MediaLink { get; }

    /// <summary>
    /// Initializes the media-link section ViewModel with media, OSC, settings, module,
    /// and app-state dependencies.
    /// </summary>
    public MediaLinkSectionViewModel(
        Lazy<IMediaLinkPersistenceService> mediaLinkSvc,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        MediaLinkDisplayState mediaLinkDisplay,
        INavigationService nav)
    {
        _mediaLinkSvc = mediaLinkSvc;
        AppSettings = appSettingsProvider.Value;
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        MediaLink = mediaLinkDisplay;
        _nav = nav;
    }

    [RelayCommand]
    private void AddSeekbarStyle() => _mediaLinkSvc.Value.AddNewSeekbarStyle();

    [RelayCommand]
    private void DeleteSeekbarStyle() => _mediaLinkSvc.Value.DeleteSelectedSeekbarStyleAndSelectDefault();

    [RelayCommand]
    private void LearnMoreMediaLink()
        => _nav.OpenUrl(Core.Constants.WikiMusicDisplayUrl);
}
