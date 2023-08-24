using System;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ComponentStatsItem
    {
        public DateTime StartedOn { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; }
        public string ComponentName { get; set; }
        public string ComponentSmallName { get; set; }
        public StatsComponentType ComponentType { get; set; }
        public string Unit { get; set; }
        public bool ShowUnit { get; set; } = true;

        public string ComponentValue { get; set; }
        public string ComponentValueMax { get; set; }

        public bool ShowMaxValue { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool ShowSmallName { get; set; } = true;

        public string GetFormattedValue()
        {
            return ShowUnit ? $"{ComponentValue}{Unit}" : ComponentValue;
        }

        public string GetFormattedMaxValue()
        {
            if (ShowMaxValue)
            {
                return ShowUnit ? $"{ComponentValue}/{ComponentValueMax} {Unit}" : $"{ComponentValue}/{ComponentValueMax}";
            }
            return GetFormattedValue();
        }

        public string GetDescription()
        {
            if (ShowSmallName)
            {
                return ShowMaxValue
                    ? $"{ComponentSmallName} {GetFormattedMaxValue()}"
                    : $"{ComponentSmallName} {GetFormattedValue()}";
            }
            else
            {
                return ShowMaxValue
                    ? $"{ComponentName}: {GetFormattedMaxValue()}"
                    : $"{ComponentName}: {GetFormattedValue()}";
            }
        }

        public ComponentStatsItem(string name, string smallName, StatsComponentType type, string value, string valueMax, bool showMaxValue, string unit)
        {
            ComponentName = name;
            ComponentSmallName = smallName;
            ComponentType = type;
            ComponentValue = value;
            ComponentValueMax = valueMax;
            ShowMaxValue = showMaxValue;
            Unit = unit;
        }
    }
}
