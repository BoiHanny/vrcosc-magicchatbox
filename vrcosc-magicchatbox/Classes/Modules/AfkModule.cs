using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using vrcosc_magicchatbox.Classes.Modules.Afk;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class AfkModuleSettings : ObservableObject
{
    public event EventHandler? SettingsChanged;

    public AfkModuleSettings()
    {
        AllStyles.CollectionChanged += (_, _) => RefreshStyleSnapshot();
    }

    private void RefreshStyleSnapshot()
    {
        var snapshot = new AfkStyle[AllStyles.Count];
        AllStyles.CopyTo(snapshot, 0);
        Volatile.Write(ref _allStylesSnapshot, snapshot);
    }

    protected virtual void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEnableAfkDetectionChanged(bool value)
    {
        OnSettingsChanged();
    }

    partial void OnAfkTimeoutChanged(int value)
    {
        OnSettingsChanged();
    }

    private const string SettingsFileName = "AfkModuleSettings.json";
    internal string _settingsPath = string.Empty;

    [ObservableProperty]
    private int afkTimeout = 120;

    [ObservableProperty]
    private bool enableAfkDetection = true;

    [ObservableProperty]
    private bool useSmallLettersForDuration = true;

    [ObservableProperty]
    private bool showPrefixIcon = true;

    [ObservableProperty]
    private string afkPrefix = "💤";

    [ObservableProperty]
    private bool activateInVR = false;

    [ObservableProperty]
    private bool showAFKTime = true;

    [ObservableProperty]
    private string afkMessageForTimeStamp = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK ᶠᵒʳ ";

    [ObservableProperty]
    private string afkMessageWithoutTimeStamp = "ᶜᵘʳʳᵉⁿᵗˡʸ AFK";

    [ObservableProperty]
    private bool overrideAfk = false;

    [ObservableProperty]
    private ObservableCollection<AfkStyle> customStyles = new();

    [ObservableProperty]
    [property: JsonProperty("Styles", NullValueHandling = NullValueHandling.Ignore)]
    private ObservableCollection<AfkStyle>? legacyStyles;

    [ObservableProperty]
    private string activeStyleId = string.Empty;

    [JsonIgnore]
    public ObservableCollection<AfkStyle> AllStyles { get; } = new();

    private IReadOnlyList<AfkStyle> _allStylesSnapshot = Array.Empty<AfkStyle>();

    [JsonIgnore]
    public IReadOnlyList<AfkStyle> AllStylesSnapshot => Volatile.Read(ref _allStylesSnapshot);

    [JsonIgnore]
    public AfkStyle? ActiveStyle => AfkStyleSeed.Resolve(AllStylesSnapshot, ActiveStyleId);

    public bool EnsureStyles()
    {
        bool hadPersistedPresets = LegacyStyles != null;

        var seeded = AfkStyleSeed.Compose(
            CustomStyles,
            LegacyStyles,
            ActiveStyleId,
            AfkPrefix,
            ShowPrefixIcon,
            AfkMessageForTimeStamp,
            AfkMessageWithoutTimeStamp,
            ShowAFKTime);

        if (!ReferenceEquals(CustomStyles, seeded.CustomStyles))
            CustomStyles = new ObservableCollection<AfkStyle>(seeded.CustomStyles);

        LegacyStyles = null;

        AllStyles.Clear();
        foreach (var style in seeded.AllStyles)
            AllStyles.Add(style);

        ActiveStyleId = seeded.ActiveId;
        return hadPersistedPresets;
    }

    public static AfkModuleSettings LoadSettings(string settingsPath)
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                var settingsJson = File.ReadAllText(settingsPath);
                var settings = JsonConvert.DeserializeObject<AfkModuleSettings>(settingsJson);
                if (settings != null) settings._settingsPath = settingsPath;
                return settings ?? new AfkModuleSettings { _settingsPath = settingsPath };
            }
            catch (JsonException ex)
            {
                Logging.WriteInfo($"Error parsing AFK settings JSON: {ex.Message}");
                return new AfkModuleSettings { _settingsPath = settingsPath };
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error reading AFK settings: {ex.Message}");
                return new AfkModuleSettings { _settingsPath = settingsPath };
            }
        }
        else
        {
            return new AfkModuleSettings { _settingsPath = settingsPath };
        }
    }

    public void SaveSettings()
    {
        var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
        if (!AtomicFileWriter.WriteAllText(_settingsPath, settingsJson))
            Logging.WriteInfo("Failed to save AFK settings.");
    }
}

public partial class AfkModule : ObservableObject, IModule
{
    private readonly IAppState _appState;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeSettings _ts;
    private TimeSettings TS => _ts;
    private readonly IPrivacyConsentService _consentService;

    private Timer? _afkTimer;
    private DateTime lastActionTime;
    private bool overrideAfkStarted = false;
    private bool _disposed;
    private bool _timerRunning;
    private int _lastAfkSecondsFormatted = int.MinValue;
    private int _lastRemainingSecondsFormatted = int.MinValue;

    public string FriendlyTimeoutTime => FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout), Settings.UseSmallLettersForDuration);

    [ObservableProperty]
    private string? remainingTimeUntilAFK;

    [ObservableProperty]
    private string? timeCurrentlyAFK;

    [ObservableProperty]
    private bool vRModeLabelActive;

    [ObservableProperty]
    private bool overrideButtonVisible;

    public event EventHandler<EventArgs>? AfkDetected;

    [ObservableProperty]
    private AfkModuleSettings settings;

    [ObservableProperty]
    private bool isAfk;

    public string Name => "AFK";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _timerRunning;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) { InitializeAfkDetection(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken ct = default) { StopTimer(); return Task.CompletedTask; }
    public void SaveSettings() => Settings?.SaveSettings();

    public AfkModule(IAppState appState, IUiDispatcher dispatcher, TimeSettings timeSettings, IEnvironmentService env, IPrivacyConsentService consentService)
    {
        _appState = appState;
        _dispatcher = dispatcher;
        _ts = timeSettings;
        _consentService = consentService;
        var settingsPath = Path.Combine(env.DataPath, "AfkModuleSettings.json");
        Settings = AfkModuleSettings.LoadSettings(settingsPath);

        if (Settings.EnsureStyles())
            Settings.SaveSettings();

        Settings.SettingsChanged += Settings_SettingsChanged;
        _appState.PropertyChanged += ViewModel_PropertyChanged;
        lastActionTime = DateTime.Now;
        InitializeAfkDetection();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsVRRunning")
        {
            InitializeAfkDetection();
        }
    }

    private void Settings_SettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FriendlyTimeoutTime));
        HandleAfkDetectionToggle();
    }

    private void HandleAfkDetectionToggle()
    {
        if (Settings.EnableAfkDetection)
        {
            if (!_timerRunning)
                StartTimer();
        }
        else if (_timerRunning)
        {
            StopTimer();
            ExitAfkMode();
        }
    }

    public string GenerateAFKString()
    {
        var style = Settings.ActiveStyle;
        if (style == null)
            return string.Empty;

        return style.Render(TimeCurrentlyAFK);
    }

    private void InitializeAfkDetection()
    {
        if (Settings.EnableAfkDetection)
            StartTimer();
    }

    private void StartTimer()
    {
        _afkTimer?.Dispose();
        _afkTimer = new Timer(_ => _dispatcher.BeginInvoke(AfkTimer_Tick), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        _timerRunning = true;
    }

    private void StopTimer()
    {
        _afkTimer?.Dispose();
        _afkTimer = null;
        _timerRunning = false;
    }

    public void OnApplicationClosing()
    {
        Settings?.SaveSettings();
    }

    private void AfkTimer_Tick()
    {
        if (_disposed)
            return;

        OverrideButtonVisible = Settings.OverrideAfk || !IsAfk;

        if (_appState.IsVRRunning && !Settings.ActivateInVR)
        {
            VRModeLabelActive = true;

            if (!Settings.OverrideAfk)
            {
                if (IsAfk)
                {
                    ExitAfkMode();
                }

                return;
            }
        }
        else
        {
            VRModeLabelActive = false;
        }

        if (Settings.OverrideAfk)
        {
            VRModeLabelActive = false;
            if (!overrideAfkStarted)
            {
                EnterAfkMode(true, true);
                overrideAfkStarted = true;
            }
        }
        else if (overrideAfkStarted)
        {
            ExitAfkMode();
            overrideAfkStarted = false;
        }

        uint idleTime = 0;

        if (!Settings.OverrideAfk)
        {
            idleTime = GetIdleTime();

            if (idleTime >= Settings.AfkTimeout && !IsAfk)
            {
                lastActionTime = DateTime.Now - TimeSpan.FromSeconds(idleTime);
                EnterAfkMode(false, false);
            }
            else if (idleTime < Settings.AfkTimeout && IsAfk)
            {
                ExitAfkMode();
            }
        }

        if (IsAfk)
        {
            var afkSeconds = (int)(DateTime.Now - lastActionTime).TotalSeconds;
            if (afkSeconds != _lastAfkSecondsFormatted)
            {
                _lastAfkSecondsFormatted = afkSeconds;
                TimeCurrentlyAFK = FormatDuration(TimeSpan.FromSeconds(afkSeconds), Settings.UseSmallLettersForDuration);
            }
        }
        else
        {
            var remainingSeconds = Settings.AfkTimeout - (int)idleTime;
            if (remainingSeconds != _lastRemainingSecondsFormatted)
            {
                _lastRemainingSecondsFormatted = remainingSeconds;
                RemainingTimeUntilAFK = FormatDuration(TimeSpan.FromSeconds(remainingSeconds), Settings.UseSmallLettersForDuration);
            }
        }
    }

    private void EnterAfkMode(bool isOverride, bool resetTime)
    {
        IsAfk = true;

        if (resetTime || isOverride)
        {
            lastActionTime = DateTime.Now;
        }

        var afkSeconds = (int)(DateTime.Now - lastActionTime).TotalSeconds;
        _lastAfkSecondsFormatted = afkSeconds;
        _lastRemainingSecondsFormatted = int.MinValue;
        TimeCurrentlyAFK = FormatDuration(TimeSpan.FromSeconds(afkSeconds), Settings.UseSmallLettersForDuration);
        AfkDetected?.Invoke(this, EventArgs.Empty);
    }

    private void ExitAfkMode()
    {
        IsAfk = false;
        lastActionTime = DateTime.Now;
        TimeCurrentlyAFK = string.Empty;
        overrideAfkStarted = false;
        _lastAfkSecondsFormatted = int.MinValue;
        _lastRemainingSecondsFormatted = Settings.AfkTimeout;
        RemainingTimeUntilAFK = FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout), Settings.UseSmallLettersForDuration);
        OverrideButtonVisible = true;
    }

    public static string FormatDuration(TimeSpan duration, bool useSmallLetters)
    {
        var parts = new List<string>();

        int years = duration.Days / 365;
        int months = (duration.Days % 365) / 30;
        int days = (duration.Days % 365) % 30;

        string yearUnit = useSmallLetters ? "ʸ" : "y";
        string monthUnit = useSmallLetters ? "ᵐᵒ" : "mo";
        string dayUnit = useSmallLetters ? "ᵈ" : "d";
        string hourUnit = useSmallLetters ? "ʰ" : "h";
        string minuteUnit = useSmallLetters ? "ᵐ" : "m";
        string secondUnit = useSmallLetters ? "ˢ" : "s";

        if (years > 0)
        {
            parts.Add($"{years}{yearUnit}");
        }

        if (months > 0)
        {
            parts.Add($"{months}{monthUnit}");
        }

        if (days > 0)
        {
            parts.Add($"{days}{dayUnit}");
        }

        if (duration.Hours > 0 || years > 0 || months > 0 || days > 0)
        {
            parts.Add($"{duration.Hours}{hourUnit}");
        }

        if (duration.Minutes > 0 || duration.Hours > 0 || years > 0 || months > 0 || days > 0)
        {
            parts.Add($"{duration.Minutes}{minuteUnit}");
        }

        parts.Add($"{duration.Seconds}{secondUnit}");

        return string.Join(" ", parts);
    }

    private uint GetIdleTime()
    {
        if (_appState.BussyBoysMode && TS.BussyBoysDateEnable)
        {
            TimeSpan elapsedSinceBussyBoysDate = DateTime.Now - TS.BussyBoysDate;
            return (uint)elapsedSinceBussyBoysDate.TotalSeconds;
        }

        if (_consentService == null || !_consentService.IsApproved(PrivacyHook.AfkSensor))
            return 0;

        LASTINPUTINFO lastInputInfo = new LASTINPUTINFO { cbSize = (uint)LastInputInfoSize };
        GetLastInputInfo(ref lastInputInfo);
        return ((uint)Environment.TickCount - lastInputInfo.dwTime) / 1000;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private static readonly int LastInputInfoSize = Marshal.SizeOf(typeof(LASTINPUTINFO));

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopTimer();
        Settings.SettingsChanged -= Settings_SettingsChanged;
        _appState.PropertyChanged -= ViewModel_PropertyChanged;
    }
}
