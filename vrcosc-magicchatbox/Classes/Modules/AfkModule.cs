using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class AfkModuleSettings : ObservableObject
    {
        public event EventHandler SettingsChanged;

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
        private static readonly string SettingsPath = Path.Combine(ViewModel.Instance.DataPath, SettingsFileName);

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

        public static AfkModuleSettings LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var settingsJson = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<AfkModuleSettings>(settingsJson);
                    return settings ?? new AfkModuleSettings();
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing settings JSON: {ex.Message}");
                    return new AfkModuleSettings();
                }
            }
            else
            {
                return new AfkModuleSettings();
            }
        }

        public void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, settingsJson);
        }
    }

    public partial class AfkModule : ObservableObject
    {
        private readonly DispatcherTimer afkTimer = new DispatcherTimer();
        private DateTime lastActionTime;
        private bool overrideAfkStarted = false;

        public string FriendlyTimeoutTime => FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout), Settings.UseSmallLettersForDuration);

        [ObservableProperty]
        private string remainingTimeUntilAFK;

        [ObservableProperty]
        private string timeCurrentlyAFK;

        [ObservableProperty]
        private bool vRModeLabelActive;

        [ObservableProperty]
        private bool overrideButtonVisible;

        public event EventHandler<EventArgs> AfkDetected;

        [ObservableProperty]
        private AfkModuleSettings settings;

        [ObservableProperty]
        private bool isAfk;

        public AfkModule()
        {
            Settings = AfkModuleSettings.LoadSettings();
            Settings.SettingsChanged += Settings_SettingsChanged;
            ViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
            lastActionTime = DateTime.Now;
            InitializeAfkDetection();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsVRRunning")
            {
                InitializeAfkDetection();
            }
        }

        private void Settings_SettingsChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(FriendlyTimeoutTime));
            HandleAfkDetectionToggle();
        }

        private void HandleAfkDetectionToggle()
        {
            if (Settings.EnableAfkDetection)
            {
                if (!afkTimer.IsEnabled)
                {
                    afkTimer.Interval = TimeSpan.FromSeconds(1);
                    afkTimer.Tick += AfkTimer_Tick;
                    afkTimer.Start();
                }
            }
            else if (afkTimer.IsEnabled)
            {
                afkTimer.Stop();
                ExitAfkMode();
            }
        }

        public string GenerateAFKString()
        {
            string afkString = "";

            if (Settings.ShowPrefixIcon)
            {
                afkString += Settings.AfkPrefix + " ";
            }

            if (Settings.ShowAFKTime && TimeCurrentlyAFK != null)
            {
                afkString += Settings.AfkMessageForTimeStamp + TimeCurrentlyAFK;
            }
            else
            {
                afkString += Settings.AfkMessageWithoutTimeStamp;
            }

            return afkString;
        }

        private void InitializeAfkDetection()
        {
            if (Settings.EnableAfkDetection)
            {
                afkTimer.Interval = TimeSpan.FromSeconds(1);
                afkTimer.Tick += AfkTimer_Tick;
                afkTimer.Start();
            }
        }

        public void OnApplicationClosing()
        {
            Settings?.SaveSettings();
        }

        private void AfkTimer_Tick(object sender, EventArgs e)
        {
            OverrideButtonVisible = Settings.OverrideAfk || !IsAfk;

            if (ViewModel.Instance.IsVRRunning && !Settings.ActivateInVR)
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

            // Handle override logic
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
                TimeCurrentlyAFK = FormatDuration(DateTime.Now - lastActionTime, Settings.UseSmallLettersForDuration);
            }
            else
            {
                RemainingTimeUntilAFK = FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout - idleTime), Settings.UseSmallLettersForDuration);
            }
        }

        private void EnterAfkMode(bool isOverride, bool resetTime)
        {
            IsAfk = true;

            if (resetTime || isOverride)
            {
                lastActionTime = DateTime.Now;
            }

            TimeCurrentlyAFK = FormatDuration(DateTime.Now - lastActionTime, Settings.UseSmallLettersForDuration);
            AfkDetected?.Invoke(this, EventArgs.Empty);
        }

        private void ExitAfkMode()
        {
            IsAfk = false;
            lastActionTime = DateTime.Now;
            TimeCurrentlyAFK = string.Empty;
            overrideAfkStarted = false;
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

        private static uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            GetLastInputInfo(ref lastInputInfo);
            return ((uint)Environment.TickCount - lastInputInfo.dwTime) / 1000;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
    }
}
