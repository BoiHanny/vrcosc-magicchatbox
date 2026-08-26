using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.Services;

public sealed class MediaLinkPersistenceService : IMediaLinkPersistenceService, IDisposable
{
    private readonly IEnvironmentService _env;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly WindowActivityDisplayState _windowActivity;
    private readonly IAppHistoryService _appHistory;
    private readonly IUiDispatcher _dispatcher;

    private const string MediaLinkStylesFileName = "MediaLinkStyles.json";

    private static readonly TimeSpan StyleSaveDebounce = TimeSpan.FromSeconds(2);

    private readonly Lock _styleSaveGate = new();
    private readonly HashSet<MediaLinkStyle> _watchedStyles = new();
    private Timer? _styleSaveTimer;
    private ObservableCollection<MediaLinkStyle>? _watchedStyleCollection;
    private bool _disposed;

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

        WatchStyleEdits();
        _mediaLink.PropertyChanged += MediaLinkStateChanged;
    }

    private void MediaLinkStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaLinkDisplayState.MediaLinkSeekbarStyles))
            WatchStyleEdits();
        else if (e.PropertyName == nameof(MediaLinkDisplayState.SelectedMediaLinkSeekbarStyle))
            QueueStyleSave();
    }

    private void WatchStyleEdits()
    {
        if (_watchedStyleCollection != null)
            _watchedStyleCollection.CollectionChanged -= StyleCollectionChanged;

        foreach (MediaLinkStyle watched in _watchedStyles)
            watched.PropertyChanged -= StyleEdited;

        _watchedStyles.Clear();

        _watchedStyleCollection = _mediaLink.MediaLinkSeekbarStyles;
        if (_watchedStyleCollection == null)
            return;

        _watchedStyleCollection.CollectionChanged += StyleCollectionChanged;

        foreach (MediaLinkStyle style in _watchedStyleCollection)
        {
            style.PropertyChanged += StyleEdited;
            _watchedStyles.Add(style);
        }
    }

    private void StyleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (MediaLinkStyle removed in e.OldItems.OfType<MediaLinkStyle>())
            {
                removed.PropertyChanged -= StyleEdited;
                _watchedStyles.Remove(removed);
            }
        }

        if (e.NewItems != null)
        {
            foreach (MediaLinkStyle added in e.NewItems.OfType<MediaLinkStyle>())
            {
                if (_watchedStyles.Add(added))
                    added.PropertyChanged += StyleEdited;
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
            WatchStyleEdits();
    }

    private void StyleEdited(object? sender, PropertyChangedEventArgs e) => QueueStyleSave();

    private void QueueStyleSave()
    {
        if (_disposed)
            return;

        lock (_styleSaveGate)
        {
            _styleSaveTimer ??= new Timer(_ => FlushStyleSave(), null, Timeout.Infinite, Timeout.Infinite);
            _styleSaveTimer.Change(StyleSaveDebounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void FlushStyleSave()
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _mediaLink.PropertyChanged -= MediaLinkStateChanged;

        if (_watchedStyleCollection != null)
            _watchedStyleCollection.CollectionChanged -= StyleCollectionChanged;

        foreach (MediaLinkStyle watched in _watchedStyles)
            watched.PropertyChanged -= StyleEdited;

        _watchedStyles.Clear();

        lock (_styleSaveGate)
        {
            _styleSaveTimer?.Dispose();
            _styleSaveTimer = null;
        }
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
            await _dispatcher.InvokeAsync(() =>
            {
                lock (MediaSessionSettings.SavedSessionsLock)
                {
                    _mediaLink.SavedSessionSettings = loadedSessions;
                }
            });
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            await _dispatcher.InvokeAsync(() =>
            {
                lock (MediaSessionSettings.SavedSessionsLock)
                {
                    if (_mediaLink.SavedSessionSettings == null)
                        _mediaLink.SavedSessionSettings = new List<MediaSessionSettings>();
                }
            });
        }
    }

    public void SaveMediaSessions()
    {
        try
        {
            if (_appHistory.CreateIfMissing(_env.DataPath) != true)
            {
                return;
            }

            List<MediaSessionSettings> sessions;
            lock (MediaSessionSettings.SavedSessionsLock)
            {
                sessions = _mediaLink.SavedSessionSettings?.ToList() ?? new List<MediaSessionSettings>();
            }
            string json = JsonConvert.SerializeObject(sessions);

            if (json == null)
            {
                return;
            }

            AtomicFileWriter.WriteAllText(Path.Combine(_env.DataPath, "LastMediaLinkSessions.json"), json);
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

        MediaLinkStyle? template = _mediaLink.SelectedMediaLinkSeekbarStyle
            ?? _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault(s => s.SystemDefault)
            ?? _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();

        MediaLinkStyle newStyle = template != null
            ? CloneMediaLinkStyle(template)
            : new MediaLinkStyle { ProgressBarLength = 8 };

        newStyle.ID = nextAvailableID;
        newStyle.SystemDefault = false;

        _mediaLink.MediaLinkSeekbarStyles.Add(newStyle);
        _mediaLink.SelectedMediaLinkSeekbarStyle = newStyle;

        SaveMediaLinkStyles();

        Logging.WriteInfo($"New media link style with ID {nextAvailableID} added.");
    }

    public void DeleteSelectedSeekbarStyleAndSelectDefault()
    {
        var selectedStyle = _mediaLink.SelectedMediaLinkSeekbarStyle;
        if (selectedStyle == null)
        {
            return;
        }

        if (selectedStyle.SystemDefault)
        {
            Logging.WriteInfo("Cannot delete system default media link style.");
            return;
        }

        int deletedId = selectedStyle.ID;

        _mediaLink.MediaLinkSeekbarStyles.Remove(selectedStyle);
        _mediaLink.SelectedMediaLinkSeekbarStyle = _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault(s => s.SystemDefault)
            ?? _mediaLink.MediaLinkSeekbarStyles.FirstOrDefault();

        SaveMediaLinkStyles();

        Logging.WriteInfo($"Media link style with ID {deletedId} deleted.");
    }

    public void ExportSeekbarStyles(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var selectedStyle = _mediaLink.SelectedMediaLinkSeekbarStyle;
        var data = new MediaLinkStylesData
        {
            CustomStyles = new ObservableCollection<MediaLinkStyle>(
                _mediaLink.MediaLinkSeekbarStyles.Where(s => !s.SystemDefault)),
            SelectedStyleId = selectedStyle != null && !selectedStyle.SystemDefault
                ? selectedStyle.ID
                : null
        };

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        AtomicFileWriter.WriteAllText(filePath, JsonConvert.SerializeObject(data, Formatting.Indented));
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
                AtomicFileWriter.WriteAllText(filePath, jsonData);

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
