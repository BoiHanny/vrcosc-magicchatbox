using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.ViewModels;
using WindowsMediaController;

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
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);

        [ObservableProperty]
        private int afkTimeout = 120;

        [ObservableProperty]
        private bool enableAfkDetection = true;

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

        public string FriendlyTimeoutTime => FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout));

        [ObservableProperty]
        private string remainingTimeUntilAFK;

        [ObservableProperty]
        private string timeCurrentlyAFK;

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
            uint idleTime = GetIdleTime();

            // Adjust visibility based on override status
            OverrideButtonVisible = Settings.OverrideAfk || !IsAfk;

            if (!Settings.ActivateInVR && ViewModel.Instance.IsVRRunning)
            {
                // If ActivateInVR is turned off and IsVRRunning is true, stop the module and pause
                if (afkTimer.IsEnabled)
                {
                    afkTimer.Stop();
                    ExitAfkMode();
                }
                return;
            }

            if (Settings.OverrideAfk && !overrideAfkStarted)
            {
                EnterAfkMode(true); // Enter AFK due to override
                overrideAfkStarted = true;
            }
            else if (!Settings.OverrideAfk && overrideAfkStarted)
            {
                ExitAfkMode();
                overrideAfkStarted = false;
            }
            else if (idleTime >= Settings.AfkTimeout && !IsAfk)
            {
                EnterAfkMode(false); // Enter AFK due to inactivity
            }
            else if (idleTime < Settings.AfkTimeout && IsAfk && !Settings.OverrideAfk)
            {
                ExitAfkMode(); // Exit AFK as user is active again and not overriding
            }
            else if (IsAfk)
            {
                // Continuously update AFK duration if AFK
                TimeCurrentlyAFK = FormatDuration(DateTime.Now - lastActionTime);
            }
            else
            {
                // Update the remaining time until AFK
                RemainingTimeUntilAFK = FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout - idleTime));
            }
        }

        private void EnterAfkMode(bool isOverride)
        {
            IsAfk = true;
            if (isOverride)
            {
                lastActionTime = DateTime.Now;
            }
            else
            {
                lastActionTime = DateTime.Now - TimeSpan.FromSeconds(Settings.AfkTimeout);
                // Hide the override button only when entering AFK due to inactivity
                OverrideButtonVisible = false;
            }

            TimeCurrentlyAFK = FormatDuration(DateTime.Now - lastActionTime);
            AfkDetected?.Invoke(this, EventArgs.Empty);
        }

        private void ExitAfkMode()
        {
            IsAfk = false;
            lastActionTime = DateTime.Now;
            TimeCurrentlyAFK = string.Empty;
            RemainingTimeUntilAFK = FormatDuration(TimeSpan.FromSeconds(Settings.AfkTimeout));
            OverrideButtonVisible = true;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            var parts = new[]
            {
                duration.TotalHours >= 1 ? $"{duration.Hours}h" : null,
                duration.Minutes > 0 ? $"{duration.Minutes}m" : null,
                $"{duration.Seconds}s"
            };

            return string.Join(" ", parts.Where(part => part != null));
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
