using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes
{
    public class StatsManager
    {
        private readonly List<ComponentStatsItem> _componentStats = new List<ComponentStatsItem>();
        private static string FileName = null;
        public bool started = false;


        public void StartModule()
        {
            LoadComponentStats();
        }

        public IReadOnlyList<ComponentStatsItem> GetAllStats()
        {
            return _componentStats.AsReadOnly();
        }

        public void SaveComponentStats()
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(_componentStats);
                File.WriteAllText(FileName, jsonData);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }

        }

        public void LoadComponentStats()
        {
            try
            {
                FileName = Path.Combine(ViewModel.Instance.DataPath, "ComponentStats.json");
                if (File.Exists(FileName))
                {
                    var jsonData = File.ReadAllText(FileName);
                    var loadedStats = JsonConvert.DeserializeObject<List<ComponentStatsItem>>(jsonData);
                    if (loadedStats != null)
                    {
                        _componentStats.Clear();
                        _componentStats.AddRange(loadedStats);
                    }
                    started = true;

                }
                else
                {
                    InitializeDefaultStats();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
            
        }

        private void InitializeDefaultStats()
        {
            try
            {
                foreach (StatsComponentType type in Enum.GetValues(typeof(StatsComponentType)))
                {
                    var unit = "";
                    switch (type)
                    {
                        case StatsComponentType.CPU:
                            unit = "﹪";
                            break;
                        case StatsComponentType.GPU:
                            unit = "﹪"; // assuming GPU also uses percentage
                            break;
                        case StatsComponentType.RAM:
                            unit = "ᵍᵇ";
                            break;
                        case StatsComponentType.VRAM:
                            unit = "ᵍᵇ";
                            break;
                        case StatsComponentType.FPS:
                            unit = "ᶠᵖˢ";
                            break;
                    }

                    var component = new ComponentStatsItem(
                        type.ToString(),
                        type.GetSmallName(),
                        type,
                        "",
                        "",
                        !(type == StatsComponentType.FPS ||
                          type == StatsComponentType.GPU ||
                          type == StatsComponentType.CPU),
                        unit
                    );

                    // Determine whether to show the unit for each type
                    // For example, if you don't want to show units for FPS, do:
                    if (type == StatsComponentType.FPS)
                    {
                        component.ShowUnit = false;
                    }

                    _componentStats.Add(component);
                }
                started = true;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
           
        }




        public void UpdateStatValue(StatsComponentType type, string newValue)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
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
            if(item != null)
            {
                item.IsEnabled = !item.IsEnabled;
            }
        }

        public void SetStatMaxValue(StatsComponentType type, string maxValue)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ComponentValueMax = maxValue;
            }
        }

        public void ActivateStat(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.IsEnabled = true;
            }
        }

        public void DeactivateStat(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.IsEnabled = false;
            }
        }

        public bool IsStatEnabled(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.IsEnabled ?? false;
        }

        public string GetStatMaxValue(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ComponentValueMax;
        }


        private static readonly StatsComponentType[] StatDisplayOrder =
        {
            StatsComponentType.CPU,
            StatsComponentType.GPU,
            StatsComponentType.VRAM,
            StatsComponentType.RAM,
            //StatsComponentType.FPS
        };

        public string GenerateStatsDescription()
        {
            const int maxLineWidth = 30;
            var separator = "┆";
            List<string> lines = new List<string>();
            string currentLine = "";

            var componentsList = StatDisplayOrder
                .Select(type => _componentStats.FirstOrDefault(stat => stat.ComponentType == type))
                .Where(stat => stat != null && stat.IsEnabled)
                .Select(stat => stat.GetDescription())
                .ToList();

            foreach (var component in componentsList)
            {
                if (currentLine.Length + component.Length > maxLineWidth)
                {
                    lines.Add(currentLine.TrimEnd());
                    currentLine = "";
                }

                if (currentLine.Length > 0)
                {
                    currentLine += separator;
                }

                currentLine += component;
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.TrimEnd());
            }
            ViewModel.Instance.ComponentStatsLastUpdate = DateTime.Now;
            return string.Join("\v", lines);
        }



    }
}
