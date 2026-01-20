using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public class WeatherConditionOverrideItem : INotifyPropertyChanged
    {
        private string _customIcon = string.Empty;
        private string _customText = string.Empty;

        public WeatherConditionOverrideItem(int code, string defaultIcon, string defaultText)
        {
            Code = code;
            DefaultIcon = defaultIcon ?? string.Empty;
            DefaultText = defaultText ?? string.Empty;
        }

        public int Code { get; }

        public string DefaultIcon { get; }

        public string DefaultText { get; }

        public string DisplayLabel
            => string.IsNullOrWhiteSpace(DefaultIcon)
                ? DefaultText
                : $"{DefaultIcon} {DefaultText}";

        public string CustomIcon
        {
            get => _customIcon;
            set
            {
                string normalized = value ?? string.Empty;
                if (_customIcon != normalized)
                {
                    _customIcon = normalized;
                    NotifyPropertyChanged(nameof(CustomIcon));
                }
            }
        }

        public string CustomText
        {
            get => _customText;
            set
            {
                string normalized = value ?? string.Empty;
                if (_customText != normalized)
                {
                    _customText = normalized;
                    NotifyPropertyChanged(nameof(CustomText));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
