namespace vrcosc_magicchatbox.Core.Privacy;

public enum PrivacyHook
{
    /// <summary>LibreHardwareMonitor — loads WinRing0.sys kernel driver for CPU/GPU/VRAM sensors.</summary>
    HardwareMonitor = 0,

    /// <summary>UIAutomation + GetForegroundWindow — reads currently focused window title and process name.</summary>
    WindowActivity = 1,

    /// <summary>Windows SMTC — reads media metadata (title, artist) from media players.</summary>
    MediaSession = 2,

    /// <summary>GetLastInputInfo — reads the timestamp of the last keyboard/mouse event.</summary>
    AfkSensor = 3,

    /// <summary>Outbound HTTP — Twitch API, Pulsoid heart-rate API, Weather service.</summary>
    InternetAccess = 4,

    /// <summary>Valve.VR / OpenVR — connects to SteamVR to read HMD, controller, and tracker battery levels.</summary>
    VrTrackerBattery = 5,

    /// <summary>System.Net.NetworkInformation — reads network interface byte counters for throughput display.</summary>
    NetworkStats = 6,

    /// <summary>Named pipe IPC — connects to the Soundpad desktop application for playback control.</summary>
    SoundpadBridge = 7,
}
