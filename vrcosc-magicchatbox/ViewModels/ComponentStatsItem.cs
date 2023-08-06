using System;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ComponentStatsItem
    {
        public DateTime StartedOn { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; }
        public string ComponentName { get; set; }
        public StatsComponentType ComponentType { get; set; }
        public string ComponentValue { get; set; }
        public string ComponentValueMax { get; set; }

        public bool ShowMaxValue { get; set; }
        public bool IsEnabled { get; set; } = true;

        public ComponentStatsItem(string name, StatsComponentType type, string value, string valueMax, bool showMaxValue)
        {
            ComponentName = name;
            ComponentType = type;
            ComponentValue = value;
            ComponentValueMax = valueMax;
            ShowMaxValue = showMaxValue;
        }
    }
}
