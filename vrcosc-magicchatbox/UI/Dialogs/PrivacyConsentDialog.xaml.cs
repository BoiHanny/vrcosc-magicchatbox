using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using vrcosc_magicchatbox.Core.Privacy;

namespace vrcosc_magicchatbox.UI.Dialogs;

public partial class PrivacyConsentDialog : Window
{
    private readonly IPrivacyConsentService _consentService;
    private readonly ObservableCollection<HookItem> _items = new();

    public PrivacyConsentDialog(IPrivacyConsentService consentService, IEnumerable<PrivacyHook> hooks)
    {
        _consentService = consentService;
        InitializeComponent();

        foreach (var hook in hooks)
            _items.Add(CreateItem(hook));

        HookList.ItemsSource = _items;
    }

    private HookItem CreateItem(PrivacyHook hook) => hook switch
    {
        PrivacyHook.HardwareMonitor => new HookItem(hook,
            title: "🖥️  Hardware Monitor  (CPU · GPU · VRAM · temps · wattage)",
            description: "Reads CPU/GPU load, temperature, and power draw using LibreHardwareMonitor. " +
                         "This requires loading a low-level kernel driver (WinRing0.sys) on first use.",
            warning: "⚠  Windows Defender may flag WinRing0.sys. This is a known false-positive caused by " +
                     "missing commercial code-signing — not malware. The driver is used by many legitimate tools. " +
                     "Denying this still shows RAM usage (no driver needed).",
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.WindowActivity => new HookItem(hook,
            title: "📋  Window Activity  (active app · window title)",
            description: "Reads the name and title of your currently focused window using the Windows " +
                         "accessibility API (UIAutomation + GetForegroundWindow). No screenshots are taken.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.MediaSession => new HookItem(hook,
            title: "🎵  Media Session  (song title · artist · playback state)",
            description: "Reads metadata from music and video players via Windows SMTC (System Media Transport Controls). " +
                         "Only the currently playing track info is read — no file access.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.AfkSensor => new HookItem(hook,
            title: "💤  AFK Sensor  (last input timestamp)",
            description: "Reads the timestamp of your last keyboard or mouse event using GetLastInputInfo to detect " +
                         "inactivity. No keystrokes or mouse positions are recorded.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.InternetAccess => new HookItem(hook,
            title: "🌐  Internet Access  (Twitch · Pulsoid · Weather · OpenAI · TTS · Speech)",
            description: "Allows outbound HTTP requests to third-party services: Twitch API for chat/stats, " +
                         "Pulsoid for heart-rate data, a weather provider for current conditions, " +
                         "OpenAI for IntelliChat, TikTok TTS for text-to-speech, and speech detection. " +
                         "Only data you configure is requested — no telemetry.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.VrTrackerBattery => new HookItem(hook,
            title: "🎮  VR Tracker Battery  (SteamVR / OpenVR)",
            description: "Connects to SteamVR via the OpenVR SDK to read battery levels for your HMD, " +
                         "controllers, and trackers. Requires SteamVR to be running.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.NetworkStats => new HookItem(hook,
            title: "📶  Network Statistics  (interface byte counters)",
            description: "Reads network interface byte counters from Windows (System.Net.NetworkInformation) to " +
                         "calculate upload and download throughput. No packet content is inspected.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        PrivacyHook.SoundpadBridge => new HookItem(hook,
            title: "🔊  Soundpad Bridge  (named-pipe IPC)",
            description: "Connects to the Soundpad desktop application via a named pipe to control sound playback. " +
                         "Requires Soundpad to be installed and running.",
            warning: null,
            isApproved: _consentService.IsApproved(hook)),

        _ => new HookItem(hook, hook.ToString(), string.Empty, null, _consentService.IsApproved(hook)),
    };

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
        {
            if (item.IsApproved)
                _consentService.Approve(item.Hook);
            else
                _consentService.Deny(item.Hook);
        }
        Close();
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.GetPosition(this).Y < 35)
            DragMove();
    }
}

public partial class HookItem : ObservableObject
{
    public PrivacyHook Hook { get; }
    public string Title { get; }
    public string Description { get; }
    public string Warning { get; }
    public Visibility WarningVisibility => string.IsNullOrEmpty(Warning) ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty] private bool _isApproved;

    public HookItem(PrivacyHook hook, string title, string description, string warning, bool isApproved)
    {
        Hook = hook;
        Title = title;
        Description = description;
        Warning = warning;
        _isApproved = isApproved;
    }

    [RelayCommand]
    private void Toggle() => IsApproved = !IsApproved;
}
