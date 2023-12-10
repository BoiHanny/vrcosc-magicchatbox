using System;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public class ComponentStatsItem
    {
        public DateTime StartedOn { get; set; } = DateTime.Now;

        public DateTime LastUpdated { get; set; }

        public string SystemMainName { get; set; }

        public string SystemMailSmallName { get; set; }

        public string HardwareFriendlyName { get; set; }

        public string HardwareFriendlyNameSmall { get; set; }

        public bool ShowPrefixHardwareTitle { get; set; } = false;

        public bool ReplaceWithHardwareName { get; set; } = false;

        public string CustomHardwarenameValue { get; set; }

        public string CustomHardwarenameValueSmall { get; set; }

        public StatsComponentType ComponentType { get; set; }

        public string Unit { get; set; }

        public bool ShowTemperature { get; set; } = true;

        public bool ShowWattage { get; set; } = false;

        public bool ShowUnit { get; set; } = true;

        public string ComponentValue { get; set; }

        public string ComponentValueMax { get; set; }

        public bool Available { get; set; } = true;

        public bool RemoveNumberTrailing { get; set; } = true;

        public bool ShowMaxValue { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool ShowSmallName { get; set; } = true;

        public string GetFormattedValue() { return ShowUnit ? $"{ComponentValue}{Unit}" : ComponentValue; }

        public string GetFormattedMaxValue()
        {
            if (ShowMaxValue)
            {
                return ShowUnit
                    ? $"{ComponentValue}/{ComponentValueMax} {Unit}"
                    : $"{ComponentValue}/{ComponentValueMax}";
            }
            return GetFormattedValue();
        }

        public string GetDescription()
        {
            string name = ShowPrefixHardwareTitle
                ? ReplaceWithHardwareName && !string.IsNullOrEmpty(CustomHardwarenameValue) && !string.IsNullOrEmpty(CustomHardwarenameValueSmall) ? CustomHardwarenameValue : HardwareFriendlyName
                : SystemMainName;

            string smallName = ShowPrefixHardwareTitle
                ? ReplaceWithHardwareName && !string.IsNullOrEmpty(CustomHardwarenameValue) && !string.IsNullOrEmpty(CustomHardwarenameValueSmall)
                    ? CustomHardwarenameValueSmall
                    : HardwareFriendlyNameSmall
                : SystemMailSmallName;

            if (ShowSmallName)
            {
                return ShowMaxValue ? $"{smallName} {GetFormattedMaxValue()}" : $"{smallName} {GetFormattedValue()}";
            }
            else
            {
                return ShowMaxValue ? $"{name}: {GetFormattedMaxValue()}" : $"{name}: {GetFormattedValue()}";
            }
        }


        public ComponentStatsItem(
            string name,
            string smallName,
            StatsComponentType type,
            string value,
            string valueMax,
            bool showMaxValue,
            string unit,
            bool isenabled = true)
        {
            SystemMainName = name;
            SystemMailSmallName = smallName;
            ComponentType = type;
            ComponentValue = value;
            ComponentValueMax = valueMax;
            ShowMaxValue = showMaxValue;
            Unit = unit;
            IsEnabled = isenabled;
        }
    }
}
