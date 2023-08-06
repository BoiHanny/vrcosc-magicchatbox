using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public class StatsManager
    {
        private readonly List<ComponentStatsItem> _componentStats = new List<ComponentStatsItem>();

        public StatsManager()
        {
            // Initialize the component stats with default values (assuming the initial value is an empty string)
            foreach (StatsComponentType type in Enum.GetValues(typeof(StatsComponentType)))
            {
                _componentStats.Add(new ComponentStatsItem(type.ToString(), type, "", "", type == StatsComponentType.FPS? false : true));
            }
        }

        public void UpdateStatValue(StatsComponentType type, string newValue)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if (item != null)
            {
                item.ComponentValue = newValue;
                item.LastUpdated = DateTime.Now;
            }
        }

        public string GetStatValue(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ComponentValue;
        }

        public void ToggleStatEnabledStatus(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if (item != null)
            {
                item.IsEnabled = !item.IsEnabled;
            }
        }

        public void SetStatMaxValue(StatsComponentType type, string maxValue)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if (item != null)
            {
                item.ComponentValueMax = maxValue;
            }
        }

        public string GetStatMaxValue(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ComponentValueMax;
        }

        // Provide a way to get all stats
        public IEnumerable<ComponentStatsItem> GetAllStats()
        {
            return _componentStats.AsReadOnly();
        }

        public string GenerateStatsDescription()
        {
            return string.Join(" ┆ ", _componentStats
                .Where(stat => stat.IsEnabled)
                .Select(stat => stat.ShowMaxValue ? $"{stat.ComponentName}: {stat.ComponentValue}/{stat.ComponentValueMax}" : $"{stat.ComponentName}: {stat.ComponentValue}"));
        }
    }
}
