using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Toast;
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
    private readonly IToastService _toast;

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
        INavigationService nav,
        IToastService toast)
    {
        _mediaLinkSvc = mediaLinkSvc;
        AppSettings = appSettingsProvider.Value;
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        MediaLink = mediaLinkDisplay;
        _nav = nav;
        _toast = toast;
    }

    [RelayCommand]
    private void AddSeekbarStyle() => _mediaLinkSvc.Value.AddNewSeekbarStyle();

    [RelayCommand]
    private void DeleteSeekbarStyle() => _mediaLinkSvc.Value.DeleteSelectedSeekbarStyleAndSelectDefault();

    [RelayCommand]
    private void CopySeekbarPreview()
    {
        string? preview = MediaLink.SelectedMediaLinkSeekbarStyle?.StyleName;
        if (string.IsNullOrWhiteSpace(preview))
            return;

        try
        {
            Clipboard.SetText(preview);
            _toast.Show("MediaLink", "Seekbar style name copied to clipboard.", ToastType.Success, key: "medialink-preview-copied");
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
            _toast.Show("MediaLink", "Could not copy the seekbar style name.", ToastType.Warning, key: "medialink-preview-copy-failed");
        }
    }

    [RelayCommand]
    private void ExportSeekbarStyles()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export MediaLink progress bar styles",
            FileName = "MagicChatbox-MediaLink-ProgressBars.json",
            DefaultExt = ".json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _mediaLinkSvc.Value.ExportSeekbarStyles(dialog.FileName);
            _toast.Show("MediaLink", "Progress bar styles exported.", ToastType.Success, key: "medialink-styles-exported");
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
            _toast.Show("MediaLink", "Could not export progress bar styles.", ToastType.Error, key: "medialink-styles-export-failed");
        }
    }

    [RelayCommand]
    private void ImportSeekbarStyles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import MediaLink progress bar styles",
            DefaultExt = ".json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            int importedCount = _mediaLinkSvc.Value.ImportSeekbarStyles(dialog.FileName);
            string message = importedCount == 1
                ? "1 progress bar style imported."
                : $"{importedCount} progress bar styles imported.";

            _toast.Show("MediaLink", message, ToastType.Success, key: "medialink-styles-imported");
        }
        catch (Exception ex)
        {
            Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
            _toast.Show("MediaLink", "Could not import progress bar styles. Check that the file is valid JSON.", ToastType.Error, key: "medialink-styles-import-failed");
        }
    }

    [RelayCommand]
    private void LearnMoreMediaLink()
        => _nav.OpenUrl(Core.Constants.WikiMusicDisplayUrl);
}
