namespace vrcosc_magicchatbox.Core.Privacy;

public static class PrivacyHookInfo
{
    public static (string Name, string Icon) Get(PrivacyHook hook) => hook switch
    {
        PrivacyHook.HardwareMonitor => ("Hardware Monitor", "🖥️"),
        PrivacyHook.WindowActivity => ("Window Activity", "📋"),
        PrivacyHook.MediaSession => ("Media Session", "🎵"),
        PrivacyHook.AfkSensor => ("AFK Sensor", "💤"),
        PrivacyHook.InternetAccess => ("Internet Access", "🌐"),
        PrivacyHook.VrTrackerBattery => ("VR Tracker Battery", "🎮"),
        PrivacyHook.NetworkStats => ("Network Statistics", "📶"),
        PrivacyHook.SoundpadBridge => ("Soundpad Bridge", "🔊"),
        PrivacyHook.VrcLogReader => ("VRChat Log Reader", "📡"),
        PrivacyHook.VrPerformance => ("VR Performance", "🎯"),
        PrivacyHook.VoicemodControl => ("Voicemod Control", "🎙️"),
        _ => (hook.ToString(), "🔒"),
    };
}
