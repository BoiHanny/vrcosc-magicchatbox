﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels.Models;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.ViewModels
{


    public class ViewModel : INotifyPropertyChanged
    {

        public static readonly ViewModel Instance = new ViewModel();

        private AfkModule _AfkModule;
        private bool _autoSelectGPU = true; // Default to true for automatically selecting the GPU

        private bool _BlankEgg = false;

        private DateTime _BussyBoysDate = DateTime.Today;

        private bool _BussyBoysDateEnable = false;

        private bool _BussyBoysMode = false;

        private bool _BussyBoysMultiMODE = false;


        private bool _ChatAddSmallDelay = true;

        private double _ChatAddSmallDelayTIME = 1.4;

        private bool _ChatLiveEdit = true;

        private bool _ChatSendAgainFX = true;

        private double _ChattingUpdateRate = 3;


        private bool _ComponentStatsGPU3DHook = false;


        private bool _ComponentStatsGPU3DVRAMHook = false;

        private ObservableCollection<ComponentStatsItem> _componentStatsListPrivate = new ObservableCollection<ComponentStatsItem>();

        private bool _ComponentStatsRunning = false;


        private bool _CountOculusSystemAsVR = false;


        private bool _CycleStatus = true;

        private bool _DisableMediaLink = false;


        private bool _Egg_Dev = false;

        private string _EggPrefixIconStatus = "";


        private List<string> _gpuList;



        private PulsoidModule _HeartRateConnector;


        private IntelliChatModule _IntelliChatModule;

        private bool _IntgrComponentStats = false;




        private bool _IntgrComponentStats_DESKTOP = false;


        private bool _IntgrComponentStats_VR = true;

        private bool _IntgrCurrentTime_DESKTOP = false;

        private bool _IntgrCurrentTime_VR = true;


        private bool _IntgrHeartRate_DESKTOP = false;

        private bool _IntgrHeartRate_OSC = false;

        private bool _IntgrHeartRate_VR = true;

        private bool _IntgrMediaLink_DESKTOP = true;


        private bool _IntgrMediaLink_VR = true;

        private bool _IntgrScanForce = true;


        private bool _IntgrScanMediaLink = true;


        private bool _IntgrSpotifyStatus_DESKTOP = true;

        private bool _IntgrSpotifyStatus_VR = true;

        private bool _IntgrStatus_DESKTOP = true;

        private bool _IntgrStatus_VR = true;

        private bool _IntgrWindowActivity_DESKTOP = true;

        private bool _IntgrWindowActivity_VR = false;


        private bool _IsFahrenheit = false;


        private bool _IsRandomCycling = true;


        private bool _IsTemperatureSwitchEnabled = true;

        private bool _IzuruBaeMode = false;


        private bool _JoinedAlphaChannel = true;


        private bool _KeepUpdatingChat = true;

        private DateTime _LastSwitchCycle = DateTime.Now;

        private readonly object _lock = new object();


        private int _MainWindowBlurEffect = 0;

        private ObservableCollection<MediaLinkStyle> _MediaLinkSeekbarStyles;


        private bool _MediaSession_AutoSwitch = true;

        private bool _MediaSession_AutoSwitchSpawn = true;


        private int _MediaSession_Timeout = 3;

        private ObservableCollection<MediaSessionInfo> _MediaSessions = new ObservableCollection<MediaSessionInfo>();


        private bool _PulsoidAuthConnected = false;



        private bool _RealTimeChatEdit = true;


        private bool _RollBackUpdateAvailable = false;


        private System.Version _RollBackVersion = new System.Version(0, 0, 0, 0);

        private List<MediaSessionSettings> _SavedSessionSettings = new List<MediaSessionSettings>();

        private string _secOSCIP = "127.0.0.1";
        private string _selectedGPU;


        private MediaLinkStyle _SelectedMediaLinkSeekbarStyle;

        private bool _Settings_Dev = false;


        private bool _Settings_NetworkStatistics = false;

        private SoundpadModule _SoundpadModule;


        private int _StatusIndex = 0;


        private int _SwitchStatusInterval = 3;


        private int _TemperatureDisplaySwitchInterval = 5;

        private string _thirdOSCIP = "127.0.0.1";


        private bool _TTSOnResendChat = false;


        private bool _UnmuteMainOutput = true;


        private bool _UseEmojisForTempAndPower = false;


        private WhisperModule _WhisperModule;



        private bool autoDowngradeSeekbar = true;

        public readonly ComponentStatsModule _statsManager = new ComponentStatsModule();
        public Dictionary<string, Action<bool>> SettingsMap;




        public ViewModel()
        {
            ActivateStatusCommand = new RelayCommand(ActivateStatus);
            ToggleVoiceCommand = new RelayCommand(ToggleVoice);

            ScannedApps.CollectionChanged += ScannedApps_CollectionChanged;
            SortScannedAppsByProcessNameCommand = new RelayCommand(() => SortScannedApps(SortProperty.ProcessName));
            SortScannedAppsByFocusCountCommand = new RelayCommand(() => SortScannedApps(SortProperty.FocusCount));
            SortScannedAppsByUsedNewMethodCommand = new RelayCommand(() => SortScannedApps(SortProperty.UsedNewMethod));
            SortScannedAppsByIsPrivateAppCommand = new RelayCommand(() => SortScannedApps(SortProperty.IsPrivateApp));
            SortScannedAppsByIsShowInfoAppCommand = new RelayCommand(() => SortScannedApps(SortProperty.ShowInfo));
            SortScannedAppsByApplyCustomAppNameCommand = new RelayCommand(() => SortScannedApps(SortProperty.ApplyCustomAppName));

            ActivateSettingCommand = new RelayCommand<string>(ActivateSetting);

            TimezoneFriendlyNames = new Dictionary<Timezone, string>
            {
                { Timezone.UTC, "Coordinated Universal Time (UTC)" },
                { Timezone.GMT, "Greenwich Mean Time (GMT)" },
                { Timezone.EST, "Eastern Time (US & Canada)" },
                { Timezone.CST, "Central Time (US & Canada)" },
                { Timezone.MST, "Mountain Time (US & Canada)" },
                { Timezone.PST, "Pacific Time (US & Canada)" },
                { Timezone.AKST, "Alaska Time (AKST)" },
                { Timezone.HST, "Hawaii Standard Time (HST)" },
                { Timezone.CET, "Central European Time (CET)" },
                { Timezone.EET, "Eastern European Time (EET)" },
                { Timezone.IST, "India Standard Time (IST)" },
                { Timezone.CSTChina, "China Standard Time (CST)" },
                { Timezone.JST, "Japan Standard Time (JST)" },
                { Timezone.KST, "Korea Standard Time (KST)" },
                { Timezone.MSK, "Moscow Standard Time (MSK)" },
                { Timezone.AEST, "Australian Eastern Time (AET)" },
                { Timezone.NZST, "New Zealand Time (NZT)" },
                { Timezone.BRT, "Brasília Time (BRT)" },
                { Timezone.SAST, "South Africa Standard Time (SAST)" },
            };

            SettingsMap = new Dictionary<string, Action<bool>>
            {
                { nameof(Settings_WindowActivity), value => Settings_WindowActivity = value },
                { nameof(Settings_MediaLink), value => Settings_MediaLink = value },
                { nameof(Settings_OpenAI), value => Settings_OpenAI = value },
                { nameof(Settings_Chatting), value => Settings_Chatting = value },
                { nameof(Settings_ComponentStats), value => Settings_ComponentStats = value },
                { nameof(Settings_NetworkStatistics), value => Settings_NetworkStatistics = value },
                { nameof(Settings_AppOptions), value => Settings_AppOptions = value },
                { nameof(Settings_TTS), value => Settings_TTS = value },
                { nameof(Settings_Time), value => Settings_Time = value },
                { nameof(Settings_HeartRate), value => Settings_HeartRate = value },
                { nameof(Settings_Status), value => Settings_Status = value }
            };

            ShuffleEmojis();
            CurrentEmoji = GetNextEmoji();
        }

        private void ProcessInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Resort();
        }



        private void Resort()
        {
            if (_currentSortProperty != default)
            {
                UpdateSortedApps();
            }
        }

        private void ScannedApps_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Resort();
            if (e.OldItems != null)
            {
                foreach (ProcessInfo processInfo in e.OldItems)
                {
                    processInfo.PropertyChanged -= ProcessInfo_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ProcessInfo processInfo in e.NewItems)
                {
                    processInfo.PropertyChanged += ProcessInfo_PropertyChanged;
                }
            }
        }

        private void ToggleVoice()
        {
            if (Instance.ToggleVoiceWithV)
                OSCSender.ToggleVoice(true);
        }

        private void UpdateSortedApps()
        {
            lock (_lock)
            {
                ObservableCollection<ProcessInfo> tempList = null;
                try
                {
                    var copiedList = ScannedApps.ToList(); // Create a copy of ScannedApps
                    IOrderedEnumerable<ProcessInfo> sortedScannedApps = null;

                    switch (_currentSortProperty)
                    {
                        case SortProperty.ProcessName:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.ProcessName)
                                : copiedList.OrderByDescending(process => process.ProcessName);
                            break;

                        case SortProperty.UsedNewMethod:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.UsedNewMethod)
                                : copiedList.OrderByDescending(process => process.UsedNewMethod);
                            break;

                        case SortProperty.ApplyCustomAppName:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.ApplyCustomAppName)
                                : copiedList.OrderByDescending(process => process.ApplyCustomAppName);
                            break;

                        case SortProperty.IsPrivateApp:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.IsPrivateApp)
                                : copiedList.OrderByDescending(process => process.IsPrivateApp);
                            break;

                        case SortProperty.FocusCount:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.FocusCount)
                                : copiedList.OrderByDescending(process => process.FocusCount);
                            break;

                        case SortProperty.ShowInfo:
                            sortedScannedApps = _sortDirection[_currentSortProperty]
                                ? copiedList.OrderBy(process => process.ShowTitle)
                                : copiedList.OrderByDescending(process => process.ShowTitle);
                            break;
                    }

                    if (sortedScannedApps != null && sortedScannedApps.Any())
                    {
                        tempList = new ObservableCollection<ProcessInfo>(sortedScannedApps);
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex);
                }

                if (tempList != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScannedApps = tempList;
                    });
                }
            }
        }

        private void UpdateToggleVoiceText()
        { ToggleVoiceText = ToggleVoiceWithV ? "Toggle voice (V)" : "Toggle voice"; }

        public void ActivateSetting(string settingName)
        {
            if (SettingsMap.ContainsKey(settingName))
            {
                foreach (var setting in SettingsMap)
                {
                    setting.Value(setting.Key == settingName);
                }
                MainWindow.ChangeMenuItem(3);
            }
        }

        public static void ActivateStatus(object parameter)
        {
            try
            {
                var item = parameter as StatusItem;
                foreach (var i in ViewModel.Instance.StatusList)
                {
                    if (i == item)
                    {
                        i.IsActive = true;
                        i.LastUsed = DateTime.Now;
                    }
                    else
                    {
                        i.IsActive = false;
                    }
                }
                SaveStatusList();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }



        public static bool CreateIfMissing(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    return true;
                }
                return true;
            }
            catch (IOException ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                return false;
            }
        }

        public static void SaveStatusList()
        {
            try
            {
                if (CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.StatusList);
                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml"), json);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void ScannedAppsItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "FocusCount")
            {
                Application.Current.Dispatcher
                    .Invoke(
                        () =>
                        {
                            CollectionViewSource.GetDefaultView(ScannedApps).Refresh();
                        });
            }
        }

        public void SortScannedApps(SortProperty sortProperty)
        {
            if (!_sortDirection.ContainsKey(sortProperty))
            {
                Logging.WriteException(new Exception($"No sortDirection: {sortProperty}"), MSGBox: false);
                return;
            }
            try
            {
                _currentSortProperty = sortProperty;
                var isAscending = _sortDirection[sortProperty];
                _sortDirection[sortProperty] = !isAscending;
                UpdateSortedApps();
                NotifyPropertyChanged(nameof(ScannedApps));
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void StartModules()
        {
            HeartRateConnector = new PulsoidModule();
            SoundpadModule = new(1000);


            PropertyChanged += HeartRateConnector.PropertyChangedHandler;
            PropertyChanged += SoundpadModule.PropertyChangedHandler;
        }

        public void UpdateComponentStatsList(ObservableCollection<ComponentStatsItem> newList)
        {
            _componentStatsListPrivate.Clear();
            foreach (var item in newList)
            {
                _componentStatsListPrivate.Add(item);
            }
        }

        public AfkModule AfkModule
        {
            get { return _AfkModule; }
            set
            {
                _AfkModule = value;
                NotifyPropertyChanged(nameof(AfkModule));
            }
        }
        public bool AutoDowngradeSeekbar
        {
            get { return autoDowngradeSeekbar; }
            set
            {
                if (autoDowngradeSeekbar != value)
                {
                    autoDowngradeSeekbar = value;
                    NotifyPropertyChanged(nameof(AutoDowngradeSeekbar));
                }
            }
        }

        public bool AutoSelectGPU
        {
            get => _autoSelectGPU;
            set
            {
                _autoSelectGPU = value;
                NotifyPropertyChanged(nameof(AutoSelectGPU));
            }
        }




        public bool BlankEgg
        {
            get { return _BlankEgg; }
            set
            {
                _BlankEgg = value;
                NotifyPropertyChanged(nameof(BlankEgg));
            }
        }

        public DateTime BussyBoysDate
        {
            get => _BussyBoysDate;
            set
            {
                if (value > DateTime.Today)
                {
                    value = DateTime.Today;
                }

                if (_BussyBoysDate != value)
                {
                    _BussyBoysDate = value;
                    NotifyPropertyChanged(nameof(BussyBoysDate));
                }
            }
        }
        public bool BussyBoysDateEnable
        {
            get => _BussyBoysDateEnable;
            set
            {
                _BussyBoysDateEnable = value;
                NotifyPropertyChanged(nameof(BussyBoysDateEnable));
            }
        }
        public bool BussyBoysMode
        {
            get => _BussyBoysMode;
            set
            {
                _BussyBoysMode = value;
                NotifyPropertyChanged(nameof(BussyBoysMode));
            }
        }
        public bool BussyBoysMultiMODE
        {
            get => _BussyBoysMultiMODE;
            set
            {
                _BussyBoysMultiMODE = value;
                NotifyPropertyChanged(nameof(BussyBoysMultiMODE));
            }
        }

        public bool ChatAddSmallDelay
        {
            get { return _ChatAddSmallDelay; }
            set
            {
                _ChatAddSmallDelay = value;
                NotifyPropertyChanged(nameof(ChatAddSmallDelay));
            }
        }

        public double ChatAddSmallDelayTIME
        {
            get { return _ChatAddSmallDelayTIME; }
            set
            {
                if (value < 0.1)
                {
                    _ChatAddSmallDelayTIME = 0.1;
                }
                else if (value > 2)
                {
                    _ChatAddSmallDelayTIME = 2;
                }
                _ChatAddSmallDelayTIME = Math.Round(value, 1);
                NotifyPropertyChanged(nameof(ChatAddSmallDelayTIME));
            }
        }

        public bool ChatLiveEdit
        {
            get { return _ChatLiveEdit; }
            set
            {
                _ChatLiveEdit = value;
                NotifyPropertyChanged(nameof(ChatLiveEdit));
            }
        }

        public bool ChatSendAgainFX
        {
            get { return _ChatSendAgainFX; }
            set
            {
                _ChatSendAgainFX = value;
                NotifyPropertyChanged(nameof(ChatSendAgainFX));
            }
        }

        public double ChattingUpdateRate
        {
            get { return _ChattingUpdateRate; }
            set
            {
                if (value < 2)
                {
                    _ChattingUpdateRate = 2;
                }
                else if (value > 10)
                {
                    _ChattingUpdateRate = 10;
                }

                _ChattingUpdateRate = Math.Round(value, 1);
                NotifyPropertyChanged(nameof(ChattingUpdateRate));
            }
        }

        public bool ComponentStatCPUTempVisible
        {
            get => _statsManager.GetShowCPUTemperature();
            set
            {
                if (value)
                {
                    _statsManager.SetShowCPUTemperature(true);
                }
                else
                {
                    _statsManager.SetShowCPUTemperature(false);
                }
                NotifyPropertyChanged(nameof(ComponentStatCPUTempVisible));
            }
        }

        public bool ComponentStatCPUWattageVisible
        {
            get => _statsManager.GetShowCPUWattage();
            set
            {
                if (value)
                {
                    _statsManager.SetShowCPUWattage(true);
                }
                else
                {
                    _statsManager.SetShowCPUWattage(false);
                }
                NotifyPropertyChanged(nameof(ComponentStatCPUWattageVisible));
            }
        }

        public bool ComponentStatGPUHotSpotVisible
        {
            get => _statsManager.GetShowGPUHotspotTemperature();
            set
            {
                if (value)
                {
                    _statsManager.SetShowGPUHotspotTemperature(true);
                }
                else
                {
                    _statsManager.SetShowGPUHotspotTemperature(false);
                }
                NotifyPropertyChanged(nameof(ComponentStatGPUHotSpotVisible));
            }
        }

        public bool ComponentStatGPUTempVisible
        {
            get => _statsManager.GetShowGPUTemperature();
            set
            {
                if (value)
                {
                    _statsManager.SetShowGPUTemperature(true);
                }
                else
                {
                    _statsManager.SetShowGPUTemperature(false);
                }
                NotifyPropertyChanged(nameof(ComponentStatGPUTempVisible));
            }
        }

        public bool ComponentStatGPUWattageVisible
        {
            get => _statsManager.GetShowGPUWattage();
            set
            {
                if (value)
                {
                    _statsManager.SetShowGPUWattage(true);
                }
                else
                {
                    _statsManager.SetShowGPUWattage(false);
                }
                NotifyPropertyChanged(nameof(ComponentStatGPUWattageVisible));
            }
        }




        public string ComponentStatsError
        {
            get { return _statsManager.GetWhitchComponentsAreNotAvailableString(); }
        }

        public bool ComponentStatsGPU3DHook
        {
            get { return _ComponentStatsGPU3DHook; }
            set
            {
                if (_ComponentStatsGPU3DHook != value)
                {
                    _ComponentStatsGPU3DHook = value;
                    NotifyPropertyChanged(nameof(ComponentStatsGPU3DHook));
                    NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                    NotifyPropertyChanged(nameof(ComponentStatsError));
                }
            }

        }
        public bool ComponentStatsGPU3DVRAMHook
        {
            get { return _ComponentStatsGPU3DVRAMHook; }
            set
            {
                _ComponentStatsGPU3DVRAMHook = value;
                NotifyPropertyChanged(nameof(ComponentStatsGPU3DVRAMHook));
            }
        }
        public ReadOnlyObservableCollection<ComponentStatsItem> ComponentStatsList => new ReadOnlyObservableCollection<ComponentStatsItem>(_componentStatsListPrivate);
        public bool ComponentStatsRunning
        {
            get { return _ComponentStatsRunning; }
            set
            {
                _ComponentStatsRunning = value;
                NotifyPropertyChanged(nameof(ComponentStatsRunning));
            }
        }

        public bool CountOculusSystemAsVR
        {
            get { return _CountOculusSystemAsVR; }
            set
            {
                if (_CountOculusSystemAsVR != value)
                {
                    _CountOculusSystemAsVR = value;
                    NotifyPropertyChanged(nameof(CountOculusSystemAsVR));
                }
            }
        }

        public bool CPU_EnableHardwareTitle
        {
            get => _statsManager.GetHardwareTitleState(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(CPU_EnableHardwareTitle));
            }
        }

        public bool CPU_NumberTrailingZeros
        {
            get => _statsManager.GetRemoveNumberTrailing(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(CPU_NumberTrailingZeros));
            }
        }

        public bool CPU_PrefixHardwareTitle
        {
            get => _statsManager.GetShowReplaceWithHardwareName(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(CPU_PrefixHardwareTitle));
            }
        }

        public bool CPU_SmallName
        {
            get => _statsManager.GetShowSmallName(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.SetShowSmallName(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.SetShowSmallName(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(CPU_SmallName));
            }
        }

        public string CPUCustomHardwareName
        {
            get => _statsManager.GetCustomHardwareName(StatsComponentType.CPU);
            set
            {
                _statsManager.SetCustomHardwareName(StatsComponentType.CPU, value);
                NotifyPropertyChanged(nameof(CPUCustomHardwareName));
            }
        }

        public string CPUHardwareName
        {
            get => _statsManager.GetHardwareName(StatsComponentType.CPU);
        }

        public bool CycleStatus
        {
            get { return _CycleStatus; }
            set
            {
                if (_CycleStatus != value)
                {
                    _CycleStatus = value;
                    NotifyPropertyChanged(nameof(CycleStatus));
                }
            }
        }

        public bool DisableMediaLink
        {
            get { return _DisableMediaLink; }
            set
            {
                _DisableMediaLink = value;
                if (_DisableMediaLink)
                {
                    IntgrScanMediaLink = false;
                }
                else
                {
                    IntgrScanSpotify_OLD = false;
                }
                NotifyPropertyChanged(nameof(DisableMediaLink));
            }
        }

        public bool Egg_Dev
        {
            get { return _Egg_Dev; }
            set
            {
                _Egg_Dev = value;
                NotifyPropertyChanged(nameof(Egg_Dev));
            }
        }

        public string EggPrefixIconStatus
        {
            get { return _EggPrefixIconStatus; }
            set
            {
                _EggPrefixIconStatus = value;
                NotifyPropertyChanged(nameof(EggPrefixIconStatus));
            }
        }

        public bool GPU_EnableHardwareTitle
        {
            get => _statsManager.GetHardwareTitleState(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(GPU_EnableHardwareTitle));
            }
        }

        public bool GPU_NumberTrailingZeros
        {
            get => _statsManager.GetRemoveNumberTrailing(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(GPU_NumberTrailingZeros));
            }
        }



        public bool GPU_PrefixHardwareTitle
        {
            get => _statsManager.GetShowReplaceWithHardwareName(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(GPU_PrefixHardwareTitle));
            }
        }

        public bool GPU_SmallName
        {
            get => _statsManager.GetShowSmallName(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetShowSmallName(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetShowSmallName(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(GPU_SmallName));
            }
        }

        public string GPUCustomHardwareName
        {
            get => _statsManager.GetCustomHardwareName(StatsComponentType.GPU);
            set
            {
                _statsManager.SetCustomHardwareName(StatsComponentType.GPU, value);
                NotifyPropertyChanged(nameof(GPUCustomHardwareName));
            }
        }

        public string GPUHardwareName
        {
            get => _statsManager.GetHardwareName(StatsComponentType.GPU);
        }

        public List<string> GPUList
        {
            get => _gpuList;
            set
            {
                _gpuList = value;
                NotifyPropertyChanged(nameof(GPUList));
            }
        }
        public PulsoidModule HeartRateConnector
        {
            get { return _HeartRateConnector; }
            set
            {
                _HeartRateConnector = value;
                NotifyPropertyChanged(nameof(HeartRateConnector));
            }
        }
        public IntelliChatModule IntelliChatModule
        {
            get { return _IntelliChatModule; }
            set
            {
                _IntelliChatModule = value;
                NotifyPropertyChanged(nameof(IntelliChatModule));
            }
        }

        public bool IntgrComponentStats
        {
            get { return _IntgrComponentStats; }
            set
            {
                _IntgrComponentStats = value;
                if (value || !_statsManager.started)
                {
                    _statsManager.StartModule();
                }
                NotifyPropertyChanged(nameof(IntgrComponentStats));
            }
        }
        public bool IntgrComponentStats_DESKTOP
        {
            get { return _IntgrComponentStats_DESKTOP; }
            set
            {
                _IntgrComponentStats_DESKTOP = value;
                if (value || !_statsManager.started)
                {
                    _statsManager.StartModule();
                }
                NotifyPropertyChanged(nameof(IntgrComponentStats_DESKTOP));
            }
        }
        public bool IntgrComponentStats_VR
        {
            get { return _IntgrComponentStats_VR; }
            set
            {
                _IntgrComponentStats_VR = value;
                if (value || !_statsManager.started)
                {
                    _statsManager.StartModule();
                }
                NotifyPropertyChanged(nameof(IntgrComponentStats_VR));
            }
        }

        public bool IntgrCurrentTime_DESKTOP
        {
            get { return _IntgrCurrentTime_DESKTOP; }
            set
            {
                _IntgrCurrentTime_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrCurrentTime_DESKTOP));
            }
        }


        public bool IntgrCurrentTime_VR
        {
            get { return _IntgrCurrentTime_VR; }
            set
            {
                _IntgrCurrentTime_VR = value;
                NotifyPropertyChanged(nameof(IntgrCurrentTime_VR));
            }
        }

        public bool IntgrHeartRate_DESKTOP
        {
            get { return _IntgrHeartRate_DESKTOP; }
            set
            {
                _IntgrHeartRate_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrHeartRate_DESKTOP));
            }
        }

        public bool IntgrHeartRate_OSC
        {
            get { return _IntgrHeartRate_OSC; }
            set
            {
                _IntgrHeartRate_OSC = value;
                NotifyPropertyChanged(nameof(IntgrHeartRate_OSC));
            }
        }

        public bool IntgrHeartRate_VR
        {
            get { return _IntgrHeartRate_VR; }
            set
            {
                _IntgrHeartRate_VR = value;
                NotifyPropertyChanged(nameof(IntgrHeartRate_VR));
            }
        }

        public bool IntgrMediaLink_DESKTOP
        {
            get { return _IntgrMediaLink_DESKTOP; }
            set
            {
                _IntgrMediaLink_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrMediaLink_DESKTOP));
            }
        }

        public bool IntgrMediaLink_VR
        {
            get { return _IntgrMediaLink_VR; }
            set
            {
                _IntgrMediaLink_VR = value;
                NotifyPropertyChanged(nameof(IntgrMediaLink_VR));
            }
        }
        public bool IntgrScanForce
        {
            get { return _IntgrScanForce; }
            set
            {
                _IntgrScanForce = value;
                NotifyPropertyChanged(nameof(IntgrScanForce));
            }
        }

        public bool IntgrScanMediaLink
        {
            get { return _IntgrScanMediaLink; }
            set
            {
                _IntgrScanMediaLink = value;
                if (_IntgrScanMediaLink)
                {
                    IntgrScanSpotify_OLD = false;
                }
                NotifyPropertyChanged(nameof(IntgrScanMediaLink));
            }
        }

        public bool IntgrSpotifyStatus_DESKTOP
        {
            get { return _IntgrSpotifyStatus_DESKTOP; }
            set
            {
                _IntgrSpotifyStatus_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrSpotifyStatus_DESKTOP));
            }
        }

        public bool IntgrSpotifyStatus_VR
        {
            get { return _IntgrSpotifyStatus_VR; }
            set
            {
                _IntgrSpotifyStatus_VR = value;
                NotifyPropertyChanged(nameof(IntgrSpotifyStatus_VR));
            }
        }

        public bool IntgrStatus_DESKTOP
        {
            get { return _IntgrStatus_DESKTOP; }
            set
            {
                _IntgrStatus_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrStatus_DESKTOP));
            }
        }

        public bool IntgrStatus_VR
        {
            get { return _IntgrStatus_VR; }
            set
            {
                _IntgrStatus_VR = value;
                NotifyPropertyChanged(nameof(IntgrStatus_VR));
            }
        }

        public bool IntgrWindowActivity_DESKTOP
        {
            get { return _IntgrWindowActivity_DESKTOP; }
            set
            {
                _IntgrWindowActivity_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrWindowActivity_DESKTOP));
            }
        }

        public bool IntgrWindowActivity_VR
        {
            get { return _IntgrWindowActivity_VR; }
            set
            {
                _IntgrWindowActivity_VR = value;
                NotifyPropertyChanged(nameof(IntgrWindowActivity_VR));
            }
        }

        public bool isCPUAvailable
        {
            get => _statsManager.IsStatAvailable(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.SetStatAvailable(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.SetStatAvailable(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(isCPUAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool IsCPUEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStateState(StatsComponentType.CPU, true);
                }
                else
                {
                    _statsManager.ActivateStateState(StatsComponentType.CPU, false);
                }
                NotifyPropertyChanged(nameof(IsCPUEnabled));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool IsFahrenheit
        {
            get { return _IsFahrenheit; }
            set
            {
                if (_IsFahrenheit != value)
                {
                    _IsFahrenheit = value;
                    NotifyPropertyChanged(nameof(IsFahrenheit));
                }
            }
        }


        public bool IsGPUAvailable
        {
            get => _statsManager.IsStatAvailable(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetStatAvailable(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetStatAvailable(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(IsGPUAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool IsGPUEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStateState(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.ActivateStateState(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(IsGPUEnabled));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool IsGPUMaxValueShown
        {
            get => _statsManager.IsStatMaxValueShown(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.GPU, true);
                }
                else
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.GPU, false);
                }
                NotifyPropertyChanged(nameof(IsGPUMaxValueShown));

            }
        }

        public bool isRAMAvailable
        {
            get => _statsManager.IsStatAvailable(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetStatAvailable(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetStatAvailable(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(isRAMAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }


        public bool IsRAMEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStateState(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.ActivateStateState(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(IsRAMEnabled));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool isRAMMaxValueShown
        {
            get => _statsManager.IsStatMaxValueShown(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(isRAMMaxValueShown));
            }
        }

        public bool IsRandomCycling
        {
            get { return _IsRandomCycling; }
            set
            {
                if (_IsRandomCycling != value)
                {
                    _IsRandomCycling = value;
                    NotifyPropertyChanged(nameof(IsRandomCycling));
                }
            }
        }

        public bool IsTemperatureSwitchEnabled
        {
            get { return _IsTemperatureSwitchEnabled; }
            set
            {
                if (_IsTemperatureSwitchEnabled != value)
                {
                    _IsTemperatureSwitchEnabled = value;
                    NotifyPropertyChanged(nameof(IsTemperatureSwitchEnabled));
                }
            }
        }

        public bool IsThereAComponentThatIsNotAvailable
        {
            get { return _statsManager.IsThereAComponentThatIsNotAvailable(); }
        }

        public bool IsThereAComponentThatIsNotGettingTempOrWattage
        {
            get { return _statsManager.IsThereAComponentThatIsNotGettingTempOrWattage(); }
        }

        public bool isVRAMAvailable
        {
            get => _statsManager.IsStatAvailable(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetStatAvailable(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetStatAvailable(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(isVRAMAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }

        public bool IsVRAMEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStateState(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.ActivateStateState(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(IsVRAMEnabled));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotAvailable));
                NotifyPropertyChanged(nameof(IsThereAComponentThatIsNotGettingTempOrWattage));
                NotifyPropertyChanged(nameof(ComponentStatsError));
            }
        }


        public bool isVRAMMaxValueShown
        {
            get => _statsManager.IsStatMaxValueShown(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetStatMaxValueShown(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(isVRAMMaxValueShown));
            }
        }
        public bool IzuruBaeMode
        {
            get => _IzuruBaeMode;
            set
            {
                _IzuruBaeMode = value;
                NotifyPropertyChanged(nameof(IzuruBaeMode));
            }
        }

        public bool JoinedAlphaChannel
        {
            get { return _JoinedAlphaChannel; }
            set
            {
                _JoinedAlphaChannel = value;

                var updateCheckTask = DataController.CheckForUpdateAndWait();
                var delayTask = Task.Delay(TimeSpan.FromSeconds(10));

                Task.Run(async () =>
                {
                    await Task.WhenAny(updateCheckTask, delayTask);
                });

                NotifyPropertyChanged(nameof(JoinedAlphaChannel));
            }
        }


        public bool KeepUpdatingChat
        {
            get { return _KeepUpdatingChat; }
            set
            {
                _KeepUpdatingChat = value;
                if (!value)
                {
                    ViewModel.Instance.ChatLiveEdit = false;
                }
                NotifyPropertyChanged(nameof(KeepUpdatingChat));
            }
        }

        public DateTime LastSwitchCycle
        {
            get { return _LastSwitchCycle; }
            set
            {
                if (_LastSwitchCycle != value)
                {
                    _LastSwitchCycle = value;
                    NotifyPropertyChanged(nameof(LastSwitchCycle));
                }
            }
        }
        public int MainWindowBlurEffect
        {
            get { return _MainWindowBlurEffect; }
            set
            {
                _MainWindowBlurEffect = value;
                NotifyPropertyChanged(nameof(MainWindowBlurEffect));
            }
        }

        public ObservableCollection<MediaLinkStyle> MediaLinkSeekbarStyles
        {
            get { return _MediaLinkSeekbarStyles; }
            set
            {
                if (_MediaLinkSeekbarStyles != value)
                {
                    _MediaLinkSeekbarStyles = value;
                    NotifyPropertyChanged(nameof(MediaLinkSeekbarStyles));
                }
            }
        }

        public bool MediaSession_AutoSwitch
        {
            get { return _MediaSession_AutoSwitch; }
            set
            {
                _MediaSession_AutoSwitch = value;
                NotifyPropertyChanged(nameof(MediaSession_AutoSwitch));
            }
        }

        public bool MediaSession_AutoSwitchSpawn
        {
            get { return _MediaSession_AutoSwitchSpawn; }
            set
            {
                _MediaSession_AutoSwitchSpawn = value;
                NotifyPropertyChanged(nameof(MediaSession_AutoSwitchSpawn));
            }
        }

        public int MediaSession_Timeout
        {
            get { return _MediaSession_Timeout; }
            set
            {
                _MediaSession_Timeout = value;
                NotifyPropertyChanged(nameof(MediaSession_Timeout));
            }
        }

        public ObservableCollection<MediaSessionInfo> MediaSessions
        {
            get { return _MediaSessions; }
            set
            {
                _MediaSessions = value;
                NotifyPropertyChanged(nameof(MediaSessions));
            }
        }

        public bool PulsoidAuthConnected
        {
            get { return _PulsoidAuthConnected; }
            set
            {
                if (_PulsoidAuthConnected != value)
                {
                    _PulsoidAuthConnected = value;
                    NotifyPropertyChanged(nameof(PulsoidAuthConnected));
                }
            }
        }

        public bool RAM_EnableHardwareTitle
        {
            get => _statsManager.GetHardwareTitleState(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(RAM_EnableHardwareTitle));
            }
        }

        public bool RAM_NumberTrailingZeros
        {
            get => _statsManager.GetRemoveNumberTrailing(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(RAM_NumberTrailingZeros));
            }
        }

        public bool RAM_PrefixHardwareTitle
        {
            get => _statsManager.GetShowReplaceWithHardwareName(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(RAM_PrefixHardwareTitle));
            }
        }

        public bool RAM_ShowDDRVersion
        {
            get => _statsManager.GetShowRamDDRVersion();
            set
            {
                if (value)
                {
                    _statsManager.SetShowRamDDRVersion(true);
                }
                else
                {
                    _statsManager.SetShowRamDDRVersion(false);
                }
                NotifyPropertyChanged(nameof(RAM_ShowDDRVersion));
            }
        }

        public bool RAM_ShowMaxValue
        {
            get => _statsManager.GetShowMaxValue(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetShowMaxValue(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetShowMaxValue(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(RAM_ShowMaxValue));
            }
        }

        public bool RAM_SmallName
        {
            get => _statsManager.GetShowSmallName(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.SetShowSmallName(StatsComponentType.RAM, true);
                }
                else
                {
                    _statsManager.SetShowSmallName(StatsComponentType.RAM, false);
                }
                NotifyPropertyChanged(nameof(RAM_SmallName));
            }
        }

        public string RAMCustomHardwareName
        {
            get => _statsManager.GetCustomHardwareName(StatsComponentType.RAM);
            set
            {
                _statsManager.SetCustomHardwareName(StatsComponentType.RAM, value);
                NotifyPropertyChanged(nameof(RAMCustomHardwareName));
            }
        }

        public string RAMHardwareName
        {
            get => _statsManager.GetHardwareName(StatsComponentType.RAM);

        }

        public bool RealTimeChatEdit
        {
            get { return _RealTimeChatEdit; }
            set
            {
                _RealTimeChatEdit = value;
                NotifyPropertyChanged(nameof(RealTimeChatEdit));
            }
        }

        public bool RollBackUpdateAvailable
        {
            get { return _RollBackUpdateAvailable; }
            set
            {
                if (_RollBackUpdateAvailable != value)
                {
                    _RollBackUpdateAvailable = value;
                    NotifyPropertyChanged(nameof(RollBackUpdateAvailable));
                }
            }
        }

        public System.Version RollBackVersion
        {
            get { return _RollBackVersion; }
            set
            {
                if (_RollBackVersion != value)
                {
                    _RollBackVersion = value;
                    NotifyPropertyChanged(nameof(RollBackVersion));
                }
            }
        }

        public List<MediaSessionSettings> SavedSessionSettings
        {
            get { return _SavedSessionSettings; }
            set
            {
                _SavedSessionSettings = value;
                NotifyPropertyChanged(nameof(SavedSessionSettings));
            }
        }
        public string SecOSCIP
        {
            get => _secOSCIP;
            set
            {
                _secOSCIP = value;
                NotifyPropertyChanged(nameof(SecOSCIP));
            }
        }

        public string SelectedGPU
        {
            get => _selectedGPU;
            set
            {
                _selectedGPU = value;
                NotifyPropertyChanged(nameof(SelectedGPU));
            }
        }

        public MediaLinkStyle SelectedMediaLinkSeekbarStyle
        {
            get { return _SelectedMediaLinkSeekbarStyle; }
            set
            {
                if (_SelectedMediaLinkSeekbarStyle != value)
                {
                    _SelectedMediaLinkSeekbarStyle = value;
                    NotifyPropertyChanged(nameof(SelectedMediaLinkSeekbarStyle));
                }
            }
        }

        public bool Settings_Dev
        {
            get { return _Settings_Dev; }
            set
            {
                _Settings_Dev = value;
                NotifyPropertyChanged(nameof(Settings_Dev));
            }
        }
        public bool Settings_NetworkStatistics
        {
            get { return _Settings_NetworkStatistics; }
            set
            {
                _Settings_NetworkStatistics = value;
                NotifyPropertyChanged(nameof(Settings_NetworkStatistics));
            }
        }
        public SoundpadModule SoundpadModule
        {
            get { return _SoundpadModule; }
            set
            {
                _SoundpadModule = value;
                NotifyPropertyChanged(nameof(SoundpadModule));
            }
        }

        public int StatusIndex
        {
            get { return _StatusIndex; }
            set
            {
                if (_StatusIndex != value)
                {
                    _StatusIndex = value;
                    NotifyPropertyChanged(nameof(StatusIndex));
                }
            }
        }

        public int SwitchStatusInterval
        {
            get { return _SwitchStatusInterval; }
            set
            {
                if (_SwitchStatusInterval != value)
                {
                    _SwitchStatusInterval = value;
                    NotifyPropertyChanged(nameof(SwitchStatusInterval));
                }
            }
        }

        public int TemperatureDisplaySwitchInterval
        {
            get { return _TemperatureDisplaySwitchInterval; }
            set
            {
                if (_TemperatureDisplaySwitchInterval != value)
                {
                    _TemperatureDisplaySwitchInterval = value;
                    NotifyPropertyChanged(nameof(TemperatureDisplaySwitchInterval));
                }
            }
        }

        public string TemperatureUnit
        {
            get
            {
                if (IsTemperatureSwitchEnabled)
                {
                    // Switch between "F" and "C" based on the interval
                    return (DateTime.Now.Second / TemperatureDisplaySwitchInterval) % 2 == 0 ? "F" : "C";
                }
                // When switching is off, return the static unit
                return IsFahrenheit ? "F" : "C";
            }
        }
        public string ThirdOSCIP
        {
            get => _thirdOSCIP;
            set
            {
                _thirdOSCIP = value;
                NotifyPropertyChanged(nameof(ThirdOSCIP));
            }
        }

        public Dictionary<Timezone, string> TimezoneFriendlyNames { get; }

        public bool TTSOnResendChat
        {
            get { return _TTSOnResendChat; }
            set
            {
                _TTSOnResendChat = value;
                NotifyPropertyChanged(nameof(TTSOnResendChat));
            }
        }
        public bool UnmuteMainOutput
        {
            get { return _UnmuteMainOutput; }
            set
            {
                _UnmuteMainOutput = value;
                NotifyPropertyChanged(nameof(UnmuteMainOutput));
            }
        }

        public bool UseEmojisForTempAndPower
        {
            get { return _UseEmojisForTempAndPower; }
            set
            {
                if (_UseEmojisForTempAndPower != value)
                {
                    _UseEmojisForTempAndPower = value;
                    NotifyPropertyChanged(nameof(UseEmojisForTempAndPower));
                }
            }
        }

        public bool VRAM_EnableHardwareTitle
        {
            get => _statsManager.GetHardwareTitleState(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetHardwareTitle(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(VRAM_EnableHardwareTitle));
            }
        }

        public bool VRAM_NumberTrailingZeros
        {
            get => _statsManager.GetRemoveNumberTrailing(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetRemoveNumberTrailing(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(VRAM_NumberTrailingZeros));
            }
        }

        public bool VRAM_PrefixHardwareTitle
        {
            get => _statsManager.GetShowReplaceWithHardwareName(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetReplaceWithHardwareName(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(VRAM_PrefixHardwareTitle));
            }
        }

        public bool VRAM_ShowMaxValue
        {
            get => _statsManager.GetShowMaxValue(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetShowMaxValue(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetShowMaxValue(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(VRAM_ShowMaxValue));
            }
        }

        public bool VRAM_SmallName
        {
            get => _statsManager.GetShowSmallName(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.SetShowSmallName(StatsComponentType.VRAM, true);
                }
                else
                {
                    _statsManager.SetShowSmallName(StatsComponentType.VRAM, false);
                }
                NotifyPropertyChanged(nameof(VRAM_SmallName));
            }
        }

        public string VRAMCustomHardwareName
        {
            get => _statsManager.GetCustomHardwareName(StatsComponentType.VRAM);
            set
            {
                _statsManager.SetCustomHardwareName(StatsComponentType.VRAM, value);
                NotifyPropertyChanged(nameof(VRAMCustomHardwareName));
            }
        }

        public string VRAMHardwareName
        {
            get => _statsManager.GetHardwareName(StatsComponentType.VRAM);
        }
        public WhisperModule WhisperModule
        {
            get { return _WhisperModule; }
            set
            {
                _WhisperModule = value;
                NotifyPropertyChanged(nameof(WhisperModule));
            }
        }



        #region ICommand's
        public ICommand ActivateStatusCommand { get; set; }

        public ICommand ToggleVoiceCommand { get; }

        public ICommand SortScannedAppsByProcessNameCommand { get; }

        public ICommand SortScannedAppsByFocusCountCommand { get; }

        public ICommand SortScannedAppsByUsedNewMethodCommand { get; }

        public ICommand SortScannedAppsByIsPrivateAppCommand { get; }

        public ICommand SortScannedAppsByIsShowInfoAppCommand { get; }

        public ICommand SortScannedAppsByApplyCustomAppNameCommand { get; }

        public RelayCommand<string> ActivateSettingCommand { get; }
        #endregion


        #region Properties
        private ObservableCollection<StatusItem> _StatusList = new ObservableCollection<StatusItem>();
        private ObservableCollection<ChatItem> _LastMessages = new ObservableCollection<ChatItem>();
        private string _aesKey = "g5X5pFei6G8W6UwK6UaA6YhC6U8W6ZbP";
        private string _PlayingSongTitle = string.Empty;
        private bool _ScanPause = false;
        private bool _Topmost = false;
        private int _ScanPauseTimeout = 25;
        private int _ScanPauseCountDown = 0;
        private string _NewStatusItemTxt = string.Empty;
        private string _NewChattingTxt = string.Empty;
        private string _ChatFeedbackTxt = string.Empty;
        private string _FocusedWindow = string.Empty;
        private string _StatusTopBarTxt = string.Empty;
        private string _ChatTopBarTxt = string.Empty;
        private bool _SpotifyActive = false;
        private bool _SpotifyPaused = false;
        private bool _IsVRRunning = false;
        private bool _MasterSwitch = true;
        private bool _PrefixTime = false;
        private bool _PrefixChat = true;
        private bool _ChatFX = true;
        private bool _TypingIndicator = false;
        private bool _PrefixIconMusic = true;
        private bool _PauseIconMusic = true;
        private bool _PrefixIconStatus = true;
        private bool _CountDownUI = true;
        private bool _Time24H = false;
        private string _OSCtoSent = string.Empty;
        private string _ApiStream = "b2t8DhYcLcu7Nu0suPcvc8MkHBjZNbEinG/3ybInlUK/5UkyNRVhK145nO7C4Mwhe1Zer1hBcG/F1b5f/BMcNFLXk4K6ozRcK7gHcebJZWnpxEDxjW6DyrZ/si913BPp";
        private Models.Version _AppVersion = new(DataController.GetApplicationVersion());
        private Models.Version _GitHubVersion;
        private string _VersionTxt = "Check for updates";
        private string _VersionTxtColor = "#FF8F80B9";
        private string _StatusBoxCount = "0/140";
        private string _StatusBoxColor = "#FF504767";
        private string _ChatBoxCount = "0/140";
        private string _ChatBoxColor = "#FF504767";
        private string _CurrentTime = string.Empty;
        private bool _IntgrStatus = true;
        private bool _IntgrScanWindowActivity = false;
        private bool _IntgrScanWindowTime = true;
        private bool _IntgrScanSpotify = false;
        private int _CurrentMenuItem = 0;
        private string _MenuItem_0_Visibility = "Hidden";
        private string _MenuItem_1_Visibility = "Hidden";
        private string _MenuItem_2_Visibility = "Hidden";
        private string _MenuItem_3_Visibility = "Visible";
        private int _OSCmsg_count = 0;
        private string _OSCmsg_countUI = string.Empty;
        private string _OSCIP = "127.0.0.1";
        private string _Char_Limit = "Hidden";
        private string _Spotify_Opacity = "1";

        private string _Soundpad_Opacity = "1";

        private string _Status_Opacity = "1";
        private string _Window_Opacity = "1";
        private string _Time_Opacity = "1";
        private string _HeartRate_Opacity = "1";
        private string _MediaLink_Opacity = "1";
        private int _OSCPortOut = 9000;
        private int _OSCPOrtIN = 9001;

        public void SyncComponentStatsList()
        {
            _componentStatsListPrivate.Clear();
            foreach (var stat in _statsManager.GetAllStats())
            {
                _componentStatsListPrivate.Add(stat);
            }
            NotifyPropertiesChanged(new string[]
            {
                nameof(GPUHardwareName),
                nameof(RAMHardwareName),
                nameof(CPUHardwareName),
                nameof(VRAMHardwareName),
                nameof(IsCPUEnabled),
                nameof(IsGPUEnabled),
                nameof(IsRAMEnabled),
                nameof(IsVRAMEnabled),
                nameof(isCPUAvailable),
                nameof(IsGPUAvailable),
                nameof(isRAMAvailable),
                nameof(isVRAMAvailable),
                nameof(CPUCustomHardwareName),
                nameof(GPUCustomHardwareName),
                nameof(RAMCustomHardwareName),
                nameof(VRAMCustomHardwareName),
                nameof(CPU_EnableHardwareTitle),
                nameof(GPU_EnableHardwareTitle),
                nameof(RAM_EnableHardwareTitle),
                nameof(VRAM_EnableHardwareTitle),
                nameof(CPU_PrefixHardwareTitle),
                nameof(GPU_PrefixHardwareTitle),
                nameof(RAM_PrefixHardwareTitle),
                nameof(VRAM_PrefixHardwareTitle),
                nameof(CPU_NumberTrailingZeros),
                nameof(GPU_NumberTrailingZeros),
                nameof(RAM_NumberTrailingZeros),
                nameof(VRAM_NumberTrailingZeros),
                nameof(CPU_SmallName),
                nameof(GPU_SmallName),
                nameof(RAM_SmallName),
                nameof(VRAM_SmallName)
            });
        }


        private bool _CheckUpdateOnStartup = true;
        public bool CheckUpdateOnStartup
        {
            get { return _CheckUpdateOnStartup; }
            set
            {
                _CheckUpdateOnStartup = value;
                NotifyPropertyChanged(nameof(CheckUpdateOnStartup));
            }
        }
        private string _WindowActivityPrivateName = "🔒 App";
        public string WindowActivityPrivateName
        {
            get { return _WindowActivityPrivateName; }
            set
            {
                _WindowActivityPrivateName = value;
                NotifyPropertyChanged(nameof(WindowActivityPrivateName));
            }
        }


        private string _ComponentStatCombined;
        public string ComponentStatCombined
        {
            get { return _ComponentStatCombined; }
            set
            {
                _ComponentStatCombined = value;
                NotifyPropertyChanged(nameof(ComponentStatCombined));
            }
        }

        public void UpdateComponentStat(StatsComponentType type, string newValue)
        {
            _statsManager.UpdateStatValue(type, newValue);
        }

        public string GetComponentStatValue(StatsComponentType type)
        {
            return _statsManager.GetStatValue(type);
        }

        public void SetComponentStatMaxValue(StatsComponentType type, string maxValue)
        {
            _statsManager.SetStatMaxValue(type, maxValue);
        }

        public int OSCPOrtIN
        {
            get { return _OSCPOrtIN; }
            set
            {
                _OSCPOrtIN = value;
                NotifyPropertyChanged(nameof(OSCPOrtIN));
            }
        }

        public void SetDataPath()
        {
            if (UseCustomProfile)
            {
                DataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    $"Vrcosc-MagicChatbox-profile-{ProfileNumber}");
            }
        }

        private string _DataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Vrcosc-MagicChatbox");
        private List<Voice> _TikTokTTSVoices;
        private Voice _SelectedTikTokTTSVoice;
        private bool _TTSTikTokEnabled = false;
        private AudioDevice _selectedAuxOutputDevice;
        private AudioDevice _selectedPlaybackOutputDevice;
        private List<AudioDevice> _playbackOutputDevices = new List<AudioDevice>();
        private List<AudioDevice> _auxOutputDevices = new List<AudioDevice>();
        private bool _TTSCutOff = true;
        private string _LogPath = @"C:\temp\Vrcosc-MagicChatbox";
        private string _RecentPlayBackOutput;
        private bool _VrcConnected;
        private bool _CanUpdate = false;
        private string _toggleVoiceText = "Toggle voice (V)";
        private bool _AutoUnmuteTTS = true;
        private bool _ToggleVoiceWithV = true;
        private bool _TTSBtnShadow = false;
        private float _TTSVolume = 0.2f;

        private ProcessInfo _LastProcessFocused = new ProcessInfo();



        private bool _AvatarSyncExecute = true;
        public bool AvatarSyncExecute
        {
            get { return _AvatarSyncExecute; }
            set
            {
                _AvatarSyncExecute = value;
                NotifyPropertyChanged(nameof(AvatarSyncExecute));
            }
        }

        private SortProperty _currentSortProperty;
        private Dictionary<SortProperty, bool> _sortDirection = new Dictionary<SortProperty, bool>
        {
            { SortProperty.ProcessName, true },
            { SortProperty.UsedNewMethod, true },
            { SortProperty.ShowInfo, true },
            { SortProperty.ApplyCustomAppName, true },
            { SortProperty.IsPrivateApp, true },
            { SortProperty.FocusCount, true }
        };

        private bool _SeperateWithENTERS = true;

        public bool SeperateWithENTERS
        {
            get { return _SeperateWithENTERS; }
            set
            {
                _SeperateWithENTERS = value;
                NotifyPropertyChanged(nameof(SeperateWithENTERS));
            }
        }

        private bool _WindowActivityTitleScan = true;
        public bool WindowActivityTitleScan
        {
            get { return _WindowActivityTitleScan; }
            set
            {
                _WindowActivityTitleScan = value;
                NotifyPropertyChanged(nameof(WindowActivityTitleScan));
            }
        }


        private bool _AutoShowTitleOnNewApp = false;
        public bool AutoShowTitleOnNewApp
        {
            get { return _AutoShowTitleOnNewApp; }
            set
            {
                _AutoShowTitleOnNewApp = value;
                NotifyPropertyChanged(nameof(AutoShowTitleOnNewApp));
            }
        }


        private bool _TitleOnAppVR = false;
        public bool TitleOnAppVR
        {
            get { return _TitleOnAppVR; }
            set
            {
                _TitleOnAppVR = value;
                NotifyPropertyChanged(nameof(TitleOnAppVR));
            }
        }


        private string _WindowActivityVRTitle = "In VR";
        public string WindowActivityVRTitle
        {
            get { return _WindowActivityVRTitle; }
            set
            {
                _WindowActivityVRTitle = value;
                NotifyPropertyChanged(nameof(WindowActivityVRTitle));
            }
        }


        private string _WindowActivityVRFocusTitle = "ᶠᵒᶜᵘˢˢⁱⁿᵍ ⁱⁿ";
        public string WindowActivityVRFocusTitle
        {
            get { return _WindowActivityVRFocusTitle; }
            set
            {
                _WindowActivityVRFocusTitle = value;
                NotifyPropertyChanged(nameof(WindowActivityVRFocusTitle));
            }
        }


        private string _WindowActivityDesktopTitle = "On desktop";
        public string WindowActivityDesktopTitle
        {
            get { return _WindowActivityDesktopTitle; }
            set
            {
                _WindowActivityDesktopTitle = value;
                NotifyPropertyChanged(nameof(WindowActivityDesktopTitle));
            }
        }


        private string _WindowActivityDesktopFocusTitle = "ⁱⁿ";
        public string WindowActivityDesktopFocusTitle
        {
            get { return _WindowActivityDesktopFocusTitle; }
            set
            {
                _WindowActivityDesktopFocusTitle = value;
                NotifyPropertyChanged(nameof(WindowActivityDesktopFocusTitle));
            }
        }


        private string _WindowActivityPrivateNameVR = "🔒 App";
        public string WindowActivityPrivateNameVR
        {
            get { return _WindowActivityPrivateNameVR; }
            set
            {
                _WindowActivityPrivateNameVR = value;
                NotifyPropertyChanged(nameof(WindowActivityPrivateNameVR));
            }
        }




        private bool _WindowActivityShowFocusedApp = true;
        public bool WindowActivityShowFocusedApp
        {
            get { return _WindowActivityShowFocusedApp; }
            set
            {
                _WindowActivityShowFocusedApp = value;
                NotifyPropertyChanged(nameof(WindowActivityShowFocusedApp));
            }
        }


        private string _ErrorInWindowActivityMsg = "Error without information";
        public string ErrorInWindowActivityMsg
        {
            get { return _ErrorInWindowActivityMsg; }
            set
            {
                _ErrorInWindowActivityMsg = value;
                NotifyPropertyChanged(nameof(ErrorInWindowActivityMsg));
            }
        }


        private bool _ErrorInWindowActivity = false;
        public bool ErrorInWindowActivity
        {
            get { return _ErrorInWindowActivity; }
            set
            {
                _ErrorInWindowActivity = value;
                NotifyPropertyChanged(nameof(ErrorInWindowActivity));
            }
        }


        private bool _LimitTitleOnApp = true;
        public bool LimitTitleOnApp
        {
            get { return _LimitTitleOnApp; }
            set
            {
                _LimitTitleOnApp = value;
                NotifyPropertyChanged(nameof(LimitTitleOnApp));
            }
        }

        private int _MaxShowTitleCount = 15;
        public int MaxShowTitleCount
        {
            get { return _MaxShowTitleCount; }
            set
            {
                _MaxShowTitleCount = value;
                NotifyPropertyChanged(nameof(MaxShowTitleCount));
            }
        }

        private bool _VersionTxtUnderLine = false;

        public bool VersionTxtUnderLine
        {
            get { return _VersionTxtUnderLine; }
            set
            {
                _VersionTxtUnderLine = value;
                NotifyPropertyChanged(nameof(VersionTxtUnderLine));
            }
        }

        private DateTime _ComponentStatsLastUpdate = DateTime.Now;

        public DateTime ComponentStatsLastUpdate
        {
            get { return _ComponentStatsLastUpdate; }
            set
            {
                _ComponentStatsLastUpdate = value;
                NotifyPropertyChanged(nameof(ComponentStatsLastUpdate));
            }
        }


        public string MediaLink_Opacity
        {
            get { return _MediaLink_Opacity; }
            set
            {
                _MediaLink_Opacity = value;
                NotifyPropertyChanged(nameof(MediaLink_Opacity));
            }
        }


        private string _ComponentStat_Opacity = "1";
        public string ComponentStat_Opacity
        {
            get { return _ComponentStat_Opacity; }
            set
            {
                _ComponentStat_Opacity = value;
                NotifyPropertyChanged(nameof(ComponentStat_Opacity));
            }
        }

        private bool _AutoSetDaylight = true;

        public bool AutoSetDaylight
        {
            get { return _AutoSetDaylight; }
            set
            {
                _AutoSetDaylight = value;
                NotifyPropertyChanged(nameof(AutoSetDaylight));
            }
        }


        private bool _SecOSC = false;

        public bool SecOSC
        {
            get { return _SecOSC; }
            set
            {
                _SecOSC = value;
                NotifyPropertyChanged(nameof(SecOSC));
            }
        }


        private int _SecOSCPort = 9002;

        public int SecOSCPort
        {
            get { return _SecOSCPort; }
            set
            {
                _SecOSCPort = value;
                NotifyPropertyChanged(nameof(SecOSCPort));
            }
        }


        private int _thirdOSCPort = 9003;
        public int ThirdOSCPort
        {
            get { return _thirdOSCPort; }
            set
            {
                _thirdOSCPort = value;
                NotifyPropertyChanged(nameof(ThirdOSCPort));
            }
        }


        private bool _thirdOSC = false;
        public bool ThirdOSC
        {
            get { return _thirdOSC; }
            set
            {
                _thirdOSC = value;
                NotifyPropertyChanged(nameof(ThirdOSC));
            }
        }


        private bool _UnmuteSecOutput = false;
        public bool UnmuteSecOutput
        {
            get { return _UnmuteSecOutput; }
            set
            {
                _UnmuteSecOutput = value;
                NotifyPropertyChanged(nameof(UnmuteSecOutput));
            }
        }

        private bool _UnmuteThirdOutput = false;

        public bool UnmuteThirdOutput
        {
            get { return _UnmuteThirdOutput; }
            set
            {
                _UnmuteThirdOutput = value;
                NotifyPropertyChanged(nameof(UnmuteThirdOutput));
            }
        }


        private bool _UseDaylightSavingTime = false;

        public bool UseDaylightSavingTime
        {
            get { return _UseDaylightSavingTime; }
            set
            {
                _UseDaylightSavingTime = value;
                NotifyPropertyChanged(nameof(UseDaylightSavingTime));
            }
        }

        private bool _Settings_WindowActivity = false;

        public bool Settings_WindowActivity
        {
            get { return _Settings_WindowActivity; }
            set
            {
                _Settings_WindowActivity = value;
                NotifyPropertyChanged(nameof(Settings_WindowActivity));
            }
        }



        private bool _Settings_MediaLink = false;

        public bool Settings_MediaLink
        {
            get { return _Settings_MediaLink; }
            set
            {
                _Settings_MediaLink = value;
                NotifyPropertyChanged(nameof(Settings_MediaLink));
            }
        }

        private bool _Settings_Chatting = false;

        public bool Settings_Chatting
        {
            get { return _Settings_Chatting; }
            set
            {
                _Settings_Chatting = value;
                NotifyPropertyChanged(nameof(Settings_Chatting));
            }
        }


        private bool _Settings_ComponentStats = false;
        public bool Settings_ComponentStats
        {
            get { return _Settings_ComponentStats; }
            set
            {
                _Settings_ComponentStats = value;
                NotifyPropertyChanged(nameof(Settings_ComponentStats));
            }
        }


        private bool _Settings_AppOptions = false;

        public bool Settings_AppOptions
        {
            get { return _Settings_AppOptions; }
            set
            {
                _Settings_AppOptions = value;
                NotifyPropertyChanged(nameof(Settings_AppOptions));
            }
        }

        private bool _Settings_TTS = false;

        public bool Settings_TTS
        {
            get { return _Settings_TTS; }
            set
            {
                _Settings_TTS = value;
                NotifyPropertyChanged(nameof(Settings_TTS));
            }
        }

        private bool _Settings_Time = false;

        public bool Settings_Time
        {
            get { return _Settings_Time; }
            set
            {
                _Settings_Time = value;
                NotifyPropertyChanged(nameof(Settings_Time));
            }
        }

        private bool _Settings_HeartRate = false;

        public bool Settings_HeartRate
        {
            get { return _Settings_HeartRate; }
            set
            {
                _Settings_HeartRate = value;
                NotifyPropertyChanged(nameof(Settings_HeartRate));
            }
        }

        private bool _Settings_Status = false;

        public bool Settings_Status
        {
            get { return _Settings_Status; }
            set
            {
                _Settings_Status = value;
                NotifyPropertyChanged(nameof(Settings_Status));
            }
        }

        private string _PulsoidAccessTokenOAuthEncrypted = string.Empty;
        private string _PulsoidAccessTokenOAuth;

        public string PulsoidAccessTokenOAuthEncrypted
        {
            get { return _PulsoidAccessTokenOAuthEncrypted; }
            set
            {
                if (_PulsoidAccessTokenOAuthEncrypted != value)
                {
                    _PulsoidAccessTokenOAuthEncrypted = value;
                    EncryptionMethods.TryProcessToken(ref _PulsoidAccessTokenOAuthEncrypted, ref _PulsoidAccessTokenOAuth, false);
                    NotifyPropertyChanged(nameof(PulsoidAccessTokenOAuthEncrypted));
                }
            }
        }

        public string PulsoidAccessTokenOAuth
        {
            get { return _PulsoidAccessTokenOAuth; }
            set
            {
                if (_PulsoidAccessTokenOAuth != value)
                {
                    _PulsoidAccessTokenOAuth = value;
                    EncryptionMethods.TryProcessToken(ref _PulsoidAccessTokenOAuth, ref _PulsoidAccessTokenOAuthEncrypted, true);
                    NotifyPropertyChanged(nameof(PulsoidAccessTokenOAuth));
                }
            }
        }

        private string _OpenAIAccessTokenEncrypted = string.Empty;
        private string _OpenAIAccessToken;

        public string OpenAIAccessTokenEncrypted
        {
            get { return _OpenAIAccessTokenEncrypted; }
            set
            {
                if (_OpenAIAccessTokenEncrypted != value)
                {
                    _OpenAIAccessTokenEncrypted = value;
                    EncryptionMethods.TryProcessToken(ref _OpenAIAccessTokenEncrypted, ref _OpenAIAccessToken, false);
                    NotifyPropertyChanged(nameof(OpenAIAccessTokenEncrypted));
                }
            }
        }

        public string OpenAIAccessToken
        {
            get { return _OpenAIAccessToken; }
            set
            {
                if (_OpenAIAccessToken != value)
                {
                    _OpenAIAccessToken = value;
                    EncryptionMethods.TryProcessToken(ref _OpenAIAccessToken, ref _OpenAIAccessTokenEncrypted, true);
                    NotifyPropertyChanged(nameof(OpenAIAccessToken));
                }
            }
        }

        private bool _OpenAIConnected = false;
        public bool OpenAIConnected
        {
            get { return _OpenAIConnected; }
            set
            {
                _OpenAIConnected = value;
                NotifyPropertyChanged(nameof(OpenAIConnected));
            }
        }


        private string _OpenAIAccessErrorTxt;
        public string OpenAIAccessErrorTxt
        {
            get { return _OpenAIAccessErrorTxt; }
            set
            {
                _OpenAIAccessErrorTxt = value;
                NotifyPropertyChanged(nameof(OpenAIAccessErrorTxt));
            }
        }


        private bool _OpenAIAccessError = false;
        public bool OpenAIAccessError
        {
            get { return _OpenAIAccessError; }
            set
            {
                _OpenAIAccessError = value;
                NotifyPropertyChanged(nameof(OpenAIAccessError));
            }
        }

        private string _OpenAIOrganizationIDEncrypted = string.Empty;
        private string _OpenAIOrganizationID;
        public string OpenAIOrganizationID
        {
            get { return _OpenAIOrganizationID; }
            set
            {
                if (_OpenAIOrganizationID != value)
                {
                    _OpenAIOrganizationID = value;
                    EncryptionMethods.TryProcessToken(ref _OpenAIOrganizationID, ref _OpenAIOrganizationIDEncrypted, true);
                    NotifyPropertyChanged(nameof(OpenAIOrganizationID));
                }
            }
        }


        private bool _Settings_OpenAI = false;
        public bool Settings_OpenAI
        {
            get { return _Settings_OpenAI; }
            set
            {
                _Settings_OpenAI = value;
                NotifyPropertyChanged(nameof(Settings_OpenAI));
            }
        }

        public string OpenAIOrganizationIDEncrypted
        {
            get { return _OpenAIOrganizationIDEncrypted; }
            set
            {
                if (_OpenAIOrganizationIDEncrypted != value)
                {
                    _OpenAIOrganizationIDEncrypted = value;
                    EncryptionMethods.TryProcessToken(ref _OpenAIOrganizationIDEncrypted, ref _OpenAIOrganizationID, false);
                    NotifyPropertyChanged(nameof(OpenAIOrganizationIDEncrypted));
                }
            }
        }

        private bool _timeShowTimeZone = false;

        public bool TimeShowTimeZone
        {
            get => _timeShowTimeZone;
            set
            {
                _timeShowTimeZone = value;
                NotifyPropertyChanged(nameof(TimeShowTimeZone));
            }
        }

        private Timezone _selectedTimeZone;

        public Timezone SelectedTimeZone
        {
            get => _selectedTimeZone;
            set
            {
                _selectedTimeZone = value;
                NotifyPropertyChanged(nameof(SelectedTimeZone));
            }
        }

        private string _lastUsedSortDirection;

        public string LastUsedSortDirection
        {
            get { return _lastUsedSortDirection; }
            set
            {
                _lastUsedSortDirection = value;
                NotifyPropertyChanged(nameof(LastUsedSortDirection));
            }
        }


        public ProcessInfo LastProcessFocused
        {
            get { return _LastProcessFocused; }
            set
            {
                _LastProcessFocused = value;
                NotifyPropertyChanged(nameof(LastProcessFocused));
            }
        }


        private string _DeletedAppslabel;

        public string DeletedAppslabel
        {
            get { return _DeletedAppslabel; }
            set
            {
                _DeletedAppslabel = value;
                NotifyPropertyChanged(nameof(DeletedAppslabel));
            }
        }
        private ObservableCollection<ProcessInfo> _scannedApps = new();
        public ObservableCollection<ProcessInfo> ScannedApps
        {
            get { return _scannedApps; }
            set
            {
                if (_scannedApps != value)
                {
                    _scannedApps.CollectionChanged -= ScannedApps_CollectionChanged;
                    _scannedApps = value;
                    _scannedApps.CollectionChanged += ScannedApps_CollectionChanged;
                }
            }
        }

        public bool MediaLinkTimeSeekStyleIsNumbersAndSeekBar => MediaLinkTimeSeekStyle == MediaLinkTimeSeekbar.NumbersAndSeekBar;

        public bool MediaLinkTimeSeekStyleIsNone => MediaLinkTimeSeekStyle == MediaLinkTimeSeekbar.None;


        private MediaLinkTimeSeekbar _MediaLinkTimeSeekStyle = MediaLinkTimeSeekbar.SmallNumbers;

        public MediaLinkTimeSeekbar MediaLinkTimeSeekStyle
        {
            get { return _MediaLinkTimeSeekStyle; }
            set
            {
                if (_MediaLinkTimeSeekStyle != value)
                {
                    _MediaLinkTimeSeekStyle = value;
                    NotifyPropertyChanged(nameof(MediaLinkTimeSeekStyleIsNumbersAndSeekBar));
                    NotifyPropertyChanged(nameof(MediaLinkTimeSeekStyleIsNone));
                    NotifyPropertyChanged(nameof(MediaLinkTimeSeekStyle));
                }
            }
        }




        public IEnumerable<MediaLinkTimeSeekbar> AvailableTimeSeekbarStyles { get; } = Enum.GetValues(typeof(MediaLinkTimeSeekbar)).Cast<MediaLinkTimeSeekbar>().ToList();


        private bool _ApplicationHookV2 = true;

        public bool ApplicationHookV2
        {
            get { return _ApplicationHookV2; }
            set
            {
                _ApplicationHookV2 = value;
                NotifyPropertyChanged(nameof(ApplicationHookV2));
            }
        }


        private bool _AppIsEnabled = true;

        public bool AppIsEnabled
        {
            get { return _AppIsEnabled; }
            set
            {
                _AppIsEnabled = value;
                NotifyPropertyChanged(nameof(AppIsEnabled));
            }
        }

        private double _AppOpacity = 0.98;

        public double AppOpacity
        {
            get { return _AppOpacity; }
            set
            {
                _AppOpacity = value;
                NotifyPropertyChanged(nameof(AppOpacity));
            }
        }

        public string ToggleVoiceText
        {
            get { return _toggleVoiceText; }
            set
            {
                _toggleVoiceText = value;
                NotifyPropertyChanged(nameof(ToggleVoiceText));
            }
        }

        public bool ToggleVoiceWithV
        {
            get { return _ToggleVoiceWithV; }
            set
            {
                _ToggleVoiceWithV = value;
                NotifyPropertyChanged(nameof(ToggleVoiceWithV));
                UpdateToggleVoiceText();
            }
        }

        public bool TTSBtnShadow
        {
            get { return _TTSBtnShadow; }
            set
            {
                _TTSBtnShadow = value;
                NotifyPropertyChanged(nameof(TTSBtnShadow));
                MainWindow.ShadowOpacity = value ? 1 : 0;
            }
        }

        public bool AutoUnmuteTTS
        {
            get { return _AutoUnmuteTTS; }
            set
            {
                _AutoUnmuteTTS = value;
                NotifyPropertyChanged(nameof(AutoUnmuteTTS));
            }
        }


        public float TTSVolume
        {
            get { return _TTSVolume; }
            set
            {
                _TTSVolume = value;
                NotifyPropertyChanged(nameof(TTSVolume));
            }
        }

        private string _tagURL;

        public string tagURL
        {
            get { return _tagURL; }
            set
            {
                _tagURL = value;
                NotifyPropertyChanged(nameof(tagURL));
            }
        }


        private string _UpdateStatustxt;

        public string UpdateStatustxt
        {
            get { return _UpdateStatustxt; }
            set
            {
                _UpdateStatustxt = value;
                NotifyPropertyChanged(nameof(UpdateStatustxt));
            }
        }

        private string _AppLocation;

        public string AppLocation
        {
            get { return _AppLocation; }
            set
            {
                _AppLocation = value;
                NotifyPropertyChanged(nameof(AppLocation));
            }
        }


        public bool CanUpdate
        {
            get { return _CanUpdate; }
            set
            {
                _CanUpdate = value;
                NotifyPropertyChanged(nameof(CanUpdate));
            }
        }


        private bool _CanUpdateLabel = false;

        public bool CanUpdateLabel
        {
            get { return _CanUpdateLabel; }
            set
            {
                _CanUpdateLabel = value;
                NotifyPropertyChanged(nameof(CanUpdateLabel));
            }
        }

        public bool VrcConnected
        {
            get { return _VrcConnected; }
            set
            {
                _VrcConnected = value;
                NotifyPropertyChanged(nameof(VrcConnected));
            }
        }


        private Models.Version _LatestReleaseVersion;

        public Models.Version LatestReleaseVersion
        {
            get { return _LatestReleaseVersion; }
            set
            {
                _LatestReleaseVersion = value;
                NotifyPropertyChanged(nameof(LatestReleaseVersion));
            }
        }


        private string _UpdateURL;

        public string UpdateURL
        {
            get { return _UpdateURL; }
            set
            {
                _UpdateURL = value;
                NotifyPropertyChanged(nameof(UpdateURL));
            }
        }

        private string _LatestReleaseURL;

        public string LatestReleaseURL
        {
            get { return _LatestReleaseURL; }
            set
            {
                _LatestReleaseURL = value;
                NotifyPropertyChanged(nameof(LatestReleaseURL));
            }
        }


        private Models.Version _PreReleaseVersion;

        public Models.Version PreReleaseVersion
        {
            get { return _PreReleaseVersion; }
            set
            {
                _PreReleaseVersion = value;
                NotifyPropertyChanged(nameof(PreReleaseVersion));
            }
        }


        private string _PreReleaseURL;

        public string PreReleaseURL
        {
            get { return _PreReleaseURL; }
            set
            {
                _PreReleaseURL = value;
                NotifyPropertyChanged(nameof(PreReleaseURL));
            }
        }

        public string LogPath
        {
            get { return _LogPath; }
            set
            {
                _LogPath = value;
                NotifyPropertyChanged(nameof(LogPath));
            }
        }

        public string RecentPlayBackOutput
        {
            get { return _RecentPlayBackOutput; }
            set
            {
                _RecentPlayBackOutput = value;
                NotifyPropertyChanged(nameof(RecentPlayBackOutput));
            }
        }

        public bool TTSCutOff
        {
            get { return _TTSCutOff; }
            set
            {
                _TTSCutOff = value;
                NotifyPropertyChanged(nameof(TTSCutOff));
            }
        }

        public List<AudioDevice> AuxOutputDevices
        {
            get { return _auxOutputDevices; }
            set
            {
                _auxOutputDevices = value;
                NotifyPropertyChanged(nameof(AuxOutputDevices));
            }
        }

        public List<AudioDevice> PlaybackOutputDevices
        {
            get { return _playbackOutputDevices; }
            set
            {
                _playbackOutputDevices = value;
                NotifyPropertyChanged(nameof(PlaybackOutputDevices));
            }
        }

        public AudioDevice SelectedAuxOutputDevice
        {
            get { return _selectedAuxOutputDevice; }
            set
            {
                _selectedAuxOutputDevice = value;
                NotifyPropertyChanged(nameof(SelectedAuxOutputDevice));
            }
        }

        public AudioDevice SelectedPlaybackOutputDevice
        {
            get { return _selectedPlaybackOutputDevice; }
            set
            {
                _selectedPlaybackOutputDevice = value;
                NotifyPropertyChanged(nameof(SelectedPlaybackOutputDevice));
            }
        }

        public bool TTSTikTokEnabled
        {
            get { return _TTSTikTokEnabled; }
            set
            {
                _TTSTikTokEnabled = value;
                NotifyPropertyChanged(nameof(TTSTikTokEnabled));
            }
        }

        private string _RecentTikTokTTSVoice = "en_au_001";

        public string RecentTikTokTTSVoice
        {
            get { return _RecentTikTokTTSVoice; }
            set
            {
                _RecentTikTokTTSVoice = value;
                NotifyPropertyChanged(nameof(RecentTikTokTTSVoice));
            }
        }

        public Voice SelectedTikTokTTSVoice
        {
            get { return _SelectedTikTokTTSVoice; }
            set
            {
                _SelectedTikTokTTSVoice = value;
                NotifyPropertyChanged(nameof(SelectedTikTokTTSVoice));
            }
        }

        public List<Voice> TikTokTTSVoices
        {
            get { return _TikTokTTSVoices; }
            set
            {
                _TikTokTTSVoices = value;
                NotifyPropertyChanged(nameof(TikTokTTSVoices));
            }
        }

        public string ApiStream
        {
            get { return _ApiStream; }
            set
            {
                _ApiStream = value;
                NotifyPropertyChanged(nameof(ApiStream));
            }
        }

        public ObservableCollection<ChatItem> LastMessages
        {
            get { return _LastMessages; }
            set
            {
                _LastMessages = value;
                NotifyPropertyChanged(nameof(LastMessages));
            }
        }

        public bool TypingIndicator
        {
            get { return _TypingIndicator; }
            set
            {
                _TypingIndicator = value;
                NotifyPropertyChanged(nameof(TypingIndicator));
            }
        }

        public bool Topmost
        {
            get { return _Topmost; }
            set
            {
                _Topmost = value;
                NotifyPropertyChanged(nameof(Topmost));
            }
        }

        public bool PauseIconMusic
        {
            get { return _PauseIconMusic; }
            set
            {
                _PauseIconMusic = value;
                NotifyPropertyChanged(nameof(PauseIconMusic));
            }
        }

        public bool ChatFX
        {
            get { return _ChatFX; }
            set
            {
                _ChatFX = value;
                NotifyPropertyChanged(nameof(ChatFX));
            }
        }

        public bool CountDownUI
        {
            get { return _CountDownUI; }
            set
            {
                _CountDownUI = value;
                NotifyPropertyChanged(nameof(CountDownUI));
            }
        }

        public bool PrefixChat
        {
            get { return _PrefixChat; }
            set
            {
                _PrefixChat = value;
                NotifyPropertyChanged(nameof(PrefixChat));
            }
        }

        public bool ScanPause
        {
            get { return _ScanPause; }
            set
            {
                _ScanPause = value;
                NotifyPropertyChanged(nameof(ScanPause));
            }
        }

        public int ScanPauseTimeout
        {
            get { return _ScanPauseTimeout; }
            set
            {
                _ScanPauseTimeout = value;
                NotifyPropertyChanged(nameof(ScanPauseTimeout));
            }
        }

        public int ScanPauseCountDown
        {
            get { return _ScanPauseCountDown; }
            set
            {
                _ScanPauseCountDown = value;
                NotifyPropertyChanged(nameof(ScanPauseCountDown));
            }
        }

        public string aesKey
        {
            get { return _aesKey; }
            set
            {
                _aesKey = value;
                NotifyPropertyChanged(nameof(aesKey));
            }
        }

        public string ChatTopBarTxt
        {
            get { return _ChatTopBarTxt; }
            set
            {
                _ChatTopBarTxt = value;
                NotifyPropertyChanged(nameof(ChatTopBarTxt));
            }
        }


        private bool _IntelliChatRequesting = false;
        public bool IntelliChatRequesting
        {
            get { return _IntelliChatRequesting; }
            set
            {
                _IntelliChatRequesting = value;
                NotifyPropertyChanged(nameof(IntelliChatRequesting));
            }
        }

        public string ChatFeedbackTxt
        {
            get { return _ChatFeedbackTxt; }
            set
            {
                _ChatFeedbackTxt = value;
                NotifyPropertyChanged(nameof(ChatFeedbackTxt));
            }
        }

        public string StatusTopBarTxt
        {
            get { return _StatusTopBarTxt; }
            set
            {
                _StatusTopBarTxt = value;
                NotifyPropertyChanged(nameof(StatusTopBarTxt));
            }
        }

        public string NewChattingTxt
        {
            get { return _NewChattingTxt; }
            set
            {
                _NewChattingTxt = value;
                NotifyPropertyChanged(nameof(NewChattingTxt));
            }
        }

        public string NewStatusItemTxt
        {
            get { return _NewStatusItemTxt; }
            set
            {
                _NewStatusItemTxt = value;
                NotifyPropertyChanged(nameof(NewStatusItemTxt));
            }
        }

        public string ChatBoxCount
        {
            get { return _ChatBoxCount; }
            set
            {
                _ChatBoxCount = value;
                NotifyPropertyChanged(nameof(ChatBoxCount));
            }
        }

        public string StatusBoxCount
        {
            get { return _StatusBoxCount; }
            set
            {
                _StatusBoxCount = value;
                NotifyPropertyChanged(nameof(StatusBoxCount));
            }
        }

        public string ChatBoxColor
        {
            get { return _ChatBoxColor; }
            set
            {
                _ChatBoxColor = value;
                NotifyPropertyChanged(nameof(ChatBoxColor));
            }
        }

        public string StatusBoxColor
        {
            get { return _StatusBoxColor; }
            set
            {
                _StatusBoxColor = value;
                NotifyPropertyChanged(nameof(StatusBoxColor));
            }
        }

        public bool PrefixIconStatus
        {
            get { return _PrefixIconStatus; }
            set
            {
                _PrefixIconStatus = value;
                NotifyPropertyChanged(nameof(PrefixIconStatus));
            }
        }

        public bool PrefixIconMusic
        {
            get { return _PrefixIconMusic; }
            set
            {
                _PrefixIconMusic = value;
                NotifyPropertyChanged(nameof(PrefixIconMusic));
            }
        }

        public ObservableCollection<StatusItem> StatusList
        {
            get { return _StatusList; }
            set
            {
                _StatusList = value;
                NotifyPropertyChanged(nameof(StatusList));
            }
        }

        public string MenuItem_3_Visibility
        {
            get { return _MenuItem_3_Visibility; }
            set
            {
                _MenuItem_3_Visibility = value;
                NotifyPropertyChanged(nameof(MenuItem_3_Visibility));
            }
        }

        public string MenuItem_2_Visibility
        {
            get { return _MenuItem_2_Visibility; }
            set
            {
                _MenuItem_2_Visibility = value;
                NotifyPropertyChanged(nameof(MenuItem_2_Visibility));
            }
        }

        public string MenuItem_1_Visibility
        {
            get { return _MenuItem_1_Visibility; }
            set
            {
                _MenuItem_1_Visibility = value;
                NotifyPropertyChanged(nameof(MenuItem_1_Visibility));
            }
        }

        public string MenuItem_0_Visibility
        {
            get { return _MenuItem_0_Visibility; }
            set
            {
                _MenuItem_0_Visibility = value;
                NotifyPropertyChanged(nameof(MenuItem_0_Visibility));
            }
        }

        public int CurrentMenuItem
        {
            get { return _CurrentMenuItem; }
            set
            {
                _CurrentMenuItem = value;
                NotifyPropertyChanged(nameof(CurrentMenuItem));
            }
        }

        public bool Time24H
        {
            get { return _Time24H; }
            set
            {
                _Time24H = value;
                NotifyPropertyChanged(nameof(Time24H));
            }
        }

        public bool PrefixTime
        {
            get { return _PrefixTime; }
            set
            {
                _PrefixTime = value;
                NotifyPropertyChanged(nameof(PrefixTime));
            }
        }

        public string Spotify_Opacity
        {
            get { return _Spotify_Opacity; }
            set
            {
                _Spotify_Opacity = value;
                NotifyPropertyChanged(nameof(Spotify_Opacity));
            }
        }

        public string HeartRate_Opacity
        {
            get { return _HeartRate_Opacity; }
            set
            {
                _HeartRate_Opacity = value;
                NotifyPropertyChanged(nameof(HeartRate_Opacity));
            }
        }


        private bool _IntgrHeartRate = false;

        public bool IntgrHeartRate
        {
            get { return _IntgrHeartRate; }
            set
            {
                _IntgrHeartRate = value;
                NotifyPropertyChanged(nameof(IntgrHeartRate));
            }
        }

        public string Status_Opacity
        {
            get { return _Status_Opacity; }
            set
            {
                _Status_Opacity = value;
                NotifyPropertyChanged(nameof(Status_Opacity));
            }
        }

        public string Time_Opacity
        {
            get { return _Time_Opacity; }
            set
            {
                _Time_Opacity = value;
                NotifyPropertyChanged(nameof(Time_Opacity));
            }
        }

        public string Window_Opacity
        {
            get { return _Window_Opacity; }
            set
            {
                _Window_Opacity = value;
                NotifyPropertyChanged(nameof(Window_Opacity));
            }
        }

        public bool IntgrStatus
        {
            get { return _IntgrStatus; }
            set
            {
                _IntgrStatus = value;
                NotifyPropertyChanged(nameof(IntgrStatus));
            }
        }

        public bool MasterSwitch
        {
            get { return _MasterSwitch; }
            set
            {
                _MasterSwitch = value;
                NotifyPropertyChanged(nameof(MasterSwitch));
            }
        }

        public string Char_Limit
        {
            get { return _Char_Limit; }
            set
            {
                _Char_Limit = value;
                NotifyPropertyChanged(nameof(Char_Limit));
            }
        }

        public string DataPath
        {
            get { return _DataPath; }
            set
            {
                _DataPath = value;
                NotifyPropertyChanged(nameof(DataPath));
            }
        }

        public string OSCmsg_countUI
        {
            get { return _OSCmsg_countUI; }
            set
            {
                _OSCmsg_countUI = value;
                NotifyPropertyChanged(nameof(OSCmsg_countUI));
            }
        }

        public int OSCmsg_count
        {
            get { return _OSCmsg_count; }
            set
            {
                _OSCmsg_count = value;
                NotifyPropertyChanged(nameof(OSCmsg_count));
            }
        }

        public bool IntgrScanWindowTime
        {
            get { return _IntgrScanWindowTime; }
            set
            {
                _IntgrScanWindowTime = value;
                NotifyPropertyChanged(nameof(IntgrScanWindowTime));
            }
        }

        public string OSCIP
        {
            get { return _OSCIP; }
            set
            {
                _OSCIP = value;
                NotifyPropertyChanged(nameof(OSCIP));
            }
        }

        public bool IntgrScanWindowActivity
        {
            get { return _IntgrScanWindowActivity; }
            set
            {
                _IntgrScanWindowActivity = value;
                NotifyPropertyChanged(nameof(IntgrScanWindowActivity));
            }
        }

        public int OSCPortOut
        {
            get { return _OSCPortOut; }
            set
            {
                _OSCPortOut = value;
                NotifyPropertyChanged(nameof(OSCPortOut));
            }
        }

        public bool IntgrScanSpotify_OLD
        {
            get { return _IntgrScanSpotify; }
            set
            {
                _IntgrScanSpotify = value;
                if (_IntgrScanSpotify)
                {
                    IntgrScanMediaLink = false;
                }
                NotifyPropertyChanged(nameof(IntgrScanSpotify_OLD));
            }
        }


        private double _ScanningInterval = 1.6;

        public double ScanningInterval
        {
            get { return _ScanningInterval; }
            set
            {
                if (value < 1.6)
                {
                    _ScanningInterval = 1.6;
                }
                else if (value > 10)
                {
                    _ScanningInterval = 10;
                }
                else
                {
                    _ScanningInterval = Math.Round(value, 1);
                }
                NotifyPropertyChanged(nameof(ScanningInterval));
            }
        }


        public string CurrentTime
        {
            get { return _CurrentTime; }
            set
            {
                _CurrentTime = value;
                NotifyPropertyChanged(nameof(CurrentTime));
            }
        }


        private bool _UseSystemCulture = false;

        public bool UseSystemCulture
        {
            get { return _UseSystemCulture; }
            set
            {
                if (_UseSystemCulture != value)
                {
                    _UseSystemCulture = value;
                    NotifyPropertyChanged(nameof(UseSystemCulture));
                }
            }
        }


        public string VersionTxt
        {
            get { return _VersionTxt; }
            set
            {
                _VersionTxt = value;
                NotifyPropertyChanged(nameof(VersionTxt));
            }
        }

        public string VersionTxtColor
        {
            get { return _VersionTxtColor; }
            set
            {
                _VersionTxtColor = value;
                NotifyPropertyChanged(nameof(VersionTxtColor));
            }
        }

        public Models.Version AppVersion
        {
            get { return _AppVersion; }
            set
            {
                _AppVersion = value;
                NotifyPropertyChanged(nameof(AppVersion));
            }
        }

        public Models.Version GitHubVersion
        {
            get { return _GitHubVersion; }
            set
            {
                _GitHubVersion = value;
                NotifyPropertyChanged(nameof(GitHubVersion));
            }
        }


        private string _NetworkStats_Opacity = "1";

        public string NetworkStats_Opacity
        {
            get { return _NetworkStats_Opacity; }
            set
            {
                if (_NetworkStats_Opacity != value)
                {
                    _NetworkStats_Opacity = value;
                    NotifyPropertyChanged(nameof(NetworkStats_Opacity));
                }
            }
        }


        private bool _IntgrNetworkStatistics = false;

        public bool IntgrNetworkStatistics
        {
            get { return _IntgrNetworkStatistics; }
            set
            {
                if (_IntgrNetworkStatistics != value)
                {
                    _IntgrNetworkStatistics = value;
                    NotifyPropertyChanged(nameof(IntgrNetworkStatistics));
                }
            }
        }


        private bool _IntgrNetworkStatistics_VR = false;

        public bool IntgrNetworkStatistics_VR
        {
            get { return _IntgrNetworkStatistics_VR; }
            set
            {
                if (_IntgrNetworkStatistics_VR != value)
                {
                    _IntgrNetworkStatistics_VR = value;
                    NotifyPropertyChanged(nameof(IntgrNetworkStatistics_VR));
                }
            }
        }


        private bool _IntgrNetworkStatistics_DESKTOP = true;

        public bool IntgrNetworkStatistics_DESKTOP
        {
            get { return _IntgrNetworkStatistics_DESKTOP; }
            set
            {
                if (_IntgrNetworkStatistics_DESKTOP != value)
                {
                    _IntgrNetworkStatistics_DESKTOP = value;
                    NotifyPropertyChanged(nameof(IntgrNetworkStatistics_DESKTOP));
                }
            }
        }


        private bool _NetworkStats_ShowCurrentDown = true;

        public bool NetworkStats_ShowCurrentDown
        {
            get { return _NetworkStats_ShowCurrentDown; }
            set
            {
                if (_NetworkStats_ShowCurrentDown != value)
                {
                    _NetworkStats_ShowCurrentDown = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowCurrentDown));
                }
            }
        }

        private bool _NetworkStats_UseInterfaceMaxSpeed = false;

        public bool NetworkStats_UseInterfaceMaxSpeed
        {
            get { return _NetworkStats_UseInterfaceMaxSpeed; }
            set
            {
                if (_NetworkStats_UseInterfaceMaxSpeed != value)
                {
                    _NetworkStats_UseInterfaceMaxSpeed = value;
                    NotifyPropertyChanged(nameof(NetworkStats_UseInterfaceMaxSpeed));
                }
            }
        }


        private bool _NetworkStats_ShowCurrentUp = false;

        public bool NetworkStats_ShowCurrentUp
        {
            get { return _NetworkStats_ShowCurrentUp; }
            set
            {
                if (_NetworkStats_ShowCurrentUp != value)
                {
                    _NetworkStats_ShowCurrentUp = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowCurrentUp));
                }
            }
        }

        private bool _NetworkStats_ShowMaxUp = false;
        public bool NetworkStats_ShowMaxUp
        {
            get { return _NetworkStats_ShowMaxUp; }
            set
            {
                if (_NetworkStats_ShowMaxUp != value)
                {
                    _NetworkStats_ShowMaxUp = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowMaxUp));
                }
            }
        }

        private bool _NetworkStats_ShowMaxDown = false;
        public bool NetworkStats_ShowMaxDown
        {
            get { return _NetworkStats_ShowMaxDown; }
            set
            {
                if (_NetworkStats_ShowMaxDown != value)
                {
                    _NetworkStats_ShowMaxDown = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowMaxDown));
                }
            }
        }

        private bool _NetworkStats_ShowTotalUp = false;
        public bool NetworkStats_ShowTotalUp
        {
            get { return _NetworkStats_ShowTotalUp; }
            set
            {
                if (_NetworkStats_ShowTotalUp != value)
                {
                    _NetworkStats_ShowTotalUp = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowTotalUp));
                }
            }
        }

        private bool _NetworkStats_ShowTotalDown = false;
        public bool NetworkStats_ShowTotalDown
        {
            get { return _NetworkStats_ShowTotalDown; }
            set
            {
                if (_NetworkStats_ShowTotalDown != value)
                {
                    _NetworkStats_ShowTotalDown = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowTotalDown));
                }
            }
        }

        private bool _NetworkStats_ShowNetworkUtilization = true;
        public bool NetworkStats_ShowNetworkUtilization
        {
            get { return _NetworkStats_ShowNetworkUtilization; }
            set
            {
                if (_NetworkStats_ShowNetworkUtilization != value)
                {
                    _NetworkStats_ShowNetworkUtilization = value;
                    NotifyPropertyChanged(nameof(NetworkStats_ShowNetworkUtilization));
                }
            }
        }


        private bool _NetworkStats_StyledCharacters = true;

        public bool NetworkStats_StyledCharacters
        {
            get { return _NetworkStats_StyledCharacters; }
            set
            {
                if (_NetworkStats_StyledCharacters != value)
                {
                    _NetworkStats_StyledCharacters = value;
                    NotifyPropertyChanged(nameof(NetworkStats_StyledCharacters));
                }
            }
        }


        private bool _PrefixIconSoundpad = true;
        public bool PrefixIconSoundpad
        {
            get { return _PrefixIconSoundpad; }
            set
            {
                _PrefixIconSoundpad = value;
                NotifyPropertyChanged(nameof(PrefixIconSoundpad));
            }
        }


        private bool _IntgrSoundpad_DESKTOP = true;
        public bool IntgrSoundpad_DESKTOP
        {
            get { return _IntgrSoundpad_DESKTOP; }
            set
            {
                _IntgrSoundpad_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrSoundpad_DESKTOP));
            }
        }

        private bool _IntgrSoundpad_VR = false;
        public bool IntgrSoundpad_VR
        {
            get { return _IntgrSoundpad_VR; }
            set
            {
                _IntgrSoundpad_VR = value;
                NotifyPropertyChanged(nameof(IntgrSoundpad_VR));
            }
        }

        public string Soundpad_Opacity
        {
            get { return _Soundpad_Opacity; }
            set
            {
                _Soundpad_Opacity = value;
                NotifyPropertyChanged(nameof(Soundpad_Opacity));
            }
        }

        private bool _IntgrSoundpad = false;
        public bool IntgrSoundpad
        {
            get { return _IntgrSoundpad; }
            set
            {
                _IntgrSoundpad = value;
                NotifyPropertyChanged(nameof(IntgrSoundpad));
            }
        }

        public bool IsVRRunning
        {
            get { return _IsVRRunning; }
            set
            {
                _IsVRRunning = value;
                NotifyPropertyChanged(nameof(IsVRRunning));
            }
        }


        public string OSCtoSent
        {
            get { return _OSCtoSent; }
            set
            {
                _OSCtoSent = value;
                NotifyPropertyChanged(nameof(OSCtoSent));
            }
        }

        public string FocusedWindow
        {
            get { return _FocusedWindow; }
            set
            {
                _FocusedWindow = value;
                NotifyPropertyChanged(nameof(FocusedWindow));
            }
        }

        public string PlayingSongTitle
        {
            get { return _PlayingSongTitle; }
            set
            {
                _PlayingSongTitle = value;
                NotifyPropertyChanged(nameof(PlayingSongTitle));
            }
        }

        public bool SpotifyActive
        {
            get { return _SpotifyActive; }
            set
            {
                _SpotifyActive = value;
                NotifyPropertyChanged(nameof(SpotifyActive));
            }
        }

        public bool SpotifyPaused
        {
            get { return _SpotifyPaused; }
            set
            {
                _SpotifyPaused = value;
                NotifyPropertyChanged(nameof(SpotifyPaused));
            }
        }

        private ObservableCollection<string> _emojiCollection = new ObservableCollection<string>
    {
        "💬", "🗨️", "💭", "🗯️"
    };


        public ObservableCollection<string> EmojiCollection
        {
            get => _emojiCollection;
            set
            {
                if (_emojiCollection != value)
                {
                    _emojiCollection = value;
                    NotifyPropertyChanged(nameof(EmojiCollection));
                }
            }
        }




        private string _emojiListString;
        public string EmojiListString
        {
            get => string.Join(",", EmojiCollection);
            set
            {
                if (_emojiListString != value)
                {
                    _emojiListString = value;
                    ParseEmojiListString(value);
                    NotifyPropertyChanged(nameof(EmojiListString));
                }
            }
        }

        private void ParseEmojiListString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                EmojiCollection.Clear();
            }
            else
            {
                var emojis = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(e => e.Trim())
                                  .Where(e => !string.IsNullOrWhiteSpace(e));

                // Clear existing collection and add new emojis
                EmojiCollection.Clear();
                foreach (var emoji in emojis)
                {
                    EmojiCollection.Add(emoji);
                }
            }

            // Notify that EmojiCollection has changed
            NotifyPropertyChanged(nameof(EmojiCollection));
        }

        public bool AddEmoji(string emoji)
        {
            if (string.IsNullOrWhiteSpace(emoji))
                return false;
            if (!EmojiCollection.Contains(emoji))
            {
                EmojiCollection.Add(emoji);
                NotifyPropertyChanged(nameof(EmojiCollection));
                NotifyPropertyChanged(nameof(EmojiListString));
            }
            return true;
        }

        private Queue<string> _shuffledEmojis;

        private readonly Random _random = new Random();

        private string _currentEmoji;
        public string CurrentEmoji
        {
            get => _currentEmoji;
            private set
            {
                if (_currentEmoji != value)
                {
                    _currentEmoji = value;
                    NotifyPropertyChanged(nameof(CurrentEmoji));
                }
            }
        }

        private void ShuffleEmojis()
        {
            var shuffledList = EmojiCollection.OrderBy(e => _random.Next()).ToList();
            _shuffledEmojis = new Queue<string>(shuffledList);
        }


        private bool _HideOpenAITools = false;
        public bool HideOpenAITools
        {
            get { return _HideOpenAITools; }
            set
            {
                _HideOpenAITools = value;
                NotifyPropertyChanged(nameof(HideOpenAITools));
            }
        }

        public string GetNextEmoji(bool IsChat = false)
        {
            string defaulticon = "💬";

            // Check if _emojiCollection is empty
            if (EmojiCollection == null || !EmojiCollection.Any())
            {
                CurrentEmoji = defaulticon;
                return defaulticon;
            }

            if (EnableEmojiShuffle && (IsChat ? EnableEmojiShuffleInChats : true))
            {
                // Initialize the shuffled queue if it's null
                if (_shuffledEmojis == null)
                {
                    ShuffleEmojis();
                }

                // If the shuffled queue is empty, reshuffle
                if (_shuffledEmojis.Count == 0)
                {
                    ShuffleEmojis();
                }

                // Ensure there are emojis to dequeue after shuffling
                if (_shuffledEmojis.Count > 0)
                {
                    CurrentEmoji = _shuffledEmojis.Dequeue();
                    return CurrentEmoji;
                }
            }

            // If shuffling is disabled or no emojis are available after shuffling, return the default icon
            CurrentEmoji = defaulticon;
            return defaulticon;
        }

        private bool _EnableEmojiShuffle = true;
        private bool _EnableEmojiShuffleInChats = false;



        public bool EnableEmojiShuffle
        {
            get { return _EnableEmojiShuffle; }
            set
            {
                _EnableEmojiShuffle = value;
                NotifyPropertyChanged(nameof(EnableEmojiShuffle));
            }
        }

        public bool EnableEmojiShuffleInChats
        {
            get { return _EnableEmojiShuffleInChats; }
            set
            {
                _EnableEmojiShuffleInChats = value;
                NotifyPropertyChanged(nameof(EnableEmojiShuffleInChats));
            }
        }

        private int _profileNumber;
        public int ProfileNumber
        {
            get => _profileNumber;
            set
            {
                _profileNumber = value;
                NotifyPropertyChanged(nameof(ProfileNumber));
            }
        }

        private bool _useCustomProfile;
        public bool UseCustomProfile
        {
            get => _useCustomProfile;
            set
            {
                _useCustomProfile = value;
                NotifyPropertyChanged(nameof(UseCustomProfile));
            }
        }

        #endregion

        #region PropChangedEvent
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }

        private void NotifyPropertiesChanged(string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                NotifyPropertyChanged(propertyName);
            }
        }
        #endregion
    }
}
