using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using Valve.VR;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class TrackerBatteryModule
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
                    }
                    return;
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _vrSystem = null;
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void UpdateDevices()
        {
            if (!ViewModel.Instance.IsVRRunning)
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

                TrackerDevice device = ViewModel.Instance.TrackerDevices
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

                    Application.Current.Dispatcher.Invoke(() =>
                        ViewModel.Instance.TrackerDevices.Add(device));
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

            foreach (var device in ViewModel.Instance.TrackerDevices)
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
                catch
                {
                }

                _vrSystem = null;
                _isInitialized = false;
            }
        }

        private void MarkAllDisconnected()
        {
            foreach (var device in ViewModel.Instance.TrackerDevices)
            {
                device.IsConnected = false;
                device.DeviceIndex = -1;
                device.IsCharging = false;
            }
        }

        public string BuildChatboxString()
        {
            UpdateDevices();

            bool globalEmergency = ViewModel.Instance.TrackerBattery_GlobalEmergency;

            string template = string.IsNullOrWhiteSpace(ViewModel.Instance.TrackerBattery_Template)
                ? "{icon} {name} {batt}%"
                : ViewModel.Instance.TrackerBattery_Template;

            string separator = string.IsNullOrWhiteSpace(ViewModel.Instance.TrackerBattery_Separator)
                ? " | "
                : ViewModel.Instance.TrackerBattery_Separator;

            IEnumerable<TrackerDevice> activeDevices = ViewModel.Instance.TrackerDevices
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
                        : ViewModel.Instance.TrackerBattery_OfflineBatteryText;
                }

                string statusText = device.IsConnected
                    ? (device.IsCharging ? "Charging" : ViewModel.Instance.TrackerBattery_OnlineText)
                    : ViewModel.Instance.TrackerBattery_OfflineText;

                string lowTag = (isLow && !device.IsCharging)
                    ? ViewModel.Instance.TrackerBattery_LowTag
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

                if (ViewModel.Instance.TrackerBattery_CompactWhitespace)
                {
                    entry = CompactWhitespace(entry);
                }

                entry = TrimEntry(entry, ViewModel.Instance.TrackerBattery_MaxEntryLength);

                if (!string.IsNullOrWhiteSpace(entry))
                {
                    entries.Add(entry.Trim());
                }
            }

            string message = entries.Count == 0 ? string.Empty : string.Join(separator, entries);

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(ViewModel.Instance.TrackerBattery_Prefix))
            {
                message = $"{ViewModel.Instance.TrackerBattery_Prefix} {message}";
            }

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(ViewModel.Instance.TrackerBattery_Suffix))
            {
                message = $"{message} {ViewModel.Instance.TrackerBattery_Suffix}";
            }

            if (!string.IsNullOrWhiteSpace(message) && ViewModel.Instance.TrackerBattery_UseSmallText)
            {
                message = ToSmallTextPreserveSymbols(message);
            }

            UpdatePreview(message);
            return message.Trim();
        }

        private void UpdateSummary(string scanStatus)
        {
            int total = ViewModel.Instance.TrackerDevices.Count;
            int connected = ViewModel.Instance.TrackerDevices.Count(d => d.IsConnected);

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ViewModel.Instance.TrackerBattery_DeviceSummary = $"{connected}/{total} connected";
                ViewModel.Instance.TrackerBattery_LastScanDisplay = scanStatus == "VR not running"
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

            bool showDisconnected = ViewModel.Instance.TrackerBattery_ShowDisconnected;
            if (!showDisconnected && !device.IsConnected)
            {
                return false;
            }

            if (device.DeviceKind == "Controller" && !ViewModel.Instance.TrackerBattery_ShowControllers)
            {
                return false;
            }

            if (device.DeviceKind == "HMD" && !ViewModel.Instance.TrackerBattery_ShowHeadset)
            {
                return false;
            }

            if (device.DeviceKind == "Tracker" && !ViewModel.Instance.TrackerBattery_ShowTrackers)
            {
                return false;
            }

            return true;
        }

        private int GetLowThreshold(TrackerDevice device)
        {
            return device.UseCustomLowThreshold
                ? device.CustomLowThreshold
                : ViewModel.Instance.TrackerBattery_LowThreshold;
        }

        private IEnumerable<TrackerDevice> ApplySort(IEnumerable<TrackerDevice> devices)
        {
            switch (ViewModel.Instance.TrackerBattery_SortMode)
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
            int maxEntries = ViewModel.Instance.TrackerBattery_MaxEntries;
            if (maxEntries <= 0 || devices.Count <= maxEntries)
            {
                _rotationIndex = 0;
                return devices;
            }

            if (!ViewModel.Instance.TrackerBattery_RotateOverflow)
            {
                return devices.Take(maxEntries).ToList();
            }

            int intervalSeconds = Math.Max(1, ViewModel.Instance.TrackerBattery_RotationIntervalSeconds);
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
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var target = ViewModel.Instance.TrackerBatteryActiveDevices;
                target.Clear();
                foreach (var device in devices)
                {
                    target.Add(device);
                }
            });
        }

        private void UpdatePreview(string message)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ViewModel.Instance.TrackerBattery_Preview = message ?? string.Empty;
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
                    string transformed = DataController.TransformToSuperscript(character.ToString());
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
