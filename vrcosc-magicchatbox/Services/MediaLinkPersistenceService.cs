using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Loads and saves media link session data and seekbar styles to disk.
/// </summary>
public sealed class MediaLinkPersistenceService : IMediaLinkPersistenceService
{
    private readonly IEnvironmentService _env;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly WindowActivityDisplayState _windowActivity;
    private readonly IAppHistoryService _appHistory;

    private const string MediaLinkStylesFileName = "MediaLinkStyles.json";

    public MediaLinkPersistenceService(
        IEnvironmentService env,
        MediaLinkDisplayState mediaLink,
        WindowActivityDisplayState windowActivity,
        IAppHistoryService appHistory)
    {
        _env = env;
        _mediaLink = mediaLink;
        _windowActivity = windowActivity;
        _appHistory = appHistory;
    }

    public void LoadMediaSessions()
    {
        try
        {
            if (File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"))
                || File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.xml")))
            {
                string json = File
                    .ReadAllText(File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"))
                        ? Path.Combine(_env.DataPath, "LastMediaLinkSessions.json")
                        : Path.Combine(_env.DataPath, "LastMediaLinkSessions.xml"));
                if (json.ToLower().Equals("null"))
                {
                    Logging.WriteInfo("LastMediaLinkSessions history is null, not problem :P");
                    _mediaLink.SavedSessionSettings = new List<MediaSessionSettings>();
                    return;
                }
                _mediaLink.SavedSessionSettings = JsonConvert.DeserializeObject<List<MediaSessionSettings>>(json);
            }
            else
            {
                Logging.WriteInfo("LastMediaSessions history has never been created, not problem :P");
                if (_mediaLink.SavedSessionSettings == null)
                {
                    _mediaLink.SavedSessionSettings = new List<MediaSessionSettings>();
                }
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            if (_windowActivity.ScannedApps == null)
            {
                _windowActivity.ScannedApps = new ObservableCollection<ViewModels.ProcessInfo>();
            }
        }
    }

    public void SaveMediaSessions()
    {
        try
        {
            if (_appHistory.CreateIfMissing(_env.DataPath) == true)
            {
                string json = JsonConvert.SerializeObject(_mediaLink.SavedSessionSettings);

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                File.WriteAllText(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"), json);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void LoadSeekbarStyles()
    {
        try
        {
            LoadMediaLinkStyles();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void SaveSeekbarStyles()
    {
        try
        {
            SaveMediaLinkStyles();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void AddNewSeekbarStyle()
    {
        ObservableCollection<MediaLinkStyle> customStyles = new ObservableCollection<MediaLinkStyle>(
            _mediaLink.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault));

        int highestID = customStyles.Any() ? customStyles.Max(s => s.ID) : 99;
        int nextAvailableID = highestID + 1;

        if (nextAvailableID < 100)
        {
            nextAvailableID = 100;
        }

        MediaLinkStyle newStyle = new MediaLinkStyle
        {
            ID = nextAvailableID,
            ProgressBarLength = 8,
            SystemDefault = false
        };

        _mediaLink.MediaLinkSeekbarStyles.Add(newStyle);
        _mediaLink.SelectedMediaLinkSeekbarStyle = newStyle;

        SaveMediaLinkStyles();

        Logging.WriteInfo($"New media link style with ID {nextAvailableID} added.");
    }

    public void DeleteSelectedSeekbarStyleAndSelectDefault()
    {
        if (_mediaLink.SelectedMediaLinkSeekbarStyle == null)
        {
            return;
        }

        if (_mediaLink.SelectedMediaLinkSeekbarStyle.SystemDefault)
        {
            Logging.WriteInfo("Cannot delete system default media link style.");
            return;
        }

        _mediaLink.MediaLinkSeekbarStyles.Remove(_mediaLink.SelectedMediaLinkSeekbarStyle);
        _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();

        SaveMediaLinkStyles();

        Logging.WriteInfo($"Media link style with ID {_mediaLink.SelectedMediaLinkSeekbarStyle.ID} deleted.");
    }

    private string GetMediaLinkStylesFilePath()
    {
        return Path.Combine(_env.DataPath, MediaLinkStylesFileName);
    }

    private void LoadMediaLinkStyles()
    {
        _mediaLink.MediaLinkSeekbarStyles = DefaultMediaLinkStyles();
        Logging.WriteInfo("Default media link styles loaded.");

        string filePath = GetMediaLinkStylesFilePath();

        if (File.Exists(filePath))
        {
            try
            {
                string jsonData = File.ReadAllText(filePath);
                var data = JsonConvert.DeserializeObject<MediaLinkStylesData>(jsonData);

                if (data?.CustomStyles != null)
                {
                    foreach (var style in data.CustomStyles)
                    {
                        if (!_mediaLink.MediaLinkSeekbarStyles.Any(s => s.ID == style.ID))
                        {
                            _mediaLink.MediaLinkSeekbarStyles.Add(style);
                        }
                    }
                    Logging.WriteInfo("Custom media link styles loaded.");
                }

                if (data?.SelectedStyleId != null)
                {
                    var selectedStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault(s => s.ID == data.SelectedStyleId);
                    if (selectedStyle != null)
                    {
                        _mediaLink.SelectedMediaLinkSeekbarStyle = selectedStyle;
                        Logging.WriteInfo("Selected media link style loaded.");
                    }
                    else
                    {
                        _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();
                        Logging.WriteInfo("Selected media link style not found in the loaded styles.");
                    }
                }
            }
            catch (Exception ex)
            {
                _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();
                Logging.WriteException(ex, MSGBox: false);
            }
        }
        else
        {
            Logging.WriteInfo($"Custom media link styles file '{filePath}' not found, no problem!");
            _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();
        }
    }

    private void SaveMediaLinkStyles()
    {
        try
        {
            if (_appHistory.CreateIfMissing(_env.DataPath))
            {
                string filePath = GetMediaLinkStylesFilePath();

                ObservableCollection<MediaLinkStyle> nonSystemMediaLinkStyles = new ObservableCollection<MediaLinkStyle>(
                    _mediaLink.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault));

                var data = new MediaLinkStylesData
                {
                    CustomStyles = nonSystemMediaLinkStyles,
                    SelectedStyleId = _mediaLink.SelectedMediaLinkSeekbarStyle?.ID
                };

                var jsonData = JsonConvert.SerializeObject(data);
                File.WriteAllText(filePath, jsonData);

                Logging.WriteInfo("Custom media link styles and selected style saved.");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private class MediaLinkStylesData
    {
        public ObservableCollection<MediaLinkStyle> CustomStyles { get; set; }
        public int? SelectedStyleId { get; set; }
    }

    public static ObservableCollection<MediaLinkStyle> DefaultMediaLinkStyles()
    {
        return new ObservableCollection<MediaLinkStyle>
        {
            new MediaLinkStyle
            {
                ID = 1,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "▒",
                MiddleCharacter = "▓",
                NonFilledCharacter = "░",
                TimePrefix = "",
                TimeSuffix = "",
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 2,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "▥",
                MiddleCharacter = "▥",
                NonFilledCharacter = "▢",
                TimePrefix = string.Empty,
                TimeSuffix = string.Empty,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 3,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "●",
                MiddleCharacter = "◐",
                NonFilledCharacter = "○",
                TimePrefix = "「",
                TimeSuffix = "」",
                SpaceBetweenPreSuffixAndTime = false,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 4,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "♣",
                MiddleCharacter = "♠",
                NonFilledCharacter = "○",
                TimePrefix = "【",
                TimeSuffix = "】",
                SpaceBetweenPreSuffixAndTime = false,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 5,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "★",
                MiddleCharacter = "✴",
                NonFilledCharacter = "☆",
                TimePrefix = "«",
                TimeSuffix = "»",
                SpaceBetweenPreSuffixAndTime = true,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 6,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "▞",
                MiddleCharacter = "▞",
                NonFilledCharacter = "━",
                TimePrefix = "┣",
                TimeSuffix = "┫",
                SpaceBetweenPreSuffixAndTime = false,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 7,
                ProgressBarLength = 8,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "◉",
                MiddleCharacter = "◉",
                NonFilledCharacter = "◎",
                TimePrefix = "",
                TimeSuffix = "",
                SpaceBetweenPreSuffixAndTime = false,
                SystemDefault = true
            },
            new MediaLinkStyle
            {
                ID = 8,
                ProgressBarLength = 7,
                DisplayTime = true,
                ShowTimeInSuperscript = true,
                FilledCharacter = "┅",
                MiddleCharacter = "🕷️",
                NonFilledCharacter = "┅",
                TimePrefix = "🧙",
                TimeSuffix = "🕸️",
                SpaceBetweenPreSuffixAndTime = false,
                SystemDefault = true
            },
        };
    }
}
