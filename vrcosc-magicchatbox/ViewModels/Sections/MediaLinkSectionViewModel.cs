using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public static class SeekbarPreviewBuilder
{
    public static readonly TimeSpan SampleElapsed = TimeSpan.FromSeconds(83);

    public static readonly TimeSpan SampleLength = TimeSpan.FromSeconds(225);

    public static string Build(MediaLinkStyle? style)
    {
        if (style == null)
            return string.Empty;

        double percent = SampleElapsed.TotalSeconds / SampleLength.TotalSeconds * 100;

        return SeekbarUtilities.CreateProgressBar(percent, SampleElapsed, SampleLength, new SeekbarStyleOptions
        {
            DisplayTime = style.DisplayTime,
            FilledCharacter = style.FilledCharacter,
            MiddleCharacter = style.MiddleCharacter,
            NonFilledCharacter = style.NonFilledCharacter,
            ProgressBarLength = style.ProgressBarLength,
            ShowTimeInSuperscript = style.ShowTimeInSuperscript,
            SpaceAgainObjects = style.SpaceAgainObjects,
            SpaceBetweenPreSuffixAndTime = style.SpaceBetweenPreSuffixAndTime,
            TimePrefix = style.TimePrefix,
            TimePreSuffixOnTheInside = style.TimePreSuffixOnTheInside,
            TimeSuffix = style.TimeSuffix,
        });
    }

    public static string Caption(string bar)
        => string.IsNullOrEmpty(bar)
            ? "Fill in all three characters below to see the bar"
            : $"At {SeekbarUtilities.FormatTimeSpan(SampleElapsed)} of a {SeekbarUtilities.FormatTimeSpan(SampleLength)} song";
}

public partial class MediaLinkSectionViewModel : ObservableObject
{
    private readonly Lazy<IMediaLinkPersistenceService> _mediaLinkSvc;
    private readonly INavigationService _nav;
    private readonly IToastService _toast;

    private MediaLinkStyle? _watchedStyle;

    public AppSettings AppSettings { get; }
    public MediaLinkSettings MediaLinkSettings { get; }
    public MediaLinkDisplayState MediaLink { get; }

    public MediaLinkSectionViewModel(
        Lazy<IMediaLinkPersistenceService> mediaLinkSvc,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        MediaLinkDisplayState mediaLinkDisplay,
        IMenuNavigationService menuNav,
        INavigationService nav,
        IToastService toast)
    {
        _mediaLinkSvc = mediaLinkSvc;
        AppSettings = appSettingsProvider.Value;
        MediaLinkSettings = mediaLinkSettingsProvider.Value;
        MediaLink = mediaLinkDisplay;
        _nav = nav;
        _toast = toast;

        MediaLink.PropertyChanged += OnDisplayChanged;
        WatchSelectedStyle();
    }

    public string SeekbarPreview => SeekbarPreviewBuilder.Build(MediaLink.SelectedMediaLinkSeekbarStyle);

    public string SeekbarPreviewCaption => SeekbarPreviewBuilder.Caption(SeekbarPreview);

    private void OnDisplayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MediaLinkDisplayState.SelectedMediaLinkSeekbarStyle))
            return;

        WatchSelectedStyle();
        RefreshSeekbarPreview();
    }

    private void WatchSelectedStyle()
    {
        if (_watchedStyle != null)
            _watchedStyle.PropertyChanged -= OnSelectedStyleChanged;

        _watchedStyle = MediaLink.SelectedMediaLinkSeekbarStyle;

        if (_watchedStyle != null)
            _watchedStyle.PropertyChanged += OnSelectedStyleChanged;
    }

    private void OnSelectedStyleChanged(object? sender, PropertyChangedEventArgs e) => RefreshSeekbarPreview();

    private void RefreshSeekbarPreview()
    {
        OnPropertyChanged(nameof(SeekbarPreview));
        OnPropertyChanged(nameof(SeekbarPreviewCaption));
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
