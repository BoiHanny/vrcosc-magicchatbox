using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
            if(ViewModel.Instance.IntgrComponentStats && ViewModel.Instance.IntgrComponentStats_VR &&
                    ViewModel.Instance.IsVRRunning || ViewModel.Instance.IntgrComponentStats &&
                    ViewModel.Instance.IntgrComponentStats_DESKTOP &&
                    !ViewModel.Instance.IsVRRunning)
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
                if(_componentStats == null || _componentStats.Count == 0) return;
                var jsonData = JsonConvert.SerializeObject(_componentStats);
                File.WriteAllText(FileName, jsonData);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }

        }

        public bool GetShowMaxValue(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ShowMaxValue ?? false;
        }

        public void SetShowMaxValue(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ShowMaxValue = state;
            }
        }


        public void LoadComponentStats()
        {
            try
            {
                FileName = Path.Combine(ViewModel.Instance.DataPath, "ComponentStatsV1.json");
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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.FireExitSave();
                        RestartApplication();
                    });

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
                            unit = "﹪";
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

                    if (type == StatsComponentType.FPS)
                    {
                        component.ShowUnit = false;
                    }
                    if(type == StatsComponentType.VRAM || type == StatsComponentType.RAM)
                    {
                        component.RemoveNumberTrailing = false;
                        component.IsEnabled = false;
                    }
                    _componentStats.Add(component);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    started = true;
                });
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }

        }

        private async void RestartApplication()
        {
            // Obtain the full path of the current application
            string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Replace .dll with .exe to get the path to the executable
            string exePath = dllPath.Replace(".dll", ".exe");

            // Create a new process to start the application again
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = false 
            };

            // Start the new process
            System.Diagnostics.Process.Start(psi);

            // Wait for a short delay
            await Task.Delay(500); 

            // Shut down the current application
            Application.Current.Shutdown();
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

        public bool IsStatAvailable(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.Available ?? false;
        }

        public void SetStatAvailable(StatsComponentType type, bool available)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.Available = available;
            }
        }

        public bool GetShowSmallName(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ShowSmallName ?? false;
        }

        public void SetShowSmallName(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ShowSmallName = state;
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

        public void ActivateStateState(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.IsEnabled = state;
            }
        }

        public string GetHardwareName(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.HardwareFriendlyName;
        }

        public void SetHardwareTitle(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ShowPrefixHardwareTitle = state;
            }
        }

        public bool GetHardwareTitleState(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ShowPrefixHardwareTitle ?? false;
        }

        public string GetCustomHardwareName(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.CustomHardwarenameValue;
        }

        public void SetCustomHardwareName(StatsComponentType type, string name)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.CustomHardwarenameValue = name;
            }
        }

        public bool GetShowReplaceWithHardwareName(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ReplaceWithHardwareName ?? false;
        }

        public void SetReplaceWithHardwareName(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ReplaceWithHardwareName = state;
            }
        }

        public bool GetRemoveNumberTrailing(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.RemoveNumberTrailing ?? false;
        }

        public void SetRemoveNumberTrailing(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.RemoveNumberTrailing = state;
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

        public bool IsStatMaxValueShown(StatsComponentType type)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            return item?.ShowMaxValue ?? false;
        }

        public void SetStatMaxValueShown(StatsComponentType type, bool state)
        {
            var item = _componentStats.FirstOrDefault(stat => stat.ComponentType == type);
            if(item != null)
            {
                item.ShowMaxValue = state;
            }
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
                .Where(stat => stat != null && stat.IsEnabled && stat.Available)
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
