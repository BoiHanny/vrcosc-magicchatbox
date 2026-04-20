using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Controls how VrcRadar occupies the chatbox.
/// </summary>
public enum RadarDisplayMode
{
    /// <summary>Always show world info + player count in chatbox.</summary>
    AlwaysShow,
    /// <summary>Only show text when events happen (joins, leaves, etc.), then disappear.</summary>
    TransientOnly,
    /// <summary>Show world info by default, override with event text temporarily.</summary>
    EventOverlay,
    /// <summary>Only show join/leave activity — world info is hidden, events only.</summary>
    JoinLeaveOnly,
    /// <summary>Minimal one-line: world name and count only, no extras.</summary>
    CompactInfo
}

/// <summary>
/// Persisted settings for the VRChat Radar (local log parser) integration.
/// Template placeholders: {master}, {world}, {count}, {peak}, {session_time},
///   {app_session}, {offline}, {user}, {type}, {region}, {owner}
/// </summary>
public partial class VrcLogSettings : VersionedSettings
{
    // --- Display mode ---
    [ObservableProperty] private RadarDisplayMode _displayMode = RadarDisplayMode.EventOverlay;

    // --- Feature toggles ---
    [ObservableProperty] private bool _announceJoins = true;
    [ObservableProperty] private bool _announceLeaves = true;
    [ObservableProperty] private bool _announceScreenshots = true;
    [ObservableProperty] private bool _warnOnCrashers = false;
    [ObservableProperty] private bool _showInstanceType = true;
    [ObservableProperty] private bool _showRegion = true;
    [ObservableProperty] private bool _showWorldDownload = true;
    [ObservableProperty] private bool _showLeavingRoom = true;
    [ObservableProperty] private bool _detectSeenAgain = false;
    [ObservableProperty] private int _seenAgainWindowMinutes = 5;
    [ObservableProperty] private bool _showSessionStatsInChatbox = false;

    // --- Output templates ---
    [ObservableProperty] private string _templateWorld = "{master}🌎 {world} | 👥 {count} | {type} {region}";
    [ObservableProperty] private string _templateJoin = "👋 {user} joined!";
    [ObservableProperty] private string _templateLeave = "🏃 {user} left";
    [ObservableProperty] private string _templateScreenshot = "📸 *Click!* Just took a picture!";
    [ObservableProperty] private string _templateCrasher = "⚠️ Crasher avatar blocked!";
    [ObservableProperty] private string _templateDownload = "⏳ Loading world... {size}MB @ {speed}MB/s";
    [ObservableProperty] private string _templateLeaving = "🚪 Leaving {world}...";
    [ObservableProperty] private string _templateSeenAgain = "👀 {user} is here again!";
    [ObservableProperty] private string _templateSessionStats = "📊 {worlds} worlds | {players} players met";
    [ObservableProperty] private string _masterIcon = "👑 ";

    // --- Transient durations (seconds) ---
    [ObservableProperty] private int _joinLeaveDuration = 4;
    [ObservableProperty] private int _screenshotDuration = 4;
    [ObservableProperty] private int _crasherDuration = 5;
    [ObservableProperty] private int _downloadDuration = 8;
    [ObservableProperty] private int _leavingDuration = 3;
    [ObservableProperty] private int _seenAgainDuration = 5;
    [ObservableProperty] private int _sessionStatsDuration = 5;

    // --- OSC pulse triggers ---
    [ObservableProperty] private bool _sendCameraFlashOsc = false;
    [ObservableProperty] private string _oscCameraFlashParam = "/avatar/parameters/CameraFlash";
    [ObservableProperty] private bool _sendPanicShieldOsc = false;
    [ObservableProperty] private string _oscPanicShieldParam = "/avatar/parameters/PanicShield";

    // --- Performance / limits ---
    /// <summary>Maximum log lines to keep in memory. Older entries are evicted. Range: 500–100000.</summary>
    [ObservableProperty] private int _maxLogEntries = 50000;
    /// <summary>Maximum bytes (MB) to scan during backfill. Range: 1–200.</summary>
    [ObservableProperty] private int _maxBackfillSizeMb = 10;

    /// <summary>
    /// Built-in world info template presets. Not persisted — UI only.
    /// </summary>
    public static readonly (string Name, string Value)[] WorldTemplatePresets =
    [
        ("Detailed",    "{master}🌎 {world} | 👥 {count} | {type} {region}"),
        ("With Owner",  "{master}🌎 {world} | 👥 {count} | {type} {region}\\n🏠 {owner}"),
        ("Compact",     "🌎 {world} 👥{count}"),
        ("Full Stats",  "{master}🌎 {world} | 👥 {count}/{peak} | {type} {region} | ⏱️ {session_time}"),
        ("Session",     "🌎 {world} 👥{count} | ⏱️ {session_time} | 📊 {app_session}"),
        ("Multi-line",  "🌎 {world}\\n👥 {count} | {type} {region}"),
        ("Minimal",     "{world} ({count})"),
    ];
}
