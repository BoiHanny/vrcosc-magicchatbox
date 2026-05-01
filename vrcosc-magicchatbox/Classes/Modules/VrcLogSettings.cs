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
    [ObservableProperty] private bool _showInstanceType = true;
    [ObservableProperty] private bool _showRegion = true;
    [ObservableProperty] private bool _showWorldDownload = true;
    [ObservableProperty] private bool _detectSeenAgain = false;
    [ObservableProperty] private int _seenAgainWindowMinutes = 5;
    [ObservableProperty] private bool _showSessionStatsInChatbox = false;
    /// <summary>Show notification when a player is "seen again" in a new world. Requires DetectSeenAgain.</summary>
    [ObservableProperty] private bool _showSeenAgainNotification = true;
    /// <summary>Warn when VRChat blocks an avatar (oversized asset bundle / performance shield).</summary>
    [ObservableProperty] private bool _warnOnAvatarBlocked = true;

    // --- Session detection ---
    /// <summary>Use VRChat.exe process detection alongside log activity for session management.</summary>
    [ObservableProperty] private bool _useWindowDetection = true;
    /// <summary>End session after this many minutes of inactivity (no logs AND no VRChat process). Range: 5–90.</summary>
    [ObservableProperty] private int _sessionTimeoutMinutes = 15;

    // --- Encounter tracking ---
    /// <summary>Show the encounter tracking table in the UI.</summary>
    [ObservableProperty] private bool _showEncounterTable = false;
    /// <summary>Minimum times a player must be seen before appearing in the encounter table. Range: 1–10.</summary>
    [ObservableProperty] private int _minEncounterCount = 2;

    // --- Output templates ---
    [ObservableProperty] private string _templateWorld = "{master}🌎 {world} | 👥 {count} | {type} {region}";
    [ObservableProperty] private string _templateJoin = "👋 {user} joined!";
    [ObservableProperty] private string _templateLeave = "🏃 {user} left";
    [ObservableProperty] private string _templateScreenshot = "📸 *Click!* Just took a picture!";
    [ObservableProperty] private string _templateDownload = "⏳ Loading world... {size}MB @ {speed}MB/s";
    [ObservableProperty] private string _templateSeenAgain = "👀 {user} is here again!";
    [ObservableProperty] private string _templateSessionStats = "📊 {worlds} worlds | {players} players met | Peak: {peak_session}";
    [ObservableProperty] private string _templateAvatarBlocked = "⚠️ Avatar blocked by performance shield";
    [ObservableProperty] private string _masterIcon = "👑 ";

    // --- Transient durations (seconds) ---
    [ObservableProperty] private int _joinLeaveDuration = 4;
    [ObservableProperty] private int _screenshotDuration = 4;
    [ObservableProperty] private int _downloadDuration = 8;
    [ObservableProperty] private int _seenAgainDuration = 5;
    /// <summary>How long session stats stay visible after a world change (seconds). Range: 5–120.</summary>
    [ObservableProperty] private int _sessionStatsDuration = 15;
    [ObservableProperty] private int _avatarBlockedDuration = 6;

    // --- OSC pulse triggers ---
    [ObservableProperty] private bool _sendCameraFlashOsc = false;
    [ObservableProperty] private string _oscCameraFlashParam = "/avatar/parameters/CameraFlash";

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
        ("World Host",  "🏠 {world} | 👥 {count} unique | Peak: {peak_session} | {type}"),
        ("Host Stats",  "🏠 Hosting: {world}\\n👥 {count}/{peak} | 🔄 {worlds} worlds | 📊 {players} unique"),
        ("Event Host",  "🎉 {world} | 👥 {count} online | Peak: {peak_session} | ⏱️ {session_time}"),
    ];
}
