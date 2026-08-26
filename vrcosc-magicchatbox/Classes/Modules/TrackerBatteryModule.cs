using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Vr;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules
{
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
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);

        private IDisposable? _sessionLease;

        private CVRSystem? _vrSystem;
        private int _rotationIndex;
        private DateTime _lastRotationUtc = DateTime.MinValue;
        private readonly StringBuilder _stringBuilder = new StringBuilder(256);

        private ObservableCollection<TrackerDevice>? _observedDevices;
        private IReadOnlyList<TrackerDevice> _deviceSnapshot = Array.Empty<TrackerDevice>();
        private readonly object _lock = new();
        private Timer? _refreshTimer;
        private bool _disposed;

        private readonly ISettingsProvider<TrackerBatterySettings> _settingsProvider;
        public TrackerBatterySettings Settings => _settingsProvider.Value;
        public void SaveSettings() => _settingsProvider.Save();

        public string Name => "TrackerBattery";
        public bool IsEnabled { get; set; } = true;
        public bool IsRunning { get; private set; }
        public Task InitializeAsync(CancellationToken ct = default) { Initialize(); return Task.CompletedTask; }

        public Task StartAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_disposed || IsRunning)
                    return Task.CompletedTask;

                _refreshTimer = new Timer(_ => RefreshTick(), null, TimeSpan.Zero, RefreshInterval);
                IsRunning = true;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                _refreshTimer?.Dispose();
                _refreshTimer = null;
                IsRunning = false;
            }

            ReleaseSession("StopAsync");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            _consentService.ConsentChanged -= OnConsentChanged;
            _tracker.PropertyChanged -= OnTrackerStateChanged;

            var observed = _observedDevices;
            if (observed != null)
            {
                observed.CollectionChanged -= OnDeviceCollectionChanged;
            }
            _observedDevices = null;

            StopAsync().GetAwaiter().GetResult();
        }

        private void RefreshTick()
        {
            if (_disposed)
                return;

            try
            {
                UpdateDevices();
                BuildChatboxString();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Tracker battery refresh failed: {ex.Message}");
            }
        }

        private readonly IAppState _appState;
        private readonly TrackerDisplayState _tracker;
        private readonly IntegrationDisplayState _integrationDisplay;
        private readonly IUiDispatcher _dispatcher;
        private readonly IPrivacyConsentService _consentService;
        private readonly IOpenVrSessionService _session;
        private readonly IToastService? _toast;

        public TrackerBatteryModule(
            ISettingsProvider<TrackerBatterySettings> settingsProvider,
            IAppState appState,
            TrackerDisplayState tracker,
            IntegrationDisplayState integrationDisplay,
            IUiDispatcher dispatcher,
            IPrivacyConsentService consentService,
            IOpenVrSessionService session,
            IToastService? toast = null)
        {
            _settingsProvider = settingsProvider;
            _appState = appState;
            _tracker = tracker;
            _integrationDisplay = integrationDisplay;
            _dispatcher = dispatcher;
            _consentService = consentService;
            _session = session;
            _toast = toast;

            _consentService.ConsentChanged += OnConsentChanged;
            _tracker.PropertyChanged += OnTrackerStateChanged;
            AttachDeviceCollection();
        }

        private IReadOnlyList<TrackerDevice> DeviceSnapshot => Volatile.Read(ref _deviceSnapshot);

        private void OnTrackerStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName)
                && !string.Equals(e.PropertyName, nameof(TrackerDisplayState.TrackerDevices), StringComparison.Ordinal))
            {
                return;
            }

            AttachDeviceCollection();
        }

        private void AttachDeviceCollection()
        {
            var current = _tracker.TrackerDevices;
            var previous = _observedDevices;

            if (!ReferenceEquals(previous, current))
            {
                if (previous != null)
                {
                    previous.CollectionChanged -= OnDeviceCollectionChanged;
                }

                _observedDevices = current;

                if (current != null)
                {
                    current.CollectionChanged += OnDeviceCollectionChanged;
                }
            }

            RefreshDeviceSnapshot();
        }

        private void OnDeviceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshDeviceSnapshot();
        }

        private void RefreshDeviceSnapshot()
        {
            var source = _observedDevices;

            IReadOnlyList<TrackerDevice> snapshot = source == null || source.Count == 0
                ? Array.Empty<TrackerDevice>()
                : source.ToArray();

            Volatile.Write(ref _deviceSnapshot, snapshot);
        }

        private void OnConsentChanged(object? sender, ConsentChangedEventArgs e)
        {
            if (e.Hook != PrivacyHook.VrTrackerBattery)
                return;

            if (e.NewState == ConsentState.Denied && _sessionLease != null)
            {
                ReleaseSession("Privacy consent revoked");
                _toast?.Show("🔒 VR Tracker", "Tracker battery monitoring paused — privacy consent revoked.", ToastType.Privacy, key: "tracker-privacy-denied");
            }
        }

        public void Initialize()
        {
            if (_sessionLease != null)
            {
                return;
            }

            if (!_consentService.IsApproved(PrivacyHook.VrTrackerBattery))
            {
                return;
            }

            _sessionLease = _session.AcquireLease(PrivacyHook.VrTrackerBattery, Name);
        }

        public void UpdateDevices()
        {
            if (!_appState.IsVRRunning)
            {
                MarkAllDisconnected();
                UpdateSummary("VR not running");
                return;
            }

            Initialize();

            _vrSystem = _session.System;
            if (_vrSystem == null)
            {
                MarkAllDisconnected();
                UpdateSummary("Waiting for SteamVR...");
                return;
            }

            var currentSerialNumbers = new HashSet<string>();
            IReadOnlyList<TrackerDevice> knownDevices = DeviceSnapshot;

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

                TrackerDevice? device = knownDevices
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

                    _dispatcher.BeginInvoke(() =>
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

            foreach (var device in knownDevices)
            {
                if (!currentSerialNumbers.Contains(device.SerialNumber))
                {
                    device.IsConnected = false;
                    device.DeviceIndex = -1;
                    device.IsCharging = false;
                    device.BatteryLevel = 0f;
                }
            }

            UpdateSummary("Scanning...");
        }

        private void ReleaseSession(string reason)
        {
            MarkAllDisconnected();
            UpdateSummary(reason);

            _vrSystem = null;
            _sessionLease?.Dispose();
            _sessionLease = null;
        }

        private void MarkAllDisconnected()
        {
            foreach (var device in DeviceSnapshot)
            {
                device.IsConnected = false;
                device.DeviceIndex = -1;
                device.IsCharging = false;
            }
        }

        public string BuildChatboxString()
        {
            bool globalEmergency = Settings.GlobalEmergency;

            IEnumerable<TrackerDevice> activeDevices = DeviceSnapshot
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

            string message = ComposeMessage(displayDevices, Settings);

            UpdatePreview(message);
            return message.Trim();
        }

        public static string ComposeMessage(IEnumerable<TrackerDevice> devices, TrackerBatterySettings settings)
        {
            string template = string.IsNullOrWhiteSpace(settings.Template)
                ? "{icon} {name} {batt}%"
                : settings.Template;

            string separator = string.IsNullOrWhiteSpace(settings.Separator)
                ? " | "
                : settings.Separator;

            var entries = new List<string>();
            foreach (var device in devices)
            {
                int lowThreshold = GetLowThreshold(device, settings);
                bool isLow = device.IsConnected && device.BatteryPercentage <= lowThreshold;

                string entry = BuildEntry(device, template, settings, isLow);

                if (!string.IsNullOrWhiteSpace(entry))
                {
                    entries.Add(entry.Trim());
                }
            }

            string message = entries.Count == 0 ? string.Empty : string.Join(separator, entries);

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(settings.Prefix))
            {
                message = $"{Raise(settings.Prefix, settings.UseSmallText)} {message}";
            }

            if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(settings.Suffix))
            {
                message = $"{message} {Raise(settings.Suffix, settings.UseSmallText)}";
            }

            return message;
        }

        public static string BuildSampleMessage(TrackerBatterySettings settings)
        {
            var sample = new List<TrackerDevice>
            {
                new()
                {
                    SerialNumber = "SAMPLE-HMD",
                    OriginalModelName = "Headset",
                    DeviceKind = "HMD",
                    CustomIcon = "🥽",
                    IsConnected = true,
                    BatteryLevel = 0.82f,
                },
                new()
                {
                    SerialNumber = "SAMPLE-CTRL",
                    OriginalModelName = "Left controller",
                    DeviceKind = "Controller",
                    CustomIcon = "🎮",
                    IsConnected = true,
                    BatteryLevel = 0.14f,
                },
                new()
                {
                    SerialNumber = "SAMPLE-TRKR",
                    OriginalModelName = "Waist tracker",
                    DeviceKind = "Tracker",
                    CustomIcon = "📍",
                    IsConnected = true,
                    BatteryLevel = 0.57f,
                },
            };

            IEnumerable<TrackerDevice> shown = sample.Where(d => ShouldIncludeDevice(d, settings));

            if (settings.GlobalEmergency)
            {
                shown = shown.Where(d => d.BatteryPercentage <= GetLowThreshold(d, settings));
            }

            var ordered = ApplySort(shown, settings).ToList();

            if (settings.MaxEntries > 0 && ordered.Count > settings.MaxEntries)
            {
                ordered = ordered.Take(settings.MaxEntries).ToList();
            }

            return ComposeMessage(ordered, settings);
        }

        private void UpdateSummary(string scanStatus)
        {
            IReadOnlyList<TrackerDevice> devices = DeviceSnapshot;
            int total = devices.Count;
            int connected = devices.Count(d => d.IsConnected);

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
            _vrSystem?.GetStringTrackedDeviceProperty(
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

            var role = _vrSystem?.GetControllerRoleForTrackedDeviceIndex(deviceIndex);
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

        private static void NormalizeLegacyIcon(TrackerDevice device, string? deviceKind)
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

        private static string GetDefaultIcon(string? deviceKind)
        {
            if (DefaultIconsByKind.TryGetValue(deviceKind ?? string.Empty, out var icon))
            {
                return icon;
            }

            return string.Empty;
        }

        private bool ShouldIncludeDevice(TrackerDevice device) => ShouldIncludeDevice(device, Settings);

        public static bool ShouldIncludeDevice(TrackerDevice device, TrackerBatterySettings settings)
        {
            if (device.IsHidden)
            {
                return false;
            }

            bool showDisconnected = settings.ShowDisconnected;
            if (!showDisconnected && !device.IsConnected)
            {
                return false;
            }

            if (device.DeviceKind == "Controller" && !settings.ShowControllers)
            {
                return false;
            }

            if (device.DeviceKind == "HMD" && !settings.ShowHeadset)
            {
                return false;
            }

            if (device.DeviceKind == "Tracker" && !settings.ShowTrackers)
            {
                return false;
            }

            return true;
        }

        private int GetLowThreshold(TrackerDevice device) => GetLowThreshold(device, Settings);

        public static int GetLowThreshold(TrackerDevice device, TrackerBatterySettings settings)
        {
            return device.UseCustomLowThreshold
                ? device.CustomLowThreshold
                : settings.LowThreshold;
        }

        private IEnumerable<TrackerDevice> ApplySort(IEnumerable<TrackerDevice> devices) => ApplySort(devices, Settings);

        public static IEnumerable<TrackerDevice> ApplySort(IEnumerable<TrackerDevice> devices, TrackerBatterySettings settings)
        {
            switch (settings.SortMode)
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

                if (target.Count == devices.Count)
                {
                    bool identical = true;
                    for (int i = 0; i < devices.Count; i++)
                    {
                        if (!ReferenceEquals(target[i], devices[i]))
                        {
                            identical = false;
                            break;
                        }
                    }
                    if (identical)
                        return;
                }

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

        public static string BuildEntry(TrackerDevice device, string template, TrackerBatterySettings settings, bool isLow)
        {
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
                    : settings.OfflineBatteryText;
            }

            string statusText = device.IsConnected
                ? (device.IsCharging ? "Charging" : settings.OnlineText)
                : settings.OfflineText;

            string lowTag = (isLow && !device.IsCharging)
                ? settings.LowTag
                : string.Empty;

            bool small = settings.UseSmallText;

            string entry = template
                .Replace("{icon}", device.CustomIcon ?? string.Empty)
                .Replace("{name}", Raise(displayName, small))
                .Replace("{batt}", batteryText ?? string.Empty)
                .Replace("{status}", Raise(statusText, small))
                .Replace("{low}", lowTag ?? string.Empty)
                .Replace("{kind}", Raise(device.DeviceKind, small))
                .Replace("{serial}", device.SerialNumber ?? string.Empty)
                .Replace("{model}", Raise(device.OriginalModelName, small));

            if (settings.CompactWhitespace)
            {
                entry = CompactWhitespace(entry);
            }

            return TrimEntry(entry, settings.MaxEntryLength);
        }

        private static string Raise(string value, bool small)
        {
            if (!small || string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            return ToSmallTextPreserveSymbols(value);
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
