using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public enum RadarDisplayMode
{
    [Description("Always show where I am")]
    AlwaysShow,

    [Description("Only speak up when something happens")]
    TransientOnly,

    [Description("Show where I am, and interrupt for events")]
    EventOverlay,

    [Description("Only speak up when people come and go")]
    JoinLeaveOnly,

    [Description("Just the world name and how many people")]
    CompactInfo
}

public partial class VrcLogSettings : VersionedSettings
{
    [ObservableProperty] private RadarDisplayMode _displayMode = RadarDisplayMode.EventOverlay;

    [ObservableProperty] private bool _announceJoins = true;
    [ObservableProperty] private bool _announceLeaves = true;
    [ObservableProperty] private bool _announceScreenshots = true;
    [ObservableProperty] private bool _showInstanceType = true;
    [ObservableProperty] private bool _showRegion = true;
    [ObservableProperty] private bool _showWorldDownload = true;
    [ObservableProperty] private bool _detectSeenAgain = false;
    [ObservableProperty] private int _seenAgainWindowMinutes = 5;
    [ObservableProperty] private bool _showSessionStatsInChatbox = false;
    [ObservableProperty] private bool _showSeenAgainNotification = true;
    [ObservableProperty] private bool _warnOnAvatarBlocked = true;

    [ObservableProperty] private bool _useWindowDetection = true;
    [ObservableProperty] private int _sessionTimeoutMinutes = 15;

    [ObservableProperty] private bool _showEncounterTable = false;
    [ObservableProperty] private int _minEncounterCount = 2;

    [ObservableProperty] private string _templateWorld = "{master}🌎 {world} | 👥 {count} | {type} {region}";
    [ObservableProperty] private string _templateJoin = "👋 {user} joined!";
    [ObservableProperty] private string _templateLeave = "🏃 {user} left";
    [ObservableProperty] private string _templateScreenshot = "📸 *Click!* Just took a picture!";
    [ObservableProperty] private string _templateDownload = "⏳ Loading world... {size}MB @ {speed}MB/s";
    [ObservableProperty] private string _templateSeenAgain = "👀 {user} is here again!";
    [ObservableProperty] private string _templateSessionStats = "📊 {worlds} worlds | {players} players met | Peak: {peak_session}";
    [ObservableProperty] private string _templateAvatarBlocked = "⚠️ Avatar blocked by performance shield";
    [ObservableProperty] private string _masterIcon = "👑 ";

    [ObservableProperty] private int _joinLeaveDuration = 4;
    [ObservableProperty] private int _screenshotDuration = 4;
    [ObservableProperty] private int _downloadDuration = 8;
    [ObservableProperty] private int _seenAgainDuration = 5;
    [ObservableProperty] private int _sessionStatsDuration = 15;
    [ObservableProperty] private int _avatarBlockedDuration = 6;

    [ObservableProperty] private bool _sendCameraFlashOsc = false;
    [ObservableProperty] private string _oscCameraFlashParam = "/avatar/parameters/CameraFlash";

    [ObservableProperty] private int _maxLogEntries = 50000;
    [ObservableProperty] private int _maxBackfillSizeMb = 10;

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
