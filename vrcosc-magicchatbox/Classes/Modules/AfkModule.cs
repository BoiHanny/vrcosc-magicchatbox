using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using Newtonsoft.Json;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class AfkModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "AfkModuleSettings.json";

        [ObservableProperty]
        private int afkTimeout = 5;

        [ObservableProperty]
        private bool enableAfkDetection = true;

        [ObservableProperty]
        private bool detectKeyboardActivity = false;

        [ObservableProperty]
        private bool detectMouseActivity = true;

        [ObservableProperty]
        private string targetApplication = "VRChat.exe";

        [ObservableProperty]
        private bool overrideAfk = false;


        public static AfkModuleSettings LoadSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);

            if (File.Exists(path))
            {
                var settingsJson = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The settings JSON file is empty or corrupted.");
                    return new AfkModuleSettings();
                }

                try
                {
                    var settings = JsonConvert.DeserializeObject<AfkModuleSettings>(settingsJson);

                    if (settings != null)
                    {
                        return settings;
                    }
                    else
                    {
                        Logging.WriteInfo("Failed to deserialize the settings JSON.");
                        return new AfkModuleSettings();
                    }
                }
                catch (JsonException ex)
                {
                    Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
                    return new AfkModuleSettings();
                }
            }
            else
            {
                Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
                return new AfkModuleSettings();
            }
        }

        public void SaveSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); // Ensure directory exists
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, settingsJson);
        }

    }

    public partial class AfkModule : ObservableObject
    {
        private DispatcherTimer afkTimer;

        public event EventHandler<EventArgs> AfkDetected;

        [ObservableProperty]
        private AfkModuleSettings settings;

        [ObservableProperty]
        private bool isAfk;

        public AfkModule()
        {
            Settings = AfkModuleSettings.LoadSettings();
            InitializeAfkDetection();
        }

        private void InitializeAfkDetection()
        {
            if (Settings.EnableAfkDetection)
            {
                afkTimer = new DispatcherTimer();
                afkTimer.Interval = TimeSpan.FromSeconds(2);
                afkTimer.Tick += AfkTimer_Tick;
                afkTimer.Start();
            }
        }

        private void AfkTimer_Tick(object sender, EventArgs e)
        {
            if (Settings.EnableAfkDetection)
            {
                uint idleTime = GetIdleTime();
                if (idleTime >= Settings.AfkTimeout)
                {
                    if (!Settings.OverrideAfk)
                    {
                        IsAfk = true;
                        OnAfkDetected();
                    }
                }
                else
                {
                    IsAfk = false;
                }
            }
        }

        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            GetLastInputInfo(ref lastInputInfo);

            return ((uint)Environment.TickCount - lastInputInfo.dwTime) / 1000;
        }

        private bool IsTargetApplicationFocused()
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow != IntPtr.Zero)
            {
                uint processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                var process = Process.GetProcessById((int)processId);
                return process.ProcessName.Equals(settings.TargetApplication, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void OnAfkDetected()
        {
            AfkDetected?.Invoke(this, EventArgs.Empty);
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
