using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;

namespace vrcosc_magicchatbox.UI.Dialogs;

/// <summary>
/// First-run wizard: privacy hook configuration with TOS/SLA links in the footer.
/// DialogResult is true only when the user clicks "Accept &amp; Continue".
/// </summary>
public partial class TosAndPrivacyWizard : Window
{
    private readonly IPrivacyConsentService _consentService;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly ObservableCollection<HookItem> _items = new();

    public TosAndPrivacyWizard(
        IPrivacyConsentService consentService,
        ISettingsProvider<AppSettings> appSettingsProvider)
    {
        _consentService = consentService;
        _appSettingsProvider = appSettingsProvider;

        InitializeComponent();

        foreach (PrivacyHook hook in System.Enum.GetValues<PrivacyHook>())
            _items.Add(CreateItem(hook));

        HookList.ItemsSource = _items;
    }

    private HookItem CreateItem(PrivacyHook hook) => hook switch
    {
        PrivacyHook.HardwareMonitor => new HookItem(hook,
            title: "🖥️  Hardware Monitor  (CPU · GPU · VRAM · temps · wattage)",
            description: "Reads CPU/GPU load, temperature, and power draw using LibreHardwareMonitor. " +
                         "Requires loading a low-level kernel driver (WinRing0.sys) on first use.",
            warning: "⚠  Windows Defender may flag WinRing0.sys. This is a known false-positive — not malware. " +
                     "Denying this still shows RAM usage.",
            isApproved: DefaultApproved(hook)),

        PrivacyHook.WindowActivity => new HookItem(hook,
            title: "📋  Window Activity  (active app · window title)",
            description: "Reads the name and title of your currently focused window using the Windows " +
                         "accessibility API (UIAutomation + GetForegroundWindow). No screenshots are taken.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.MediaSession => new HookItem(hook,
            title: "🎵  Media Session  (song title · artist · playback state)",
            description: "Reads metadata from music and video players via Windows SMTC. " +
                         "Only the currently playing track info is read — no file access.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.AfkSensor => new HookItem(hook,
            title: "💤  AFK Sensor  (last input timestamp)",
            description: "Reads the timestamp of your last keyboard or mouse event to detect inactivity. " +
                         "No keystrokes or mouse positions are recorded.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.InternetAccess => new HookItem(hook,
            title: "🌐  Internet Access  (Twitch · Pulsoid · Weather · OpenAI · TTS · Speech)",
            description: "Allows outbound HTTP requests to third-party services: Twitch API, Pulsoid heart-rate, " +
                         "weather data, OpenAI IntelliChat, TikTok TTS, and speech detection. " +
                         "Only data you configure is requested — no telemetry.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.VrTrackerBattery => new HookItem(hook,
            title: "🎮  VR Tracker Battery  (SteamVR / OpenVR)",
            description: "Connects to SteamVR via the OpenVR SDK to read battery levels for your HMD, " +
                         "controllers, and trackers. Requires SteamVR to be running.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.NetworkStats => new HookItem(hook,
            title: "📶  Network Statistics  (interface byte counters)",
            description: "Reads network interface byte counters from Windows to calculate upload/download throughput. " +
                         "No packet content is inspected.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.SoundpadBridge => new HookItem(hook,
            title: "🔊  Soundpad Bridge  (named-pipe IPC)",
            description: "Connects to the Soundpad desktop application via a named pipe to control sound playback. " +
                         "Requires Soundpad to be installed and running.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        PrivacyHook.VrcLogReader => new HookItem(hook,
            title: "📡  VRChat Log Reader  (file read)",
            description: "Reads VRChat's output_log.txt to extract world info, player events, and session stats. " +
                         "All data stays local — nothing is sent externally.",
            warning: null,
            isApproved: DefaultApproved(hook)),

        _ => new HookItem(hook, hook.ToString(), string.Empty, null, DefaultApproved(hook)),
    };

    /// <summary>
    /// Returns the effective approved state for a hook:
    /// - If the user has already made a decision, honour it.
    /// - On first run (Unknown), default WindowActivity, MediaSession, AfkSensor,
    ///   and InternetAccess to ON; everything else OFF.
    /// </summary>
    private bool DefaultApproved(PrivacyHook hook) =>
        _consentService.GetState(hook) switch
        {
            ConsentState.Approved => true,
            ConsentState.Denied => false,
            _ => hook is PrivacyHook.WindowActivity
                                      or PrivacyHook.MediaSession
                                      or PrivacyHook.AfkSensor
                                      or PrivacyHook.InternetAccess,
        };

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
        {
            if (item.IsApproved)
                _consentService.Approve(item.Hook);
            else
                _consentService.Deny(item.Hook);
        }

        var settings = _appSettingsProvider.Value;
        settings.AcceptedTosVersion = Constants.TosVersion;
        _appSettingsProvider.Save();

        DialogResult = true;
        Close();
    }

    private void TosLink_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(Constants.GitHubSecurityUrl) { UseShellExecute = true });

    private void SlaLink_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(Constants.GitHubLicenseUrl) { UseShellExecute = true });

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.GetPosition(this).Y < 35)
            DragMove();
    }
}
