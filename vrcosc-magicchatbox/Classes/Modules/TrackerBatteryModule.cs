using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules
{
    /// <summary>
    /// Module that reads SteamVR tracker, controller, and headset battery levels via OpenVR and formats them for display.
    /// </summary>
    public class TrackerBatteryModule : IModule
    {
        private static readonly Dictionary<string, string> DefaultIconsByKind = new(StringComparer.OrdinalIgnoreCase)
        {
            { "HMD", "🥽" },
            { "Controller", "🎮" },
            { "Tracker", "📍" },
            { "BaseStation", "📡" }
        };

        private static readonly Dictionary<string, string> LegacyIconsByKind = new(StringComparer.OrdinalIgnoreCase)
        {
            { "HMD", "H" },
            { "Controller", "C" },
            { "Tracker", "T" },
            { "BaseStation", "B" }
        };
        private CVRSystem _vrSystem;
        private bool _isInitialized;
        private int _rotationIndex;
        private DateTime _lastRotationUtc = DateTime.MinValue;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        private readonly ISettingsProvider<TrackerBatterySettings> _settingsProvider;
        public TrackerBatterySettings Settings => _settingsProvider.Value;
        public void SaveSettings() => _settingsProvider.Save();

        public string Name => "TrackerBattery";
        public bool IsEnabled { get; set; } = true;
        public bool IsRunning => _isInitialized;
        public Task InitializeAsync(CancellationToken ct = default) { Initialize(); return Task.CompletedTask; }
        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) { ShutdownOpenVR("StopAsync"); return Task.CompletedTask; }
        public void Dispose() => ShutdownOpenVR("Dispose");

        private readonly IAppState _appState;
        private readonly TrackerDisplayState _tracker;
        private readonly IntegrationDisplayState _integrationDisplay;
        private readonly IUiDispatcher _dispatcher;
        private readonly IToastService? _toast;
        private volatile bool _trackerErrorShown;

        public TrackerBatteryModule(
            ISettingsProvider<TrackerBatterySettings> settingsProvider,
            IAppState appState,
            TrackerDisplayState tracker,
            IntegrationDisplayState integrationDisplay,
            IUiDispatcher dispatcher,
            IToastService? toast = null)
        {
            _settingsProvider = settingsProvider;
            _appState = appState;
            _tracker = tracker;
            _integrationDisplay = integrationDisplay;
            _dispatcher = dispatcher;
            _toast = toast;
        }

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                EVRInitError error = EVRInitError.None;
                _vrSystem = Valve.VR.OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

                if (error != EVRInitError.None)
                {
                    _vrSystem = null;
                    if (error != EVRInitError.Init_NoServerForBackgroundApp)
                    {
                        Logging.WriteInfo($"OpenVR Init Failed: {error}");
                        if (!_trackerErrorShown)
                        {
                            _trackerErrorShown = true;
                            _toast?.Show("📍 VR Tracker", $"OpenVR failed to initialize: {error}", ToastType.Warning, key: "tracker-error");
                        }
                    }
                    return;
                }

                _isInitialized = true;
                _trackerErrorShown = false;
            }
            catch (Exception ex)
            {
                _vrSystem = null;
                Logging.WriteException(ex, MSGBox: false);
                _toast?.Show("📍 VR Tracker", $"OpenVR initialization error: {ex.Message}", ToastType.Warning, key: "tracker-error");
            }
        }

        public void UpdateDevices()
        {
            if (!_appState.IsVRRunning)
            {
                ShutdownOpenVR("VR not running");
                return;
            }

            if (!_isInitialized)
            {
                Initialize();
                if (!_isInitialized)
                {
                    UpdateSummary("Waiting for SteamVR...");
                    return;
                }
            }

            if (_vrSystem == null)
            {
                return;
            }

            var currentSerialNumbers = new HashSet<string>();

            for (uint i = 0; i < Valve.VR.OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (!_vrSystem.IsTrackedDeviceConnected(i))
                {
                    continue;
                }

                var deviceClass = _vrSystem.GetTrackedDeviceClass(i);
                if (deviceClass != ETrackedDeviceClass.Controller &&
                    deviceClass != ETrackedDeviceClass.GenericTracker &&
                    deviceClass != ETrackedDeviceClass.HMD)
                {
                    continue;
                }

                string serial = GetStringProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String);
                if (string.IsNullOrWhiteSpace(serial))
                {
                    continue;
                }

                currentSerialNumbers.Add(serial);

                TrackerDevice device = _tracker.TrackerDevices
                    .FirstOrDefault(d => string.Equals(d.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));

                if (device == null)
                {
                    string model = GetStringProperty(i, ETrackedDeviceProperty.Prop_ModelNumber_String);
                    string smartModel = SmartModelName(model, deviceClass);
                    device = new TrackerDevice
                    {
                        SerialNumber = serial,
                        OriginalModelName = smartModel,
                        DeviceKind = ResolveDeviceKind(deviceClass),
                        CustomName = SuggestName(i, deviceClass, smartModel),
                        CustomIcon = SuggestIcon(deviceClass),
                        UseCustomLowThreshold = false
                    };

                    _dispatcher.Invoke(() =>
                        _tracker.TrackerDevices.Add(device));
                }
                else
                {
                    device.DeviceKind = ResolveDeviceKind(deviceClass);
                    NormalizeLegacyIcon(device, device.DeviceKind);
                    if (string.IsNullOrWhiteSpace(device.CustomName))
                    {
                        string smartModel = SmartModelName(device.OriginalModelName, deviceClass);
                        if (!string.Equals(device.OriginalModelName, smartModel, StringComparison.Ordinal))
                        {
                            device.OriginalModelName = smartModel;
                        }
                    }
                }

                device.DeviceIndex = (int)i;
                device.IsConnected = true;

                ETrackedPropertyError propError = ETrackedPropertyError.TrackedProp_Success;
                bool providesBattery = _vrSystem.GetBoolTrackedDeviceProperty(
                    i,
                    ETrackedDeviceProperty.Prop_DeviceProvidesBatteryStatus_Bool,
                    ref propError);

                if (providesBattery)
                {
                    float battery = _vrSystem.GetFloatTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float,
                        ref propError);
                    bool isCharging = _vrSystem.GetBoolTrackedDeviceProperty(
                        i,
                        ETrackedDeviceProperty.Prop_DeviceIsCharging_Bool,
                        ref propError);

                    if (propError == ETrackedPropertyError.TrackedProp_Success)
                    {
                        device.BatteryLevel = battery;
                        device.IsCharging = isCharging;
                    }
                }
                else
                {
                    device.BatteryLevel = 1.0f;
                    device.IsCharging = false;
                }
            }

            foreach (var device in _tracker.TrackerDevices)
            {
                if (!currentSerialNumbers.Contains(device.SerialNumber))
                {
                    device.IsConnected = false;
                    device.DeviceIndex = -1;
                    device.IsCharging = false;
                }
            }

            UpdateSummary("Scanning...");
        }

        private void ShutdownOpenVR(string reason)
        {
            MarkAllDisconnected();
            UpdateSummary(reason);

            if (_isInitialized)
            {
                try
                {
                    Valve.VR.OpenVR.Shutdown();
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"OpenVR shutdown error (non-fatal): {ex.Message}");
                }

                _vrSystem = null;
                _isInitialized = false;
            }
        }

        private void MarkAllDisconnected()
        {
            foreach (var device in _tracker.TrackerDevices)
            {
                device.IsConnected = false;
                device.DeviceIndex = -1;
                device.IsCharging = false;
            }
        }

        public string BuildChatboxString()
        {
            UpdateDevices();

            bool globalEmergency = Settings.GlobalEmergency;

            string template = string.IsNullOrWhiteSpace(Settings.Template)
                ? "{icon} {name} {batt}%"
                : Settings.Template;

            string separator = string.IsNullOrWhiteSpace(Settings.Separator)
                ? " | "
                : Settings.Separator;

            IEnumerable<TrackerDevice> activeDevices = _tracker.TrackerDevices
                .Where(ShouldIncludeDevice);

            IEnumerable<TrackerDevice> orderedDevices = ApplySort(activeDevices);

            var displayDevices = new List<TrackerDevice>();
            foreach (var device in orderedDevices)
            {
                int lowThreshold = GetLowThreshold(device);
                bool isLow = device.IsConnected && device.BatteryPercentage <= lowThreshold && !device.IsCharging;

                if (globalEmergency && !isLow)
                {
                    continue;
                }

                if (device.ShowOnlyOnLowBattery && !isLow)
                {
                    continue;
                }

                displayDevices.Add(device);
            }

            displayDevices = ApplyEntryLimit(displayDevices);

            UpdateActiveDevices(displayDevices);

            var entries = new List<string>();
            foreach (var device in displayDevices)
            {
                int lowThreshold = GetLowThreshold(device);
                bool isLow = device.IsConnected && device.BatteryPercentage <= lowThreshold;

                string displayName = string.IsNullOrWhiteSpace(device.DisplayName)
                    ? (device.SerialNumber ?? "Device")
                    : device.DisplayName;

                string batteryText;
                if (device.IsCharging)
                {
                    batteryText = "+" + device.BatteryPercentage.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    batteryText = device.IsConnected
                        ? device.BatteryPercentage.ToString(CultureInfo.InvariantCulture)
                        : Settings.OfflineBatteryText;
                }

                string statusText = device.IsConnected
                    ? (device.IsCharging ? "Charging" : Settings.OnlineText)
                    : Settings.OfflineText;

                string lowTag = (isLow && !device.IsCharging)
                    ? Settings.LowTag
                    : string.Empty;

                string entry = template
                    .Replace("{icon}", device.CustomIcon ?? string.Empty)
                    .Replace("{name}", displayName)
                    .Replace("{batt}", batteryText ?? string.Empty)
                    .Replace("{status}", statusText ?? string.Empty)
                    .Replace("{low}", lowTag ?? string.Empty)
                    .Replace("{kind}", device.DeviceKind ?? string.Empty)
                    .Replace("{serial}", device.SerialNumber ?? string.Empty)
                    .Replace("{model}", device.OriginalModelName ?? string.Empty);

                if (Settings.CompactWhitespace)
                {
                    entry = CompactWhitespace(entry);
                }

                entry = TrimEntry(entry, Settings.MaxEntryLength);

                if (!string.IsNullOrWhiteSpace(entry))
                {
                    entries.Add(entry.Trim());
                }
            }

            string message = entries.Count == 0 ? string.Empty : string.Join(separator, entries);

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(Settings.Prefix))
            {
                message = $"{Settings.Prefix} {message}";
            }

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(Settings.Suffix))
            {
                message = $"{message} {Settings.Suffix}";
            }

            if (!string.IsNullOrWhiteSpace(message) && Settings.UseSmallText)
            {
                message = ToSmallTextPreserveSymbols(message);
            }

            UpdatePreview(message);
            return message.Trim();
        }

        private void UpdateSummary(string scanStatus)
        {
            int total = _tracker.TrackerDevices.Count;
            int connected = _tracker.TrackerDevices.Count(d => d.IsConnected);

            _dispatcher.InvokeAsync(() =>
            {
                _integrationDisplay.TrackerBatteryDeviceSummary = $"{connected}/{total} connected";
                _integrationDisplay.TrackerBatteryLastScanDisplay = scanStatus == "VR not running"
                    ? "Last scan: VR not running"
                    : $"Last scan: {DateTime.Now:T}";
            });
        }

        private string GetStringProperty(uint deviceIndex, ETrackedDeviceProperty prop)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            _stringBuilder.Clear();
            _vrSystem.GetStringTrackedDeviceProperty(
                deviceIndex,
                prop,
                _stringBuilder,
                (uint)_stringBuilder.Capacity,
                ref error);

            if (error == ETrackedPropertyError.TrackedProp_Success)
            {
                return _stringBuilder.ToString();
            }

            return string.Empty;
        }

        private string SmartModelName(string rawModel, ETrackedDeviceClass deviceClass)
        {
            if (string.IsNullOrWhiteSpace(rawModel))
            {
                return deviceClass == ETrackedDeviceClass.HMD ? "Headset" : "Unknown Device";
            }

            if (rawModel.Contains("Tundra", StringComparison.OrdinalIgnoreCase))
            {
                return "Tundra Tracker";
            }

            if (rawModel.Contains("Vive Tracker Pro", StringComparison.OrdinalIgnoreCase))
            {
                return "Vive Tracker 3.0";
            }

            if (rawModel.Contains("Vive Tracker", StringComparison.OrdinalIgnoreCase))
            {
                return "Vive Tracker";
            }

            if (rawModel.Contains("Knuckles", StringComparison.OrdinalIgnoreCase) ||
                rawModel.Contains("Valve Index", StringComparison.OrdinalIgnoreCase))
            {
                return "Index Controller";
            }

            if (rawModel.Contains("Quest", StringComparison.OrdinalIgnoreCase) ||
                rawModel.Contains("Miramar", StringComparison.OrdinalIgnoreCase))
            {
                return "Quest Controller";
            }

            return rawModel;
        }

        private string SuggestName(uint deviceIndex, ETrackedDeviceClass deviceClass, string modelName)
        {
            if (deviceClass == ETrackedDeviceClass.HMD)
            {
                return "Headset";
            }

            var role = _vrSystem.GetControllerRoleForTrackedDeviceIndex(deviceIndex);
            if (role == ETrackedControllerRole.LeftHand)
            {
                return "Left Hand";
            }

            if (role == ETrackedControllerRole.RightHand)
            {
                return "Right Hand";
            }

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                return modelName;
            }

            return deviceClass == ETrackedDeviceClass.GenericTracker ? "Tracker" : "Device";
        }

        private string SuggestIcon(ETrackedDeviceClass deviceClass)
        {
            return GetDefaultIcon(ResolveDeviceKind(deviceClass));
        }

        private string ResolveDeviceKind(ETrackedDeviceClass deviceClass)
        {
            switch (deviceClass)
            {
                case ETrackedDeviceClass.HMD:
                    return "HMD";
                case ETrackedDeviceClass.Controller:
                    return "Controller";
                case ETrackedDeviceClass.GenericTracker:
                    return "Tracker";
                case ETrackedDeviceClass.TrackingReference:
                    return "BaseStation";
                default:
                    return "Unknown";
            }
        }

        public static void NormalizeLegacyIcons(IEnumerable<TrackerDevice> devices)
        {
            if (devices == null)
            {
                return;
            }

            foreach (var device in devices)
            {
                NormalizeLegacyIcon(device, device?.DeviceKind);
            }
        }

        private static void NormalizeLegacyIcon(TrackerDevice device, string deviceKind)
        {
            if (device == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(device.CustomIcon))
            {
                return;
            }

            if (!LegacyIconsByKind.TryGetValue(deviceKind ?? string.Empty, out var legacyIcon))
            {
                return;
            }

            if (!string.Equals(device.CustomIcon, legacyIcon, StringComparison.Ordinal))
            {
                return;
            }

            string defaultIcon = GetDefaultIcon(deviceKind);
            device.CustomIcon = defaultIcon ?? string.Empty;
        }

        private static string GetDefaultIcon(string deviceKind)
        {
            if (DefaultIconsByKind.TryGetValue(deviceKind ?? string.Empty, out var icon))
            {
                return icon;
            }

            return string.Empty;
        }

        private bool ShouldIncludeDevice(TrackerDevice device)
        {
            if (device.IsHidden)
            {
                return false;
            }

            bool showDisconnected = Settings.ShowDisconnected;
            if (!showDisconnected && !device.IsConnected)
            {
                return false;
            }

            if (device.DeviceKind == "Controller" && !Settings.ShowControllers)
            {
                return false;
            }

            if (device.DeviceKind == "HMD" && !Settings.ShowHeadset)
            {
                return false;
            }

            if (device.DeviceKind == "Tracker" && !Settings.ShowTrackers)
            {
                return false;
            }

            return true;
        }

        private int GetLowThreshold(TrackerDevice device)
        {
            return device.UseCustomLowThreshold
                ? device.CustomLowThreshold
                : Settings.LowThreshold;
        }

        private IEnumerable<TrackerDevice> ApplySort(IEnumerable<TrackerDevice> devices)
        {
            switch (Settings.SortMode)
            {
                case TrackerBatterySortMode.Name:
                    return devices.OrderBy(d => d.DisplayName);
                case TrackerBatterySortMode.BatteryLowToHigh:
                    return devices.OrderBy(d => d.IsConnected ? d.BatteryPercentage : 999)
                        .ThenBy(d => d.DisplayName);
                case TrackerBatterySortMode.BatteryHighToLow:
                    return devices.OrderByDescending(d => d.IsConnected ? d.BatteryPercentage : -1)
                        .ThenBy(d => d.DisplayName);
                case TrackerBatterySortMode.TypeThenName:
                    return devices.OrderBy(d => d.DeviceKind).ThenBy(d => d.DisplayName);
                default:
                    return devices;
            }
        }

        private List<TrackerDevice> ApplyEntryLimit(List<TrackerDevice> devices)
        {
            int maxEntries = Settings.MaxEntries;
            if (maxEntries <= 0 || devices.Count <= maxEntries)
            {
                _rotationIndex = 0;
                return devices;
            }

            if (!Settings.RotateOverflow)
            {
                return devices.Take(maxEntries).ToList();
            }

            int intervalSeconds = Math.Max(1, Settings.RotationIntervalSeconds);
            DateTime now = DateTime.UtcNow;

            if ((now - _lastRotationUtc).TotalSeconds >= intervalSeconds)
            {
                _lastRotationUtc = now;
                _rotationIndex += maxEntries;
                if (_rotationIndex >= devices.Count)
                {
                    _rotationIndex = 0;
                }
            }

            if (_rotationIndex >= devices.Count)
            {
                _rotationIndex = 0;
            }

            var result = new List<TrackerDevice>();
            int entriesRemaining = devices.Count - _rotationIndex;
            int entriesToShow = Math.Min(maxEntries, entriesRemaining);
            for (int i = 0; i < entriesToShow; i++)
            {
                int index = _rotationIndex + i;
                result.Add(devices[index]);
            }

            return result;
        }

        private void UpdateActiveDevices(IReadOnlyList<TrackerDevice> devices)
        {
            _dispatcher.InvokeAsync(() =>
            {
                var target = _tracker.TrackerBatteryActiveDevices;
                target.Clear();
                foreach (var device in devices)
                {
                    target.Add(device);
                }
            });
        }

        private void UpdatePreview(string message)
        {
            _dispatcher.InvokeAsync(() =>
            {
                _integrationDisplay.TrackerBatteryPreview = message ?? string.Empty;
            });
        }

        private static string TrimEntry(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
            {
                return value;
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            const string suffix = "...";
            if (maxLength <= suffix.Length)
            {
                return value.Substring(0, maxLength);
            }

            return value.Substring(0, maxLength - suffix.Length) + suffix;
        }

        private static string CompactWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"\s+", " ");
        }

        private static string ToSmallTextPreserveSymbols(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (IsSuperscriptCandidate(character))
                {
                    string transformed = TextUtilities.TransformToSuperscript(character.ToString());
                    builder.Append(string.IsNullOrEmpty(transformed) ? character.ToString() : transformed);
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static bool IsSuperscriptCandidate(char character)
        {
            return char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character == '/'
                || character == ':'
                || character == ','
                || character == '.'
                || character == '%';
        }
    }
}
