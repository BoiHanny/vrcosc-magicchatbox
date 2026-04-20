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
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Reads VRChat's output_log.txt in real-time to extract world info,
/// player join/leave events, instance metadata, download progress,
/// encounter tracking, and session statistics.
/// Uses FileShare.ReadWrite to avoid interfering with VRChat's logging.
/// </summary>
public partial class VrcLogModule : ObservableObject, IModule
{
    private static readonly string VrcLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "VRChat", "VRChat");

    // Regex for "Joining wrld_xxx:nnnnn~type(...)~region(xx)" lines
    private static readonly Regex JoiningRegex = new(
        @"Joining (wrld_[a-f0-9\-]+:\d+(?:~\w+\([^)]*\))*)",
        RegexOptions.Compiled);

    private readonly ISettingsProvider<VrcLogSettings> _settingsProvider;
    private readonly IntegrationSettings _integrationSettings;
    private readonly IAppState _appState;
    private readonly IOscSender _oscSender;
    private readonly IUiDispatcher _dispatcher;

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private readonly object _stateLock = new();

    // --- Observable state (UI-bound) ---
    [ObservableProperty] private string _currentWorldName = "Not in a world";
    [ObservableProperty] private int _playerCount;
    [ObservableProperty] private bool _isInstanceMaster;
    [ObservableProperty] private string _instanceType = string.Empty;   // Private/Group/Public
    [ObservableProperty] private string _region = string.Empty;          // EU/US/USW/JP etc
    [ObservableProperty] private string _instanceOwnerName = string.Empty; // resolved from logs
    [ObservableProperty] private bool _isDownloading;

    // Session stats (UI-bound)
    [ObservableProperty] private int _worldsVisited;
    [ObservableProperty] private int _uniquePlayersCount;
    [ObservableProperty] private int _totalJoinEvents;
    [ObservableProperty] private int _totalLeaveEvents;

    // --- Internal tracking (guarded by _stateLock) ---
    private readonly HashSet<string> _currentRoomPlayers = new();
    private string _currentLogFile = string.Empty;
    private long _lastPosition;

    // Bootstrap mode: suppress join/leave toasts until room fully loaded
    private bool _inBootstrapMode;
    private DateTime _lastBootstrapJoin = DateTime.MinValue;

    // Priority-based transient message system (higher priority wins)
    private string _transientMessage = string.Empty;
    private DateTime _transientExpiry = DateTime.MinValue;
    private int _transientPriority;

    // Crasher debounce: one warning per room entry
    private bool _crasherWarnedThisRoom;

    // OSC pulse reentrancy: sequence tokens per parameter path
    private readonly Dictionary<string, int> _pulseSequence = new();

    // Session-lifetime analytics (only from live events, not backfill)
    private readonly HashSet<string> _allPlayersSeen = new();
    private int _sessionWorldsVisited;
    private int _sessionTotalJoins;
    private int _sessionTotalLeaves;

    // Peak player count in current world + world join timestamp
    private int _peakPlayerCount;
    private DateTime _worldJoinedAt = DateTime.MinValue;

    // Instance key for session continuity (full "wrld_xxx:nnnnn~..." join token)
    private string _currentInstanceKey = string.Empty;

    // App-level session tracking (survives app restarts via local file)
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Vrcosc-MagicChatbox", "vrcosc_session.json");
    private DateTimeOffset _appStartedAt = DateTimeOffset.UtcNow;
    private double _totalOfflineSeconds;
    private DateTime _lastSessionSave = DateTime.MinValue;
    private bool _sessionResumed; // true if we resumed from a previous app run

    // Download tracking
    private int _downloadSizeMB;
    private double _downloadSpeedMBps;

    // userId → displayName mapping (populated from OnPlayerJoined/Left lines)
    private readonly Dictionary<string, string> _userIdToName = new(StringComparer.OrdinalIgnoreCase);

    // Pending owner ID to resolve from subsequent player joins
    private string _pendingOwnerUserId = string.Empty;

    // "Seen again" encounter tracking: userId → (lastRoomName, lastSeenTime)
    private readonly Dictionary<string, (string Room, DateTime Time)> _previousRoomPresence = new();
    private readonly HashSet<string> _usersInPreviousRoom = new();
    private string _previousWorldName = string.Empty;

    public VrcLogSettings Settings => _settingsProvider.Value;
    public string Name => "VRChat Radar";
    public bool IsEnabled { get; set; } = true;
    bool IModule.IsRunning => _isRunning;

    /// <summary>Bindable running state for UI (public, raises PropertyChanged).</summary>
    public bool IsRadarRunning => _isRunning;

    /// <summary>Live preview of current chatbox output for the template editor.</summary>
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
        IUiDispatcher dispatcher)
    {
        _settingsProvider = settingsProvider;
        _integrationSettings = integrationSettings;
        _appState = appState;
        _oscSender = oscSender;
        _dispatcher = dispatcher;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void SaveSettings() => _settingsProvider.Save();
    partial void OnCurrentWorldNameChanged(string value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnPlayerCountChanged(int value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnIsInstanceMasterChanged(bool value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnInstanceTypeChanged(string value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnRegionChanged(string value) => OnPropertyChanged(nameof(CurrentOutputPreview));
    partial void OnInstanceOwnerNameChanged(string value) => OnPropertyChanged(nameof(CurrentOutputPreview));

    public Task StartAsync(CancellationToken ct = default)
    {
        if (_isRunning) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _isRunning = true;
        OnPropertyChanged(nameof(IsRadarRunning));
        _ = Task.Run(() => TailLogLoop(_cts.Token), _cts.Token);
        Logging.WriteInfo("VrcRadar: Started log tailing.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        // Save session state before resetting so it survives app restarts
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

    // ──────────────────────────────────────────────────────────────
    // Auto-start/stop reactivity (integration toggle changes)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when IntegrationSettings or IAppState properties change.
    /// Starts or stops the module based on current toggle state.
    /// </summary>
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

    /// <summary>
    /// Returns true if the module should be running based on integration toggles and VR mode.
    /// </summary>
    public bool ShouldBeRunning()
    {
        bool isVR = _appState.IsVRRunning;
        return _integrationSettings.IntgrVrcRadar &&
               (isVR ? _integrationSettings.IntgrVrcRadar_VR : _integrationSettings.IntgrVrcRadar_DESKTOP);
    }

    // ──────────────────────────────────────────────────────────────
    // Output string (called from OSC provider thread)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current chatbox text, respecting DisplayMode and transient priority.
    /// Thread-safe: reads under lock.
    /// </summary>
    public string? GetOutputString()
    {
        lock (_stateLock)
        {
            // Active transient message always wins when valid
            bool hasTransient = DateTime.Now < _transientExpiry && !string.IsNullOrEmpty(_transientMessage);

            // DisplayMode controls baseline behavior
            switch (Settings.DisplayMode)
            {
                case RadarDisplayMode.TransientOnly:
                    return hasTransient ? _transientMessage : null;

                case RadarDisplayMode.JoinLeaveOnly:
                    // Only show join/leave/event transients, never baseline world info
                    return hasTransient ? _transientMessage : null;

                case RadarDisplayMode.CompactInfo:
                    if (hasTransient)
                        return _transientMessage;
                    if (CurrentWorldName == "Not in a world")
                        return null;
                    return $"🌎 {CurrentWorldName} 👥{PlayerCount}";

                case RadarDisplayMode.AlwaysShow:
                case RadarDisplayMode.EventOverlay:
                default:
                    if (hasTransient)
                        return _transientMessage;
                    if (CurrentWorldName == "Not in a world")
                        return null;
                    return BuildWorldTemplate();
            }
        }
    }

    private string BuildWorldTemplate()
    {
        string master = IsInstanceMaster ? Settings.MasterIcon : string.Empty;
        string text = Settings.TemplateWorld
            .Replace("{master}", master)
            .Replace("{world}", CurrentWorldName)
            .Replace("{count}", PlayerCount.ToString())
            .Replace("{peak}", _peakPlayerCount.ToString());

        // Session time in current world (e.g. "12m", "1h23m")
        if (text.Contains("{session_time}"))
        {
            string sessionTime = _worldJoinedAt > DateTime.MinValue
                ? FormatDuration(DateTime.Now - _worldJoinedAt) : string.Empty;
            text = text.Replace("{session_time}", sessionTime);
        }

        // App session time (total time since MagicChatbox session started)
        if (text.Contains("{app_session}"))
        {
            var elapsed = DateTimeOffset.UtcNow - _appStartedAt;
            text = text.Replace("{app_session}", FormatDuration(elapsed));
        }

        // Offline time (accumulated gaps where app wasn't running)
        if (text.Contains("{offline}"))
        {
            string offline = _totalOfflineSeconds >= 60
                ? FormatDuration(TimeSpan.FromSeconds(_totalOfflineSeconds))
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

        // Instance owner name (resolved from logs)
        if (!string.IsNullOrEmpty(InstanceOwnerName))
            text = text.Replace("{owner}", InstanceOwnerName);
        else
            text = text.Replace("{owner}", string.Empty);

        // Clean up any leftover empty separators from unused placeholders
        text = Regex.Replace(text, @"\s*\|\s*\|\s*", " | ");  // collapse "| |" and "| … |"
        text = Regex.Replace(text, @"(\s*\|\s*)+$", "");       // trailing pipes
        text = Regex.Replace(text, @"^\s*\|\s*", "");           // leading pipes
        text = Regex.Replace(text, @"\s{2,}", " ");             // collapse double spaces
        text = text.Trim();

        // Support \n and /n in templates
        text = text.Replace("\\n", "\n").Replace("/n", "\n");

        return text;
    }

    // ──────────────────────────────────────────────────────────────
    // Log tailing loop
    // ──────────────────────────────────────────────────────────────

    private async Task TailLogLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!Directory.Exists(VrcLogDir))
                {
                    await Task.Delay(5000, ct);
                    continue;
                }

                var latestFile = FindLatestLogFile();
                if (latestFile == null)
                {
                    await Task.Delay(2000, ct);
                    continue;
                }

                // Detect new/changed log file (VRChat restart)
                if (latestFile != _currentLogFile)
                {
                    bool isFirstAttach = string.IsNullOrEmpty(_currentLogFile);
                    _currentLogFile = latestFile;
                    ResetState();
                    _lastPosition = BackfillState(latestFile);
                    Logging.WriteInfo($"VrcRadar: Attached to {Path.GetFileName(latestFile)}, backfilled to pos {_lastPosition}");

                    // Resume session only on first attach (app startup), not on log rotation
                    if (isFirstAttach && !string.IsNullOrEmpty(_currentInstanceKey))
                        TryResumeSession();
                }

                // Read new lines
                using var fs = new FileStream(_currentLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Handle file truncation (shouldn't happen, but be safe)
                if (fs.Length < _lastPosition) _lastPosition = 0;

                fs.Seek(_lastPosition, SeekOrigin.Begin);
                using var reader = new StreamReader(fs);

                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    lock (_stateLock)
                    {
                        ParseLogLine(line, isBackfill: false);
                    }
                }
                _lastPosition = fs.Position;

                // Check bootstrap mode exit: if no new joins for 3 seconds
                lock (_stateLock)
                {
                    if (_inBootstrapMode && (DateTime.Now - _lastBootstrapJoin).TotalSeconds > 3)
                    {
                        _inBootstrapMode = false;

                        // After bootstrap, flash session stats if enabled
                        if (Settings.ShowSessionStatsInChatbox && _sessionWorldsVisited > 0)
                        {
                            string stats = Settings.TemplateSessionStats
                                .Replace("{worlds}", _sessionWorldsVisited.ToString())
                                .Replace("{players}", _allPlayersSeen.Count.ToString());
                            SetTransient(stats, Settings.SessionStatsDuration, TransientPriority.SessionStats);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Logging.WriteInfo($"VrcRadar: Log read error: {ex.Message}");
            }

            // Periodic session state save (every 30 seconds)
            if ((DateTime.Now - _lastSessionSave).TotalSeconds >= 30)
            {
                _lastSessionSave = DateTime.Now;
                SaveSessionState();
            }

            await Task.Delay(500, ct);
        }
    }

    /// <summary>
    /// Scans the log file to reconstruct current room state only.
    /// Session stats, encounter tracking, and transient messages are NOT populated.
    /// Uses progressive scan: tries last 2 MB first, then entire file if needed.
    /// </summary>
    private long BackfillState(string filePath)
    {
        try
        {
            long fileLength = new FileInfo(filePath).Length;
            if (fileLength == 0) return 0;

            // Progressive scan: 2 MB window first, then user-configured max (default 10 MB)
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
                    if (startPos > 0) reader.ReadLine(); // skip partial line

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

                    // Include the "Joining wrld_" line that precedes "Entering Room"
                    // — it carries instance type, region, and owner metadata
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

                        // All historical players counted — exit bootstrap mode
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

    // ──────────────────────────────────────────────────────────────
    // Line parsing — called under _stateLock
    // ──────────────────────────────────────────────────────────────

    private void ParseLogLine(string line, bool isBackfill)
    {
        // 1. Instance join metadata (comes before Entering Room)
        // "Joining wrld_xxx:99597~private(usr_xxx)~region(eu)"
        if (line.Contains("Joining wrld_"))
        {
            ParseJoiningLine(line);
            return;
        }

        // 2. Room entry
        if (line.Contains("[Behaviour] Entering Room: "))
        {
            int idx = line.IndexOf("Entering Room: ") + 15;
            string worldName = line[idx..].Trim();

            // Snapshot previous room users for encounter tracking
            if (!isBackfill && Settings.DetectSeenAgain && CurrentWorldName != "Not in a world")
            {
                _usersInPreviousRoom.Clear();
                foreach (var uid in _currentRoomPlayers)
                    _usersInPreviousRoom.Add(uid);
                _previousWorldName = CurrentWorldName;
            }

            _currentRoomPlayers.Clear();
            _crasherWarnedThisRoom = false;
            _inBootstrapMode = true;
            _lastBootstrapJoin = DateTime.Now;
            _pendingOwnerUserId = string.Empty;
            _peakPlayerCount = 0;

            // Use actual log timestamp for world join time (critical for backfill accuracy)
            var logTime = ParseLogTimestamp(line);
            _worldJoinedAt = logTime > DateTime.MinValue ? logTime : DateTime.Now;

            // Clear download state
            _isDownloading = false;
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

        // 3. Left room
        if (line.Contains("[Behaviour] OnLeftRoom"))
        {
            if (!isBackfill && Settings.ShowLeavingRoom && CurrentWorldName != "Not in a world")
            {
                SetTransient(
                    Settings.TemplateLeaving.Replace("{world}", CurrentWorldName),
                    Settings.LeavingDuration,
                    TransientPriority.Leaving);
            }
            return;
        }

        // 4. Instance master
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

        // 5. Player joined
        if (line.Contains("[Behaviour] OnPlayerJoined "))
        {
            var (name, userId) = ExtractPlayerInfo(line, "OnPlayerJoined ");
            if (string.IsNullOrEmpty(userId)) return;

            // Cache userId→displayName for owner resolution
            if (!string.IsNullOrEmpty(name) && name != "Someone")
                _userIdToName[userId] = name;

            // Resolve pending owner if this player is the instance owner
            if (!string.IsNullOrEmpty(_pendingOwnerUserId) && userId == _pendingOwnerUserId)
            {
                _pendingOwnerUserId = string.Empty;
                _dispatcher.BeginInvoke(() => InstanceOwnerName = name);
            }

            bool isNew = _currentRoomPlayers.Add(userId);
            if (!isNew) return;

            // Track peak player count for current world
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
            }

            if (_inBootstrapMode)
            {
                _lastBootstrapJoin = DateTime.Now;
                return; // silently counted
            }

            // "Seen again" detection: only on non-bootstrap joins
            if (!isBackfill && Settings.DetectSeenAgain && _usersInPreviousRoom.Contains(userId))
            {
                var window = TimeSpan.FromMinutes(Settings.SeenAgainWindowMinutes);
                if (_previousRoomPresence.TryGetValue(userId, out var prev)
                    && (DateTime.Now - prev.Time) < window)
                {
                    SetTransient(
                        Settings.TemplateSeenAgain.Replace("{user}", name),
                        Settings.SeenAgainDuration,
                        TransientPriority.SeenAgain);
                }
                // Record this encounter
                _previousRoomPresence[userId] = (CurrentWorldName, DateTime.Now);
            }

            if (!isBackfill && Settings.AnnounceJoins)
            {
                SetTransient(
                    Settings.TemplateJoin.Replace("{user}", name),
                    Settings.JoinLeaveDuration,
                    TransientPriority.JoinLeave);
            }
            return;
        }

        // 6. Player left
        if (line.Contains("[Behaviour] OnPlayerLeft "))
        {
            var (name, userId) = ExtractPlayerInfo(line, "OnPlayerLeft ");
            if (!string.IsNullOrEmpty(userId))
            {
                _currentRoomPlayers.Remove(userId);
                // Cache userId→displayName
                if (!string.IsNullOrEmpty(name) && name != "Someone")
                    _userIdToName[userId] = name;
            }

            _dispatcher.BeginInvoke(() => PlayerCount = Math.Max(0, _currentRoomPlayers.Count));

            if (!isBackfill)
            {
                _sessionTotalLeaves++;
                _dispatcher.BeginInvoke(() => TotalLeaveEvents = _sessionTotalLeaves);
            }

            if (!isBackfill && !_inBootstrapMode && Settings.AnnounceLeaves)
            {
                SetTransient(
                    Settings.TemplateLeave.Replace("{user}", name),
                    Settings.JoinLeaveDuration,
                    TransientPriority.JoinLeave);
            }
            return;
        }

        // 7. World download start
        if (line.Contains("[AssetBundleDownloadManager] Starting download of World"))
        {
            var sizeMatch = Regex.Match(line, @"@ (\d+) MB");
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

        // 8. World download speed update
        if (line.Contains("[AssetBundleDownloadManager] Average download speed:"))
        {
            var speedMatch = Regex.Match(line, @"speed: (\d+) bytes per second");
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

        // 9. Screenshot
        if (line.Contains("Took screenshot to") || line.Contains("Saved screenshot to"))
        {
            if (!isBackfill && Settings.AnnounceScreenshots)
                SetTransient(Settings.TemplateScreenshot, Settings.ScreenshotDuration, TransientPriority.Screenshot);

            if (!isBackfill && Settings.SendCameraFlashOsc && !string.IsNullOrWhiteSpace(Settings.OscCameraFlashParam))
                TriggerOscPulse(Settings.OscCameraFlashParam);
            return;
        }

        // 10. Crasher avatar blocked (debounced per room)
        if (line.Contains("AssetBundleSizeTooLarge"))
        {
            if (_crasherWarnedThisRoom) return;
            _crasherWarnedThisRoom = true;

            if (!isBackfill && Settings.WarnOnCrashers)
                SetTransient(Settings.TemplateCrasher, Settings.CrasherDuration, TransientPriority.Crasher);

            if (!isBackfill && Settings.SendPanicShieldOsc && !string.IsNullOrWhiteSpace(Settings.OscPanicShieldParam))
                TriggerOscPulse(Settings.OscPanicShieldParam);
        }
    }

    /// <summary>
    /// Parses instance type and region from "Joining wrld_..." lines.
    /// Examples:
    ///   Joining wrld_xxx:99597~private(usr_xxx)~region(eu)
    ///   Joining wrld_xxx:99597~group(grp_xxx)~groupAccessType(public)~region(usw)
    ///   Joining wrld_xxx:99597~region(us)
    /// </summary>
    private void ParseJoiningLine(string line)
    {
        // Capture full instance key for session continuity
        var keyMatch = JoiningRegex.Match(line);
        if (keyMatch.Success)
            _currentInstanceKey = keyMatch.Groups[1].Value;

        // Instance type — VRChat log format:
        //   ~private(usr_xxx)          → Invite / Invite+
        //   ~friends(usr_xxx)          → Friends / Friends+
        //   ~hidden(usr_xxx)           → Friends+
        //   ~group(grp_xxx)~groupAccessType(public) → Group Public
        //   ~group(grp_xxx)~groupAccessType(plus)   → Group+
        //   ~group(grp_xxx)~groupAccessType(members) → Group
        //   ~group(grp_xxx)            → Group
        //   (no access tag)            → Public
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
            // Groups use grp_xxx, not usr_xxx — store for potential future lookup
            ownerUserId = ExtractIdFromTag(line, "~group(");
        }

        // Region
        string region = string.Empty;
        var regionMatch = Regex.Match(line, @"~region\((\w+)\)");
        if (regionMatch.Success)
            region = regionMatch.Groups[1].Value;

        // Try to resolve owner name from our userId→name cache
        string ownerName = string.Empty;
        if (!string.IsNullOrEmpty(ownerUserId) && ownerUserId.StartsWith("usr_"))
        {
            if (_userIdToName.TryGetValue(ownerUserId, out var cached))
                ownerName = cached;
            else
                _pendingOwnerUserId = ownerUserId; // resolve when they join
        }

        _dispatcher.BeginInvoke(() =>
        {
            InstanceType = type;
            Region = region;
            InstanceOwnerName = ownerName;
        });
    }

    /// <summary>Extracts the ID inside a tag like ~private(usr_xxx) or ~group(grp_xxx).</summary>
    private static string ExtractIdFromTag(string line, string tagPrefix)
    {
        int start = line.IndexOf(tagPrefix);
        if (start < 0) return string.Empty;
        start += tagPrefix.Length;
        int end = line.IndexOf(')', start);
        return end > start ? line[start..end] : string.Empty;
    }

    /// <summary>
    /// Extracts username and user ID from log lines like:
    /// "[Behaviour] OnPlayerJoined BoiHanny (usr_d4666bd2-e13b-4544-9666-56fd70749200)"
    /// </summary>
    private static (string Name, string UserId) ExtractPlayerInfo(string line, string keyword)
    {
        int start = line.IndexOf(keyword) + keyword.Length;
        int parenOpen = line.IndexOf(" (usr_", start);
        int parenClose = line.IndexOf(')', parenOpen > 0 ? parenOpen : start);

        string name = "Someone";
        string userId = string.Empty;

        if (parenOpen > start)
        {
            name = line[start..parenOpen].Trim();
            userId = line[(parenOpen + 2)..parenClose].Trim(); // skip " ("
        }
        else if (start < line.Length)
        {
            name = line[start..].Trim();
        }

        return (name, userId);
    }

    // ──────────────────────────────────────────────────────────────
    // Transient message system (priority-based)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Priority levels for transient messages. Higher number = higher priority.</summary>
    private static class TransientPriority
    {
        public const int SessionStats = 1;
        public const int JoinLeave = 2;
        public const int Screenshot = 3;
        public const int SeenAgain = 4;
        public const int Leaving = 5;
        public const int Download = 6;
        public const int Crasher = 7;
    }

    /// <summary>
    /// Sets a transient message only if its priority >= current active transient,
    /// or the current transient has expired.
    /// </summary>
    private void SetTransient(string message, int seconds, int priority)
    {
        bool currentExpired = DateTime.Now >= _transientExpiry;
        if (!currentExpired && priority < _transientPriority)
            return; // lower-priority message won't override active higher-priority one

        // Support \n and /n in transient templates
        _transientMessage = message.Replace("\\n", "\n").Replace("/n", "\n");
        _transientExpiry = DateTime.Now.AddSeconds(seconds);
        _transientPriority = priority;
    }

    // ──────────────────────────────────────────────────────────────
    // OSC pulse & helpers
    // ──────────────────────────────────────────────────────────────

    private void TriggerOscPulse(string address)
    {
        int seq;
        lock (_pulseSequence)
        {
            _pulseSequence.TryGetValue(address, out int current);
            seq = current + 1;
            _pulseSequence[address] = seq;
        }

        Task.Run(async () =>
        {
            try
            {
                _oscSender.SendOscParam(address, true);
                await Task.Delay(150);

                lock (_pulseSequence)
                {
                    if (_pulseSequence.TryGetValue(address, out int latest) && latest != seq)
                        return;
                }
                _oscSender.SendOscParam(address, false);
            }
            catch { /* OSC send failures are non-fatal */ }
        });
    }

    /// <summary>
    /// Evicts encounter history entries older than 2× the seen-again window.
    /// Called on each room change to prevent unbounded growth.
    /// </summary>
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

    private void ResetState()
    {
        lock (_stateLock)
        {
            _currentRoomPlayers.Clear();
            _crasherWarnedThisRoom = false;
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
            _worldJoinedAt = DateTime.MinValue;
            _currentInstanceKey = string.Empty;
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
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Log timestamp parsing
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts timestamp from VRChat log lines formatted as "yyyy.MM.dd HH:mm:ss ...".
    /// Returns DateTime.MinValue if parsing fails.
    /// </summary>
    private static DateTime ParseLogTimestamp(string line)
    {
        if (line.Length < 19) return DateTime.MinValue;
        var span = line.AsSpan(0, 19);
        return DateTime.TryParseExact(span, "yyyy.MM.dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var ts)
            ? ts : DateTime.MinValue;
    }

    // ──────────────────────────────────────────────────────────────
    // Session persistence (survives app restarts)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Persisted session state — written periodically and on shutdown.
    /// Keyed by instance key so only the exact same VRChat instance resumes.
    /// </summary>
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

    /// <summary>
    /// Called once at startup after backfill. If the persisted session matches
    /// the current instance key and the log file is recent, resumes timing.
    /// </summary>
    private void TryResumeSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return;
            var json = File.ReadAllText(SessionFilePath);
            var saved = JsonSerializer.Deserialize<PersistedSession>(json);
            if (saved == null) return;

            // Must be same instance (same wrld:instance join token)
            if (string.IsNullOrEmpty(saved.InstanceKey) ||
                saved.InstanceKey != _currentInstanceKey)
            {
                Logging.WriteInfo("VrcRadar: Session file exists but instance key mismatch — starting fresh.");
                return;
            }

            // Log file must be the same one (or at least recent)
            string currentLogName = string.IsNullOrEmpty(_currentLogFile)
                ? string.Empty : Path.GetFileName(_currentLogFile);
            if (!string.IsNullOrEmpty(saved.LogFileName) &&
                saved.LogFileName != currentLogName)
            {
                // Different log file — VRChat may have restarted
                // Only resume if the saved LastActiveAt is within 10 minutes
                if ((DateTimeOffset.UtcNow - saved.LastActiveAt).TotalMinutes > 10)
                {
                    Logging.WriteInfo("VrcRadar: Session stale (log file changed, >10min ago) — starting fresh.");
                    return;
                }
            }

            // Resume: restore world join time and calculate offline gap
            _worldJoinedAt = saved.WorldJoinedAt.LocalDateTime;
            _appStartedAt = saved.AppStartedAt;
            double offlineGap = (DateTimeOffset.UtcNow - saved.LastActiveAt).TotalSeconds;
            _totalOfflineSeconds = saved.TotalOfflineSeconds + Math.Max(0, offlineGap);
            _sessionResumed = true;

            Logging.WriteInfo($"VrcRadar: Resumed session — world joined {saved.WorldJoinedAt:HH:mm}, offline gap {offlineGap:F0}s, total offline {_totalOfflineSeconds:F0}s");
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"VrcRadar: Failed to resume session: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves session state to disk. Called periodically (every 30s) and on stop.
    /// Thread-safe: snapshots under lock, writes outside it.
    /// </summary>
    internal void SaveSessionState()
    {
        PersistedSession snapshot;
        lock (_stateLock)
        {
            if (string.IsNullOrEmpty(_currentInstanceKey) ||
                _worldJoinedAt == DateTime.MinValue)
                return; // nothing meaningful to persist

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

    /// <summary>Formats a TimeSpan as a compact duration string (e.g. "4m", "1h23m", "2h").</summary>
    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{(int)ts.TotalSeconds}s";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
        return $"{(int)ts.TotalMinutes}m";
    }
}
