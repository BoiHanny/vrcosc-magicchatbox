using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public static class VrcLogText
{
    public const int MaxNameChars = 32;

    public static string Name(string? name) => SegmentWriter.Truncate(SegmentWriter.Tidy(name), MaxNameChars);

    public static string Duration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60)
            return Measure((int)ts.TotalSeconds, "s");
        if (ts.TotalHours >= 1)
            return Measure((int)ts.TotalHours, "h") + Measure(ts.Minutes, "m", "D2");

        return Measure((int)ts.TotalMinutes, "m");
    }

    private static string Measure(int amount, string unit, string? format = null)
        => new SegmentWriter()
            .Field(OscText.Value(amount.ToString(format, CultureInfo.InvariantCulture)), OscText.Unit(unit))
            .Text;

    public static string FitToBudget(Func<string, string> render, string worldName, int budget)
    {
        if (budget <= 0)
            return string.Empty;

        string text = render(worldName);
        if (text.Length <= budget)
            return text;

        string shorter = SegmentWriter.Truncate(worldName, worldName.Length - (text.Length - budget));
        if (shorter.Length < worldName.Length)
            text = render(shorter);

        return SegmentWriter.Truncate(text, budget);
    }
}

public partial class VrcLogModule : ObservableObject, IModule
{
    private static readonly string VrcLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "VRChat", "VRChat");

    [GeneratedRegex(@"Joining (wrld_[a-f0-9\-]+:\d+(?:~\w+\([^)]*\))*)")]
    private static partial Regex JoiningRegex();

    [GeneratedRegex(@"\s*\|\s*\|\s*")]
    private static partial Regex EmptySeparatorRegex();

    [GeneratedRegex(@"(\s*\|\s*)+$")]
    private static partial Regex TrailingSeparatorRegex();

    [GeneratedRegex(@"^\s*\|\s*")]
    private static partial Regex LeadingSeparatorRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex RepeatedSpaceRegex();

    [GeneratedRegex(@"@ (\d+) MB")]
    private static partial Regex DownloadSizeRegex();

    [GeneratedRegex(@"speed: (\d+) bytes per second")]
    private static partial Regex DownloadSpeedRegex();

    [GeneratedRegex(@"~region\((\w+)\)")]
    private static partial Regex InstanceRegionRegex();

    private const int ActivePollIntervalMs = 500;
    private const int IdlePollIntervalMs = 5000;
    private const int MaxLogReadChunkBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan LogFileRecheckInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProcessCheckInterval = TimeSpan.FromSeconds(2.5);

    private readonly ISettingsProvider<VrcLogSettings> _settingsProvider;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IAppState _appState;
    private readonly IOscSender _oscSender;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService? _toast;
    private readonly IProcessPresenceService? _processPresence;

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private readonly object _stateLock = new();

    [ObservableProperty] private string _currentWorldName = "Not in a world";
    [ObservableProperty] private int _playerCount;
    [ObservableProperty] private bool _isInstanceMaster;
    [ObservableProperty] private string _instanceType = string.Empty;
    [ObservableProperty] private string _region = string.Empty;
    [ObservableProperty] private string _instanceOwnerName = string.Empty;
    [ObservableProperty] private bool _isDownloading;

    [ObservableProperty] private int _worldsVisited;
    [ObservableProperty] private int _uniquePlayersCount;
    [ObservableProperty] private int _totalJoinEvents;
    [ObservableProperty] private int _totalLeaveEvents;

    private readonly HashSet<string> _currentRoomPlayers = new();
    private string _currentLogFile = string.Empty;
    private long _lastPosition;
    private FileStream? _logStream;
    private DateTime _lastLogFileCheck = DateTime.MinValue;
    private DateTime _lastProcessCheck = DateTime.MinValue;
    private bool _vrchatProcessDetected;

    private bool _inBootstrapMode;
    private DateTime _lastBootstrapJoin = DateTime.MinValue;

    private string _transientMessage = string.Empty;
    private DateTime _transientExpiry = DateTime.MinValue;
    private int _transientPriority;

    private bool _avatarBlockedWarnedThisRoom;

    private readonly Dictionary<string, int> _pulseSequence = new();

    private readonly HashSet<string> _allPlayersSeen = new();
    private int _sessionWorldsVisited;
    private int _sessionTotalJoins;
    private int _sessionTotalLeaves;

    private int _peakPlayerCount;
    private DateTime _worldJoinedAt = DateTime.MinValue;

    public DateTimeOffset? WorldJoinedAt => _worldJoinedAt > DateTime.MinValue
        ? new DateTimeOffset(_worldJoinedAt, TimeZoneInfo.Local.GetUtcOffset(_worldJoinedAt))
        : null;

    private string _currentInstanceKey = string.Empty;

    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Vrcosc-MagicChatbox", "vrcosc_session.json");
    private DateTimeOffset _appStartedAt = DateTimeOffset.UtcNow;
    private double _totalOfflineSeconds;
    private DateTime _lastSessionSave = DateTime.MinValue;

    private int _downloadSizeMB;
    private double _downloadSpeedMBps;

    private readonly Dictionary<string, string> _userIdToName = new(StringComparer.OrdinalIgnoreCase);

    private string _pendingOwnerUserId = string.Empty;

    private readonly Dictionary<string, (string Room, DateTime Time)> _previousRoomPresence = new();
    private readonly HashSet<string> _usersInPreviousRoom = new();
    private string _previousWorldName = string.Empty;

    private readonly Dictionary<string, EncounterRecord> _encounterRecords = new();
    private System.Collections.ObjectModel.ObservableCollection<EncounterRecord>? _sessionEncounters;

    public System.Collections.ObjectModel.ObservableCollection<EncounterRecord> SessionEncounters
        => _sessionEncounters ??= new();

    private ICollectionView? _filteredEncountersView;

    public ICollectionView FilteredEncountersView
    {
        get
        {
            if (_filteredEncountersView == null)
            {
                _filteredEncountersView = System.Windows.Data.CollectionViewSource.GetDefaultView(SessionEncounters);
                _filteredEncountersView.Filter = obj =>
                    obj is EncounterRecord r && r.TimesSeenThisSession >= Settings.MinEncounterCount;
            }
            return _filteredEncountersView;
        }
    }

    public void RefreshEncounterFilter() => _filteredEncountersView?.Refresh();

    private DateTime _lastLogActivity = DateTime.UtcNow;
    private DateTime _lastVrchatProcessSeen = DateTime.UtcNow;

    [ObservableProperty] private bool _isVrchatProcessRunning;

    [ObservableProperty] private int _peakPlayerCountThisSession;

    public event Action? OnVrcWorldStateChanged;

    public VrcLogSettings Settings => _settingsProvider.Value;
    public string Name => "VRChat Radar";
    public bool IsEnabled { get; set; } = true;
    bool IModule.IsRunning => _isRunning;

    public bool IsRadarRunning => _isRunning;

    public string CurrentOutputPreview
    {
        get
        {
            var text = GetOutputString();
            return string.IsNullOrWhiteSpace(text) ? "(no output — waiting for world data)" : text;
        }
    }

    public VrcLogModule(
        ISettingsProvider<VrcLogSettings> settingsProvider,
        IntegrationSettings integrationSettings,
        IAppState appState,
        IOscSender oscSender,
        IUiDispatcher dispatcher,
        IPrivacyConsentService consentService,
        IToastService? toast = null,
        IProcessPresenceService? processPresence = null)
    {
        _settingsProvider = settingsProvider;
        _integrationSettings = integrationSettings;
        _appState = appState;
        _oscSender = oscSender;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _toast = toast;
        _processPresence = processPresence;

        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VrcLogSettings.MinEncounterCount))
                _dispatcher.BeginInvoke(RefreshEncounterFilter);
        };

        _consentService.ConsentChanged += OnConsentChanged;
    }

    private void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
    {
        if (e.Hook != PrivacyHook.VrcLogReader)
            return;

        if (e.NewState == ConsentState.Denied && _isRunning)
        {
            _ = StopAsync();
            _toast?.Show("🔒 VRC Log Reader", "VRChat log reading paused — privacy consent revoked.", ToastType.Privacy, key: "vrclog-privacy-denied");
        }
        else if (e.NewState == ConsentState.Approved && !_isRunning && ShouldBeRunning())
        {
            _ = StartAsync();
        }
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void SaveSettings() => _settingsProvider.Save();
    partial void OnCurrentWorldNameChanged(string value) { OnPropertyChanged(nameof(CurrentOutputPreview)); OnVrcWorldStateChanged?.Invoke(); }
    partial void OnPlayerCountChanged(int value) { OnPropertyChanged(nameof(CurrentOutputPreview)); OnVrcWorldStateChanged?.Invoke(); }
    partial void OnIsInstanceMasterChanged(bool value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnInstanceTypeChanged(string value) { OnPropertyChanged(nameof(CurrentOutputPreview)); OnVrcWorldStateChanged?.Invoke(); }
    partial void OnRegionChanged(string value) { OnPropertyChanged(nameof(CurrentOutputPreview)); OnVrcWorldStateChanged?.Invoke(); }
    partial void OnInstanceOwnerNameChanged(string value) => OnPropertyChanged(nameof(CurrentOutputPreview));

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_isRunning) return Task.CompletedTask;

        if (!_consentService.IsApproved(PrivacyHook.VrcLogReader))
        {
            Logging.WriteInfo("VrcRadar: Start blocked — privacy consent not granted.");
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _isRunning = true;
        OnPropertyChanged(nameof(IsRadarRunning));
        _ = Task.Run(() => TailLogLoop(_cts.Token), _cts.Token);
        Logging.WriteInfo("VrcRadar: Started log tailing.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        SaveSessionState();

        _isRunning = false;
        OnPropertyChanged(nameof(IsRadarRunning));
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        ResetState();
        Logging.WriteInfo("VrcRadar: Stopped.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        SaveSessionState();
        _isRunning = false;
        OnPropertyChanged(nameof(IsRadarRunning));
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "IntgrVrcRadar" or "IntgrVrcRadar_VR" or "IntgrVrcRadar_DESKTOP" or "IsVRRunning")
        {
            if (ShouldBeRunning())
            {
                if (!_isRunning)
                    _ = StartAsync();
            }
            else
            {
                if (_isRunning)
                    _ = StopAsync();
            }
        }
    }

    public bool ShouldBeRunning()
    {
        if (!_consentService.IsApproved(PrivacyHook.VrcLogReader))
            return false;

        bool isVR = _appState.IsVRRunning;
        return _integrationSettings.IntgrVrcRadar &&
               (isVR ? _integrationSettings.IntgrVrcRadar_VR : _integrationSettings.IntgrVrcRadar_DESKTOP);
    }

    public string? GetOutputString(int budget = Constants.OscMaxMessageLength)
    {
        lock (_stateLock)
        {
            bool hasTransient = DateTime.Now < _transientExpiry && !string.IsNullOrEmpty(_transientMessage);

            string? Transient() => SegmentWriter.Truncate(_transientMessage, budget) is { Length: > 0 } t ? t : null;

            switch (Settings.DisplayMode)
            {
                case RadarDisplayMode.TransientOnly:
                    return hasTransient ? Transient() : null;

                case RadarDisplayMode.JoinLeaveOnly:
                    return hasTransient ? Transient() : null;

                case RadarDisplayMode.CompactInfo:
                    if (hasTransient)
                        return Transient();
                    if (CurrentWorldName == "Not in a world")
                        return null;
                    return VrcLogText.FitToBudget(
                        world => $"🌎 {world} 👥{PlayerCount}", VrcLogText.Name(CurrentWorldName), budget);

                case RadarDisplayMode.AlwaysShow:
                case RadarDisplayMode.EventOverlay:
                default:
                    if (hasTransient)
                        return Transient();
                    if (CurrentWorldName == "Not in a world")
                        return null;
                    return VrcLogText.FitToBudget(
                        BuildWorldTemplate, VrcLogText.Name(CurrentWorldName), budget);
            }
        }
    }

    private string BuildWorldTemplate(string worldName)
    {
        string master = IsInstanceMaster ? Settings.MasterIcon : string.Empty;
        string text = Settings.TemplateWorld
            .Replace("{master}", master)
            .Replace("{world}", worldName)
            .Replace("{count}", PlayerCount.ToString())
            .Replace("{peak}", _peakPlayerCount.ToString())
            .Replace("{peak_session}", PeakPlayerCountThisSession.ToString())
            .Replace("{worlds}", _sessionWorldsVisited.ToString())
            .Replace("{players}", _allPlayersSeen.Count.ToString());

        if (text.Contains("{session_time}"))
        {
            string sessionTime = _worldJoinedAt > DateTime.MinValue
                ? VrcLogText.Duration(DateTime.Now - _worldJoinedAt) : string.Empty;
            text = text.Replace("{session_time}", sessionTime);
        }

        if (text.Contains("{app_session}"))
        {
            var elapsed = DateTimeOffset.UtcNow - _appStartedAt;
            text = text.Replace("{app_session}", VrcLogText.Duration(elapsed));
        }

        if (text.Contains("{offline}"))
        {
            string offline = _totalOfflineSeconds >= 60
                ? VrcLogText.Duration(TimeSpan.FromSeconds(_totalOfflineSeconds))
                : string.Empty;
            text = text.Replace("{offline}", offline);
        }

        if (Settings.ShowInstanceType && !string.IsNullOrEmpty(InstanceType))
            text = text.Replace("{type}", InstanceType);
        else
            text = text.Replace("{type}", string.Empty);

        if (Settings.ShowRegion && !string.IsNullOrEmpty(Region))
            text = text.Replace("{region}", Region.ToUpperInvariant());
        else
            text = text.Replace("{region}", string.Empty);

        text = text.Replace("{owner}", VrcLogText.Name(InstanceOwnerName));

        text = EmptySeparatorRegex().Replace(text, " | ");
        text = TrailingSeparatorRegex().Replace(text, "");
        text = LeadingSeparatorRegex().Replace(text, "");
        text = RepeatedSpaceRegex().Replace(text, " ");
        text = text.Trim();

        text = text.Replace("\\n", "\n").Replace("/n", "\n");

        return text;
    }

    private async Task TailLogLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!Directory.Exists(VrcLogDir))
                    {
                        CloseLogStream();
                        await Task.Delay(5000, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (_logStream == null || (DateTime.UtcNow - _lastLogFileCheck) >= LogFileRecheckInterval)
                    {
                        _lastLogFileCheck = DateTime.UtcNow;

                        var latestFile = FindLatestLogFile();
                        if (latestFile == null)
                        {
                            CloseLogStream();
                            await Task.Delay(2000, ct).ConfigureAwait(false);
                            continue;
                        }

                        if (latestFile != _currentLogFile)
                        {
                            CloseLogStream();

                            bool isFirstAttach = string.IsNullOrEmpty(_currentLogFile);
                            _currentLogFile = latestFile;
                            ResetState();
                            _lastPosition = BackfillState(latestFile);
                            Logging.WriteInfo($"VrcRadar: Attached to {Path.GetFileName(latestFile)}, backfilled to pos {_lastPosition}");

                            if (CurrentWorldName != "Not in a world")
                            {
                                var fileAge = DateTime.UtcNow - new FileInfo(latestFile).LastWriteTimeUtc;
                                var timeout = TimeSpan.FromMinutes(Math.Clamp(Settings.SessionTimeoutMinutes, 5, 90));
                                bool isStale = fileAge > timeout;

                                if (!isStale && Settings.UseWindowDetection)
                                {
                                    try
                                    {
                                        bool running = IsVrchatProcessPresent();
                                        if (!running)
                                        {
                                            isStale = true;
                                            Logging.WriteInfo("VrcRadar: VRChat process not running — treating backfilled session as stale.");
                                        }
                                        else
                                        {
                                            _lastVrchatProcessSeen = DateTime.UtcNow;
                                        }
                                    }
                                    catch { }
                                }

                                if (isStale)
                                {
                                    Logging.WriteInfo($"VrcRadar: Log file is {fileAge.TotalMinutes:F0}m old (timeout {timeout.TotalMinutes}m) — discarding stale session.");
                                    EndSessionDueToTimeout();
                                }
                            }

                            if (isFirstAttach && !string.IsNullOrEmpty(_currentInstanceKey))
                                TryResumeSession();
                        }

                        _logStream ??= new FileStream(_currentLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    }

                    if (_logStream != null && await ReadNewLogLinesAsync(_logStream, ct).ConfigureAwait(false))
                        _lastLogActivity = DateTime.UtcNow;

                    lock (_stateLock)
                    {
                        if (_inBootstrapMode && (DateTime.Now - _lastBootstrapJoin).TotalSeconds > 3)
                        {
                            _inBootstrapMode = false;

                            if (Settings.ShowSessionStatsInChatbox && _sessionWorldsVisited > 0)
                            {
                                string stats = Settings.TemplateSessionStats
                                    .Replace("{worlds}", _sessionWorldsVisited.ToString())
                                    .Replace("{players}", _allPlayersSeen.Count.ToString())
                                    .Replace("{peak_session}", PeakPlayerCountThisSession.ToString());
                                SetTransient(stats, Settings.SessionStatsDuration, TransientPriority.SessionStats);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    CloseLogStream();
                    Logging.WriteInfo($"VrcRadar: Log read error: {ex.Message}");
                }

                if ((DateTime.Now - _lastSessionSave).TotalSeconds >= 30)
                {
                    _lastSessionSave = DateTime.Now;
                    SaveSessionState();
                }

                if (Settings.UseWindowDetection && (DateTime.UtcNow - _lastProcessCheck) >= ProcessCheckInterval)
                {
                    _lastProcessCheck = DateTime.UtcNow;
                    try
                    {
                        var running = IsVrchatProcessPresent();

                        _vrchatProcessDetected = running;

                        if (running)
                            _lastVrchatProcessSeen = DateTime.UtcNow;

                        if (running != IsVrchatProcessRunning)
                            _dispatcher.BeginInvoke(() => IsVrchatProcessRunning = running);
                    }
                    catch { }
                }

                CheckSessionTimeout();

                await Task.Delay(
                    Settings.UseWindowDetection && !_vrchatProcessDetected
                        ? IdlePollIntervalMs
                        : ActivePollIntervalMs,
                    ct).ConfigureAwait(false);
            }
        }
        finally
        {
            CloseLogStream();
        }
    }

    private bool IsVrchatProcessPresent()
    {
        if (_processPresence != null)
            return _processPresence.IsRunning("VRChat");

        var procs = System.Diagnostics.Process.GetProcessesByName("VRChat");
        try
        {
            return procs.Length > 0;
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }

    private async Task<bool> ReadNewLogLinesAsync(FileStream fs, CancellationToken ct)
    {
        long length = fs.Length;
        if (length < _lastPosition)
            _lastPosition = 0;

        long available = length - _lastPosition;
        if (available <= 0)
            return false;

        int wanted = (int)Math.Min(available, MaxLogReadChunkBytes);
        var buffer = new byte[wanted];

        fs.Seek(_lastPosition, SeekOrigin.Begin);

        int read = 0;
        while (read < wanted)
        {
            int got = await fs.ReadAsync(buffer.AsMemory(read, wanted - read), ct).ConfigureAwait(false);
            if (got <= 0) break;
            read += got;
        }

        if (read <= 0)
            return false;

        int lastNewline = Array.LastIndexOf(buffer, (byte)'\n', read - 1, read);
        if (lastNewline < 0)
            return false;

        int completeBytes = lastNewline + 1;
        string text = System.Text.Encoding.UTF8.GetString(buffer, 0, completeBytes);
        _lastPosition += completeBytes;

        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        bool parsedAny = false;
        int lineStart = 0;
        while (lineStart < text.Length)
        {
            int newline = text.IndexOf('\n', lineStart);
            if (newline < 0) break;

            int lineEnd = newline;
            if (lineEnd > lineStart && text[lineEnd - 1] == '\r') lineEnd--;

            string logLine = text[lineStart..lineEnd];
            parsedAny = true;

            lock (_stateLock)
            {
                ParseLogLine(logLine, isBackfill: false);
            }

            lineStart = newline + 1;
        }

        return parsedAny;
    }

    private void CloseLogStream()
    {
        _logStream?.Dispose();
        _logStream = null;
    }

    private long BackfillState(string filePath)
    {
        try
        {
            long fileLength = new FileInfo(filePath).Length;
            if (fileLength == 0) return 0;

            int maxMb = Math.Clamp(Settings.MaxBackfillSizeMb, 1, 200);
            long maxScan = maxMb * 1024L * 1024L;
            long[] scanWindows = { 2 * 1024 * 1024, Math.Min(fileLength, maxScan) };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            foreach (long window in scanWindows)
            {
                if (cts.IsCancellationRequested) break;

                long startPos = Math.Max(0, fileLength - window);

                List<string> lines = new();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(startPos, SeekOrigin.Begin);
                    using var reader = new StreamReader(fs);
                    if (startPos > 0) reader.ReadLine();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (cts.IsCancellationRequested) break;
                        lines.Add(line);
                    }
                }

                if (cts.IsCancellationRequested)
                {
                    Logging.WriteInfo($"VrcRadar: Backfill timed out after 5s scanning {window / 1024}KB");
                    break;
                }

                int lastRoomIdx = -1;
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    if (lines[i].Contains("[Behaviour] Entering Room: "))
                    {
                        lastRoomIdx = i;
                        break;
                    }
                }

                if (lastRoomIdx >= 0 || startPos == 0)
                {
                    int replayStart = lastRoomIdx >= 0 ? lastRoomIdx : 0;

                    if (lastRoomIdx > 0)
                    {
                        for (int i = lastRoomIdx - 1; i >= Math.Max(0, lastRoomIdx - 10); i--)
                        {
                            if (lines[i].Contains("Joining wrld_"))
                            {
                                replayStart = i;
                                break;
                            }
                        }
                    }
                    lock (_stateLock)
                    {
                        for (int i = replayStart; i < lines.Count; i++)
                            ParseLogLine(lines[i], isBackfill: true);

                        _inBootstrapMode = false;
                    }
                    Logging.WriteInfo($"VrcRadar: Backfill scanned {window / 1024}KB, replayed {lines.Count - replayStart} lines, {_currentRoomPlayers.Count} players in room");
                    return fileLength;
                }
            }

            return fileLength;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar: Backfill error: {ex.Message}");
            return 0;
        }
    }

    private void ParseLogLine(string line, bool isBackfill)
    {
        if (line.Contains("Joining wrld_"))
        {
            ParseJoiningLine(line);
            return;
        }

        if (line.Contains("[Behaviour] Entering Room: "))
        {
            int idx = line.IndexOf("Entering Room: ") + 15;
            string worldName = line[idx..].Trim();

            if (!isBackfill && Settings.DetectSeenAgain && CurrentWorldName != "Not in a world")
            {
                _usersInPreviousRoom.Clear();
                foreach (var uid in _currentRoomPlayers)
                    _usersInPreviousRoom.Add(uid);
                _previousWorldName = CurrentWorldName;
            }

            if (!isBackfill)
                FinalizeActiveEncounters();

            _currentRoomPlayers.Clear();
            _avatarBlockedWarnedThisRoom = false;
            _inBootstrapMode = true;
            _lastBootstrapJoin = DateTime.Now;
            _pendingOwnerUserId = string.Empty;
            _peakPlayerCount = 0;

            var logTime = ParseLogTimestamp(line);
            _worldJoinedAt = logTime > DateTime.MinValue ? logTime : DateTime.Now;

            IsDownloading = false;
            _downloadSizeMB = 0;
            _downloadSpeedMBps = 0;

            if (!isBackfill)
            {
                _sessionWorldsVisited++;
                EvictStaleEncounters();
            }

            _dispatcher.BeginInvoke(() =>
            {
                CurrentWorldName = worldName;
                PlayerCount = 0;
                IsInstanceMaster = false;
                IsDownloading = false;
                InstanceOwnerName = string.Empty;
                if (!isBackfill)
                {
                    WorldsVisited = _sessionWorldsVisited;
                }
            });
            return;
        }

        if (line.Contains("[Behaviour] OnLeftRoom"))
        {
            return;
        }

        if (line.Contains("[Behaviour] I am MASTER"))
        {
            _dispatcher.BeginInvoke(() => IsInstanceMaster = true);
            return;
        }
        if (line.Contains("[Behaviour] I am *NOT* MASTER"))
        {
            _dispatcher.BeginInvoke(() => IsInstanceMaster = false);
            return;
        }

        if (line.Contains("[Behaviour] OnPlayerJoined "))
        {
            var (name, userId) = ExtractPlayerInfo(line, "OnPlayerJoined ");
            if (string.IsNullOrEmpty(userId)) return;

            if (!string.IsNullOrEmpty(name) && name != "Someone")
                _userIdToName[userId] = name;

            if (!string.IsNullOrEmpty(_pendingOwnerUserId) && userId == _pendingOwnerUserId)
            {
                _pendingOwnerUserId = string.Empty;
                _dispatcher.BeginInvoke(() => InstanceOwnerName = name);
            }

            bool isNew = _currentRoomPlayers.Add(userId);
            if (!isNew) return;

            if (_currentRoomPlayers.Count > _peakPlayerCount)
                _peakPlayerCount = _currentRoomPlayers.Count;

            _dispatcher.BeginInvoke(() => PlayerCount = _currentRoomPlayers.Count);

            if (!isBackfill)
            {
                _allPlayersSeen.Add(userId);
                _sessionTotalJoins++;
                _dispatcher.BeginInvoke(() =>
                {
                    UniquePlayersCount = _allPlayersSeen.Count;
                    TotalJoinEvents = _sessionTotalJoins;
                });

                RecordEncounter(userId, name, CurrentWorldName);
            }

            if (_inBootstrapMode)
            {
                _lastBootstrapJoin = DateTime.Now;
                return;
            }

            if (!isBackfill && Settings.DetectSeenAgain && _usersInPreviousRoom.Contains(userId))
            {
                var window = TimeSpan.FromMinutes(Settings.SeenAgainWindowMinutes);
                if (_previousRoomPresence.TryGetValue(userId, out var prev)
                    && (DateTime.Now - prev.Time) < window)
                {
                    if (Settings.ShowSeenAgainNotification)
                    {
                        SetTransient(
                            Settings.TemplateSeenAgain.Replace("{user}", VrcLogText.Name(name)),
                            Settings.SeenAgainDuration,
                            TransientPriority.SeenAgain);
                    }
                }
                _previousRoomPresence[userId] = (CurrentWorldName, DateTime.Now);
            }

            if (!isBackfill && Settings.AnnounceJoins)
            {
                SetTransient(
                    Settings.TemplateJoin.Replace("{user}", VrcLogText.Name(name)),
                    Settings.JoinLeaveDuration,
                    TransientPriority.JoinLeave);
            }
            return;
        }

        if (line.Contains("[Behaviour] OnPlayerLeft "))
        {
            var (name, userId) = ExtractPlayerInfo(line, "OnPlayerLeft ");
            if (!string.IsNullOrEmpty(userId))
            {
                _currentRoomPlayers.Remove(userId);
                if (!string.IsNullOrEmpty(name) && name != "Someone")
                    _userIdToName[userId] = name;
            }

            _dispatcher.BeginInvoke(() => PlayerCount = Math.Max(0, _currentRoomPlayers.Count));

            if (!isBackfill)
            {
                _sessionTotalLeaves++;
                _dispatcher.BeginInvoke(() => TotalLeaveEvents = _sessionTotalLeaves);

                if (!string.IsNullOrEmpty(userId))
                    CloseEncounter(userId);
            }

            if (!isBackfill && !_inBootstrapMode && Settings.AnnounceLeaves)
            {
                SetTransient(
                    Settings.TemplateLeave.Replace("{user}", VrcLogText.Name(name)),
                    Settings.JoinLeaveDuration,
                    TransientPriority.JoinLeave);
            }
            return;
        }

        if (line.Contains("[AssetBundleDownloadManager] Starting download of World"))
        {
            var sizeMatch = DownloadSizeRegex().Match(line);
            if (sizeMatch.Success)
            {
                _downloadSizeMB = int.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                _dispatcher.BeginInvoke(() => IsDownloading = true);

                if (!isBackfill && Settings.ShowWorldDownload)
                {
                    string msg = Settings.TemplateDownload
                        .Replace("{size}", _downloadSizeMB.ToString())
                        .Replace("{speed}", "...");
                    SetTransient(msg, Settings.DownloadDuration, TransientPriority.Download);
                }
            }
            return;
        }

        if (line.Contains("[AssetBundleDownloadManager] Average download speed:"))
        {
            var speedMatch = DownloadSpeedRegex().Match(line);
            if (speedMatch.Success && _downloadSizeMB > 0)
            {
                _downloadSpeedMBps = Math.Round(
                    double.Parse(speedMatch.Groups[1].Value, CultureInfo.InvariantCulture) / (1024 * 1024), 1);

                if (!isBackfill && Settings.ShowWorldDownload)
                {
                    string msg = Settings.TemplateDownload
                        .Replace("{size}", _downloadSizeMB.ToString())
                        .Replace("{speed}", _downloadSpeedMBps.ToString("F1", CultureInfo.InvariantCulture));
                    SetTransient(msg, Settings.DownloadDuration, TransientPriority.Download);
                }
            }
            return;
        }

        if (line.Contains("Took screenshot to") || line.Contains("Saved screenshot to"))
        {
            if (!isBackfill && Settings.AnnounceScreenshots)
                SetTransient(Settings.TemplateScreenshot, Settings.ScreenshotDuration, TransientPriority.Screenshot);

            if (!isBackfill && Settings.SendCameraFlashOsc && !string.IsNullOrWhiteSpace(Settings.OscCameraFlashParam))
                TriggerOscPulse(Settings.OscCameraFlashParam);
            return;
        }

        if (line.Contains("AssetBundleSizeTooLarge"))
        {
            if (_avatarBlockedWarnedThisRoom) return;
            _avatarBlockedWarnedThisRoom = true;

            if (!isBackfill && Settings.WarnOnAvatarBlocked)
                SetTransient(Settings.TemplateAvatarBlocked, Settings.AvatarBlockedDuration, TransientPriority.AvatarBlocked);
            return;
        }
    }

    private void ParseJoiningLine(string line)
    {
        var keyMatch = JoiningRegex().Match(line);
        if (keyMatch.Success)
            _currentInstanceKey = keyMatch.Groups[1].Value;

        string type = "Public";
        string ownerUserId = string.Empty;

        if (line.Contains("~private("))
        {
            type = line.Contains("~canRequestInvite") ? "Invite+" : "Invite";
            ownerUserId = ExtractIdFromTag(line, "~private(");
        }
        else if (line.Contains("~hidden("))
        {
            type = "Friends+";
            ownerUserId = ExtractIdFromTag(line, "~hidden(");
        }
        else if (line.Contains("~friends("))
        {
            type = "Friends";
            ownerUserId = ExtractIdFromTag(line, "~friends(");
        }
        else if (line.Contains("~group("))
        {
            if (line.Contains("~groupAccessType(public)"))
                type = "Group Public";
            else if (line.Contains("~groupAccessType(plus)"))
                type = "Group+";
            else
                type = "Group";
            ownerUserId = ExtractIdFromTag(line, "~group(");
        }

        string region = string.Empty;
        var regionMatch = InstanceRegionRegex().Match(line);
        if (regionMatch.Success)
            region = regionMatch.Groups[1].Value;

        string ownerName = string.Empty;
        if (!string.IsNullOrEmpty(ownerUserId) && ownerUserId.StartsWith("usr_"))
        {
            if (_userIdToName.TryGetValue(ownerUserId, out var cached))
                ownerName = cached;
            else
                _pendingOwnerUserId = ownerUserId;
        }

        _dispatcher.BeginInvoke(() =>
        {
            InstanceType = type;
            Region = region;
            InstanceOwnerName = ownerName;
        });
    }

    private static string ExtractIdFromTag(string line, string tagPrefix)
    {
        int start = line.IndexOf(tagPrefix);
        if (start < 0) return string.Empty;
        start += tagPrefix.Length;
        int end = line.IndexOf(')', start);
        return end > start ? line[start..end] : string.Empty;
    }

    private static (string Name, string UserId) ExtractPlayerInfo(string line, string keyword)
    {
        int start = line.IndexOf(keyword) + keyword.Length;
        int parenOpen = line.IndexOf(" (usr_", start);
        int parenClose = parenOpen > start ? line.IndexOf(')', parenOpen) : -1;

        string name = "Someone";
        string userId = string.Empty;

        if (parenOpen > start)
        {
            name = line[start..parenOpen].Trim();
            if (parenClose > parenOpen)
                userId = line[(parenOpen + 2)..parenClose].Trim();
        }
        else if (start < line.Length)
        {
            name = line[start..].Trim();
        }

        return (name, userId);
    }

    private static class TransientPriority
    {
        public const int SessionStats = 1;
        public const int JoinLeave = 2;
        public const int Screenshot = 3;
        public const int SeenAgain = 4;
        public const int Download = 5;
        public const int AvatarBlocked = 6;
    }

    private void SetTransient(string message, int seconds, int priority)
    {
        bool currentExpired = DateTime.Now >= _transientExpiry;
        if (!currentExpired && priority < _transientPriority)
            return;

        _transientMessage = message.Replace("\\n", "\n").Replace("/n", "\n");
        _transientExpiry = DateTime.Now.AddSeconds(seconds);
        _transientPriority = priority;
    }

    private void TriggerOscPulse(string address)
    {
        int seq;
        lock (_pulseSequence)
        {
            _pulseSequence.TryGetValue(address, out int current);
            seq = current + 1;
            _pulseSequence[address] = seq;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                _oscSender.SendOscParam(address, true);
                await Task.Delay(150).ConfigureAwait(false);

                lock (_pulseSequence)
                {
                    if (_pulseSequence.TryGetValue(address, out int latest) && latest != seq)
                        return;
                }
                _oscSender.SendOscParam(address, false);
            }
            catch { }
        });
    }

    private void EvictStaleEncounters()
    {
        var cutoff = DateTime.Now - TimeSpan.FromMinutes(Settings.SeenAgainWindowMinutes * 2);
        var staleKeys = _previousRoomPresence
            .Where(kv => kv.Value.Time < cutoff)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in staleKeys)
            _previousRoomPresence.Remove(key);
    }

    private string? FindLatestLogFile()
    {
        try
        {
            return new DirectoryInfo(VrcLogDir)
                .GetFiles("output_log_*.txt")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }

    private void CheckSessionTimeout()
    {
        if (CurrentWorldName == "Not in a world") return;

        var timeout = TimeSpan.FromMinutes(Math.Clamp(Settings.SessionTimeoutMinutes, 5, 90));
        var logStale = (DateTime.UtcNow - _lastLogActivity) > timeout;

        if (Settings.UseWindowDetection)
        {
            var processStale = (DateTime.UtcNow - _lastVrchatProcessSeen) > timeout;

            var processGone = (DateTime.UtcNow - _lastVrchatProcessSeen) > TimeSpan.FromSeconds(30);
            var logQuiet = (DateTime.UtcNow - _lastLogActivity) > TimeSpan.FromSeconds(30);
            if (processGone && logQuiet && !IsVrchatProcessRunning)
            {
                Logging.WriteInfo("VrcRadar: Session timeout — VRChat process not detected for 30s+ with no log activity.");
                EndSessionDueToTimeout();
                return;
            }

            if (logStale && processStale)
            {
                Logging.WriteInfo($"VrcRadar: Session timeout — no logs for {timeout.TotalMinutes}m and VRChat process not detected.");
                EndSessionDueToTimeout();
            }
        }
        else
        {
            if (logStale)
            {
                Logging.WriteInfo($"VrcRadar: Session timeout — no logs for {timeout.TotalMinutes}m.");
                EndSessionDueToTimeout();
            }
        }
    }

    private void EndSessionDueToTimeout()
    {
        FinalizeActiveEncounters();

        lock (_stateLock)
        {
            _currentRoomPlayers.Clear();
            _avatarBlockedWarnedThisRoom = false;
            _inBootstrapMode = false;
            _currentInstanceKey = string.Empty;
            _peakPlayerCount = 0;
        }

        _dispatcher.BeginInvoke(() =>
        {
            CurrentWorldName = "Not in a world";
            PlayerCount = 0;
            IsInstanceMaster = false;
            InstanceType = string.Empty;
            Region = string.Empty;
            InstanceOwnerName = string.Empty;
        });
    }

    private void RecordEncounter(string userId, string displayName, string worldName)
    {
        if (!Settings.ShowEncounterTable && !Settings.DetectSeenAgain) return;

        if (!_encounterRecords.TryGetValue(userId, out var record))
        {
            record = new EncounterRecord
            {
                PlayerName = displayName,
                LastWorldName = worldName,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                TimesSeenThisSession = 1,
                IsCurrentlyPresent = true,
                CurrentRoomJoinedAt = DateTime.UtcNow,
            };
            _encounterRecords[userId] = record;
            _dispatcher.BeginInvoke(() => SessionEncounters.Add(record));
        }
        else
        {
            record.TimesSeenThisSession++;
            record.LastWorldName = worldName;
            record.LastSeenAt = DateTime.UtcNow;
            record.IsCurrentlyPresent = true;
            record.CurrentRoomJoinedAt = DateTime.UtcNow;
        }

        int currentCount = _currentRoomPlayers.Count;
        if (currentCount > PeakPlayerCountThisSession)
            PeakPlayerCountThisSession = currentCount;
    }

    private void CloseEncounter(string userId)
    {
        if (_encounterRecords.TryGetValue(userId, out var record) && record.IsCurrentlyPresent)
        {
            record.IsCurrentlyPresent = false;
            if (record.CurrentRoomJoinedAt.HasValue)
            {
                record.TotalTimeTogetherSeconds +=
                    (DateTime.UtcNow - record.CurrentRoomJoinedAt.Value).TotalSeconds;
                record.CurrentRoomJoinedAt = null;
            }
        }
    }

    private void FinalizeActiveEncounters()
    {
        foreach (var record in _encounterRecords.Values)
        {
            if (record.IsCurrentlyPresent && record.CurrentRoomJoinedAt.HasValue)
            {
                record.TotalTimeTogetherSeconds +=
                    (DateTime.UtcNow - record.CurrentRoomJoinedAt.Value).TotalSeconds;
                record.CurrentRoomJoinedAt = null;
                record.IsCurrentlyPresent = false;
            }
        }
    }

    public string? GetCurrentJoinUrl()
    {
        if (string.IsNullOrEmpty(_currentInstanceKey)) return null;

        if (!IsPublicInstance()) return null;

        var parts = _currentInstanceKey.Split(':');
        if (parts.Length < 2) return null;

        var worldId = parts[0];
        var instanceId = string.Join(":", parts.Skip(1));
        return $"https://vrchat.com/home/launch?worldId={Uri.EscapeDataString(worldId)}&instanceId={Uri.EscapeDataString(instanceId)}";
    }

    private bool IsPublicInstance()
    {
        return !string.IsNullOrEmpty(_currentInstanceKey) &&
               !_currentInstanceKey.Contains("~private(") &&
               !_currentInstanceKey.Contains("~friends(") &&
               !_currentInstanceKey.Contains("~hidden(") &&
               !_currentInstanceKey.Contains("~group(");
    }

    private void ResetState()
    {
        lock (_stateLock)
        {
            _currentRoomPlayers.Clear();
            _inBootstrapMode = false;
            _transientMessage = string.Empty;
            _transientExpiry = DateTime.MinValue;
            _transientPriority = 0;
            _downloadSizeMB = 0;
            _downloadSpeedMBps = 0;
            _usersInPreviousRoom.Clear();
            _previousWorldName = string.Empty;
            _allPlayersSeen.Clear();
            _sessionWorldsVisited = 0;
            _sessionTotalJoins = 0;
            _sessionTotalLeaves = 0;
            _previousRoomPresence.Clear();
            _pulseSequence.Clear();
            _pendingOwnerUserId = string.Empty;
            _peakPlayerCount = 0;
            PeakPlayerCountThisSession = 0;
            _worldJoinedAt = DateTime.MinValue;
            _currentInstanceKey = string.Empty;
            _encounterRecords.Clear();
            _lastLogActivity = DateTime.UtcNow;
            _lastVrchatProcessSeen = DateTime.UtcNow;
            _lastProcessCheck = DateTime.MinValue;
        }

        _dispatcher.BeginInvoke(() =>
        {
            CurrentWorldName = "Not in a world";
            PlayerCount = 0;
            IsInstanceMaster = false;
            InstanceType = string.Empty;
            Region = string.Empty;
            InstanceOwnerName = string.Empty;
            IsDownloading = false;
            WorldsVisited = 0;
            UniquePlayersCount = 0;
            IsVrchatProcessRunning = false;
            SessionEncounters.Clear();
        });
    }

    private static DateTime ParseLogTimestamp(string line)
    {
        if (line.Length < 19) return DateTime.MinValue;
        var span = line.AsSpan(0, 19);
        return DateTime.TryParseExact(span, "yyyy.MM.dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts)
            ? ts : DateTime.MinValue;
    }

    private sealed class PersistedSession
    {
        public string InstanceKey { get; set; } = string.Empty;
        public string WorldName { get; set; } = string.Empty;
        public DateTimeOffset WorldJoinedAt { get; set; }
        public DateTimeOffset AppStartedAt { get; set; }
        public DateTimeOffset LastActiveAt { get; set; }
        public double TotalOfflineSeconds { get; set; }
        public string LogFileName { get; set; } = string.Empty;
    }

    private void TryResumeSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return;
            var json = File.ReadAllText(SessionFilePath);
            var saved = JsonSerializer.Deserialize<PersistedSession>(json);
            if (saved == null) return;

            if (string.IsNullOrEmpty(saved.InstanceKey) ||
                saved.InstanceKey != _currentInstanceKey)
            {
                Logging.WriteInfo("VrcRadar: Session file exists but instance key mismatch — starting fresh.");
                return;
            }

            string currentLogName = string.IsNullOrEmpty(_currentLogFile)
                ? string.Empty : Path.GetFileName(_currentLogFile);
            if (!string.IsNullOrEmpty(saved.LogFileName) &&
                saved.LogFileName != currentLogName)
            {
                if ((DateTimeOffset.UtcNow - saved.LastActiveAt).TotalMinutes > 10)
                {
                    Logging.WriteInfo("VrcRadar: Session stale (log file changed, >10min ago) — starting fresh.");
                    return;
                }
            }

            _worldJoinedAt = saved.WorldJoinedAt.LocalDateTime;
            _appStartedAt = saved.AppStartedAt;
            double offlineGap = (DateTimeOffset.UtcNow - saved.LastActiveAt).TotalSeconds;
            _totalOfflineSeconds = saved.TotalOfflineSeconds + Math.Max(0, offlineGap);

            Logging.WriteInfo($"VrcRadar: Resumed session — world joined {saved.WorldJoinedAt:HH:mm}, offline gap {offlineGap:F0}s, total offline {_totalOfflineSeconds:F0}s");
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar: Failed to resume session: {ex.Message}");
        }
    }

    internal void SaveSessionState()
    {
        PersistedSession snapshot;
        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(_currentInstanceKey) ||
                _worldJoinedAt == DateTime.MinValue)
                return;

            snapshot = new PersistedSession
            {
                InstanceKey = _currentInstanceKey,
                WorldName = CurrentWorldName,
                WorldJoinedAt = new DateTimeOffset(_worldJoinedAt, TimeZoneInfo.Local.GetUtcOffset(_worldJoinedAt)),
                AppStartedAt = _appStartedAt,
                LastActiveAt = DateTimeOffset.UtcNow,
                TotalOfflineSeconds = _totalOfflineSeconds,
                LogFileName = string.IsNullOrEmpty(_currentLogFile)
                    ? string.Empty : Path.GetFileName(_currentLogFile)
            };
        }

        try
        {
            var dir = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = SessionFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SessionFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar: Failed to save session: {ex.Message}");
        }
    }
}
