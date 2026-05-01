using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
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
    private readonly IUiDispatcher _dispatcher;

    private const string MediaLinkStylesFileName = "MediaLinkStyles.json";

    public MediaLinkPersistenceService(
        IEnvironmentService env,
        MediaLinkDisplayState mediaLink,
        WindowActivityDisplayState windowActivity,
        IAppHistoryService appHistory,
        IUiDispatcher dispatcher)
    {
        _env = env;
        _mediaLink = mediaLink;
        _windowActivity = windowActivity;
        _appHistory = appHistory;
        _dispatcher = dispatcher;
    }

    public async Task LoadMediaSessionsAsync()
    {
        try
        {
            List<MediaSessionSettings>? loadedSessions = null;

            if (File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"))
                || File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.xml")))
            {
                string json = File
                    .ReadAllText(File.Exists(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"))
                        ? Path.Combine(_env.DataPath, "LastMediaLinkSessions.json")
                        : Path.Combine(_env.DataPath, "LastMediaLinkSessions.xml"));
                if (json.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    Logging.WriteInfo("LastMediaLinkSessions history is null, not problem :P");
                    loadedSessions = new List<MediaSessionSettings>();
                }
                else
                {
                    loadedSessions = JsonConvert.DeserializeObject<List<MediaSessionSettings>>(json);
                }
            }
            else
            {
                Logging.WriteInfo("LastMediaSessions history has never been created, not problem :P");
                loadedSessions = _mediaLink.SavedSessionSettings ?? new List<MediaSessionSettings>();
            }

            loadedSessions ??= new List<MediaSessionSettings>();
            await _dispatcher.InvokeAsync(() => _mediaLink.SavedSessionSettings = loadedSessions);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            await _dispatcher.InvokeAsync(() =>
            {
                if (_windowActivity.ScannedApps == null)
                    _windowActivity.ScannedApps = new ObservableCollection<ViewModels.ProcessInfo>();
            });
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

    public async Task LoadSeekbarStylesAsync()
    {
        try
        {
            var snapshot = ReadMediaLinkStylesSnapshot();
            await _dispatcher.InvokeAsync(() => ApplyMediaLinkStyles(snapshot));
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

    public void ExportSeekbarStyles(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var data = new MediaLinkStylesData
        {
            CustomStyles = new ObservableCollection<MediaLinkStyle>(
                _mediaLink.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault)),
            SelectedStyleId = _mediaLink.SelectedMediaLinkSeekbarStyle?.SystemDefault == false
                ? _mediaLink.SelectedMediaLinkSeekbarStyle.ID
                : null
        };

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        Logging.WriteInfo($"Exported {data.CustomStyles.Count} custom media link seekbar styles to '{filePath}'.");
    }

    public int ImportSeekbarStyles(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("MediaLink seekbar style import file was not found.", filePath);

        var data = ReadStylesDataFromFile(filePath);
        if (data.CustomStyles.Count == 0)
            return 0;

        var existingIds = _mediaLink.MediaLinkSeekbarStyles.Select(s => s.ID).ToHashSet();
        var importedIdMap = new Dictionary<int, int>();
        var importedStyles = new List<MediaLinkStyle>();

        foreach (var style in data.CustomStyles)
        {
            var importedStyle = CloneMediaLinkStyle(style);
            importedStyle.SystemDefault = false;

            int originalId = importedStyle.ID;
            if (importedStyle.ID < 100 || existingIds.Contains(importedStyle.ID))
                importedStyle.ID = NextCustomStyleId(existingIds);

            existingIds.Add(importedStyle.ID);
            importedIdMap[originalId] = importedStyle.ID;
            _mediaLink.MediaLinkSeekbarStyles.Add(importedStyle);
            importedStyles.Add(importedStyle);
        }

        if (data.SelectedStyleId != null
            && importedIdMap.TryGetValue(data.SelectedStyleId.Value, out int selectedId))
        {
            _mediaLink.SelectedMediaLinkSeekbarStyle =
                _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault(s => s.ID == selectedId);
        }
        else
        {
            _mediaLink.SelectedMediaLinkSeekbarStyle = importedStyles.FirstOrDefault();
        }

        SaveMediaLinkStyles();
        Logging.WriteInfo($"Imported {importedStyles.Count} custom media link seekbar styles from '{filePath}'.");
        return importedStyles.Count;
    }

    private string GetMediaLinkStylesFilePath()
    {
        return Path.Combine(_env.DataPath, MediaLinkStylesFileName);
    }

    private MediaLinkStylesSnapshot ReadMediaLinkStylesSnapshot()
    {
        List<MediaLinkStyle> styles = DefaultMediaLinkStyles().ToList();
        Logging.WriteInfo("Default media link styles loaded.");

        string filePath = GetMediaLinkStylesFilePath();
        int? selectedStyleId = null;

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
                        if (!styles.Any(s => s.ID == style.ID))
                        {
                            styles.Add(style);
                        }
                    }
                    Logging.WriteInfo("Custom media link styles loaded.");
                }

                selectedStyleId = data?.SelectedStyleId;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }
        else
        {
            Logging.WriteInfo($"Custom media link styles file '{filePath}' not found, no problem!");
        }

        return new MediaLinkStylesSnapshot
        {
            Styles = styles,
            SelectedStyleId = selectedStyleId
        };
    }

    private void ApplyMediaLinkStyles(MediaLinkStylesSnapshot snapshot)
    {
        _mediaLink.MediaLinkSeekbarStyles = new ObservableCollection<MediaLinkStyle>(snapshot.Styles);

        if (snapshot.SelectedStyleId != null)
        {
            var selectedStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault(s => s.ID == snapshot.SelectedStyleId);
            if (selectedStyle != null)
            {
                _mediaLink.SelectedMediaLinkSeekbarStyle = selectedStyle;
                Logging.WriteInfo("Selected media link style loaded.");
                return;
            }

            Logging.WriteInfo("Selected media link style not found in the loaded styles.");
        }

        _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();
    }

    private sealed class MediaLinkStylesSnapshot
    {
        public required List<MediaLinkStyle> Styles { get; init; }
        public int? SelectedStyleId { get; init; }
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

    private static MediaLinkStyle CloneMediaLinkStyle(MediaLinkStyle style)
        => JsonConvert.DeserializeObject<MediaLinkStyle>(JsonConvert.SerializeObject(style)) ?? new MediaLinkStyle();

    private static int NextCustomStyleId(HashSet<int> existingIds)
    {
        int nextId = existingIds.Count == 0 ? 100 : Math.Max(100, existingIds.Max() + 1);
        while (existingIds.Contains(nextId))
            nextId++;

        return nextId;
    }

    private static MediaLinkStylesData ReadStylesDataFromFile(string filePath)
    {
        string jsonData = File.ReadAllText(filePath);
        var data = JsonConvert.DeserializeObject<MediaLinkStylesData>(jsonData);

        if (data?.CustomStyles != null)
            return data;

        var legacyStyles = JsonConvert.DeserializeObject<ObservableCollection<MediaLinkStyle>>(jsonData);
        return new MediaLinkStylesData
        {
            CustomStyles = legacyStyles ?? new ObservableCollection<MediaLinkStyle>()
        };
    }

    private class MediaLinkStylesData
    {
        public ObservableCollection<MediaLinkStyle> CustomStyles { get; set; } = new();
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
