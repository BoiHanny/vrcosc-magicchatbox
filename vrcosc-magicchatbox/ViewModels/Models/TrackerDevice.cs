using System.ComponentModel;
using Newtonsoft.Json;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public class TrackerDevice : INotifyPropertyChanged
    {
        private float _batteryLevel;
        private bool _isCharging;
        private string _customName = string.Empty;
        private string _customIcon = string.Empty;
        private bool _isHidden;
        private bool _showOnlyOnLowBattery;
        private bool _useCustomLowThreshold;
        private int _customLowThreshold = 20;
        private bool _isConnected;
        private int _deviceIndex = -1;
        private string _serialNumber = string.Empty;
        private string _originalModelName = string.Empty;
        private string _deviceKind = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        // Immutable data from SteamVR/OpenVR
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value ?? string.Empty;
                    NotifyPropertyChanged(nameof(SerialNumber));
                }
            }
        }

        public string OriginalModelName
        {
            get => _originalModelName;
            set
            {
                if (_originalModelName != value)
                {
                    _originalModelName = value ?? string.Empty;
                    NotifyPropertyChanged(nameof(OriginalModelName));
                    NotifyPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string DeviceKind
        {
            get => _deviceKind;
            set
            {
                if (_deviceKind != value)
                {
                    _deviceKind = value ?? string.Empty;
                    NotifyPropertyChanged(nameof(DeviceKind));
                }
            }
        }

        // Live data (not saved)
        [JsonIgnore]
        public int DeviceIndex
        {
            get => _deviceIndex;
            set
            {
                if (_deviceIndex != value)
                {
                    _deviceIndex = value;
                    NotifyPropertyChanged(nameof(DeviceIndex));
                }
            }
        }

        [JsonIgnore]
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    NotifyPropertyChanged(nameof(IsConnected));
                    NotifyPropertyChanged(nameof(IsLowBattery));
                }
            }
        }

        [JsonIgnore]
        public bool IsCharging
        {
            get => _isCharging;
            set
            {
                if (_isCharging != value)
                {
                    _isCharging = value;
                    NotifyPropertyChanged(nameof(IsCharging));
                }
            }
        }

        [JsonIgnore]
        public float BatteryLevel
        {
            get => _batteryLevel;
            set
            {
                if (System.Math.Abs(_batteryLevel - value) > 0.009f)
                {
                    _batteryLevel = value;
                    NotifyPropertyChanged(nameof(BatteryLevel));
                    NotifyPropertyChanged(nameof(BatteryPercentage));
                    NotifyPropertyChanged(nameof(IsLowBattery));
                }
            }
        }

        [JsonIgnore]
        public int BatteryPercentage => (int)(_batteryLevel * 100);

        // User customization (saved)
        public string CustomName
        {
            get => _customName;
            set
            {
                if (_customName != value)
                {
                    _customName = value ?? string.Empty;
                    NotifyPropertyChanged(nameof(CustomName));
                    NotifyPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string CustomIcon
        {
            get => _customIcon;
            set
            {
                if (_customIcon != value)
                {
                    _customIcon = value ?? string.Empty;
                    NotifyPropertyChanged(nameof(CustomIcon));
                }
            }
        }

        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    _isHidden = value;
                    NotifyPropertyChanged(nameof(IsHidden));
                }
            }
        }

        public bool ShowOnlyOnLowBattery
        {
            get => _showOnlyOnLowBattery;
            set
            {
                if (_showOnlyOnLowBattery != value)
                {
                    _showOnlyOnLowBattery = value;
                    NotifyPropertyChanged(nameof(ShowOnlyOnLowBattery));
                }
            }
        }

        public bool UseCustomLowThreshold
        {
            get => _useCustomLowThreshold;
            set
            {
                if (_useCustomLowThreshold != value)
                {
                    _useCustomLowThreshold = value;
                    NotifyPropertyChanged(nameof(UseCustomLowThreshold));
                    NotifyPropertyChanged(nameof(IsLowBattery));
                }
            }
        }

        public int CustomLowThreshold
        {
            get => _customLowThreshold;
            set
            {
                int clamped = value < 1 ? 1 : (value > 100 ? 100 : value);
                if (_customLowThreshold != clamped)
                {
                    _customLowThreshold = clamped;
                    NotifyPropertyChanged(nameof(CustomLowThreshold));
                    NotifyPropertyChanged(nameof(IsLowBattery));
                }
            }
        }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? OriginalModelName : CustomName;

        [JsonIgnore]
        public bool IsLowBattery => IsConnected &&
            BatteryPercentage <= (UseCustomLowThreshold ? CustomLowThreshold : ViewModel.Instance.TrackerBattery_LowThreshold);

        public void NotifyIsLowBatteryChanged()
        {
            NotifyPropertyChanged(nameof(IsLowBattery));
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
