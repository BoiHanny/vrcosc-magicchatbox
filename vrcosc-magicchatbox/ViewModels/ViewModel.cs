using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ViewModel : INotifyPropertyChanged
    {
        public static readonly ViewModel Instance = new ViewModel();

        public readonly StatsManager _statsManager = new StatsManager();

        private readonly object _lock = new object();

        private ObservableCollection<ComponentStatsItem> _componentStatsListPrivate = new ObservableCollection<ComponentStatsItem>();
        public ReadOnlyObservableCollection<ComponentStatsItem> ComponentStatsList => new ReadOnlyObservableCollection<ComponentStatsItem>(_componentStatsListPrivate);

        public void UpdateComponentStatsList(ObservableCollection<ComponentStatsItem> newList)
        {
            _componentStatsListPrivate.Clear();
            foreach (var item in newList)
            {
                _componentStatsListPrivate.Add(item);
            }
        }


        private bool _BlankEgg = false;


        private bool _ChatAddSmallDelay = true;


        private double _ChatAddSmallDelayTIME = 1.4;

        private bool _ChatLiveEdit = true;

        private bool _ChatSendAgainFX = true;


        private double _ChattingUpdateRate = 3;


        private bool _DisableMediaLink = false;

        private bool _Egg_Dev = false;

        private HeartRateConnector _heartRateConnector;


        private bool _HeartRateTitle = false;

        private bool _IntgrCurrentTime_DESKTOP = false;

        private bool _IntgrCurrentTime_VR = true;


        private bool _IntgrHeartRate_DESKTOP = false;

        private bool _IntgrHeartRate_VR = true;

        private bool _IntgrMediaLink_DESKTOP = true;


        private bool _IntgrMediaLink_VR = true;


        private bool _IntgrScanMediaLink = true;


        private bool _IntgrSpotifyStatus_DESKTOP = true;

        private bool _IntgrSpotifyStatus_VR = true;

        private bool _IntgrStatus_DESKTOP = true;

        private bool _IntgrStatus_VR = true;

        private bool _IntgrWindowActivity_DESKTOP = true;

        private bool _IntgrWindowActivity_VR = false;


        private bool _JoinedAlphaChannel = true;


        private bool _KeepUpdatingChat = true;


        private bool _MediaSession_AutoSwitch = true;


        private bool _IntgrComponentStats_VR = true;
        public bool IntgrComponentStats_VR
        {
            get { return _IntgrComponentStats_VR; }
            set
            {
                _IntgrComponentStats_VR = value;
                NotifyPropertyChanged(nameof(IntgrComponentStats_VR));
            }
        }

        


        private bool _IntgrComponentStats_DESKTOP = false;
        public bool IntgrComponentStats_DESKTOP
        {
            get { return _IntgrComponentStats_DESKTOP; }
            set
            {
                _IntgrComponentStats_DESKTOP = value;
                NotifyPropertyChanged(nameof(IntgrComponentStats_DESKTOP));
            }
        }

        private bool _IntgrComponentStats = false;
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

        private bool _MediaSession_AutoSwitchSpawn = true;


        private int _MediaSession_Timeout = 3;

        private ObservableCollection<MediaSessionInfo> _MediaSessions = new ObservableCollection<MediaSessionInfo>();


        private bool _RealTimeChatEdit = true;

        private List<MediaSessionSettings> _SavedSessionSettings = new List<MediaSessionSettings>();

        private bool _Settings_Dev = false;


        private bool _TTSOnResendChat = false;
        public Dictionary<string, Action<bool>> SettingsMap;

        public bool IsCPUEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.CPU);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStat(StatsComponentType.CPU);
                }
                else
                {
                    _statsManager.DeactivateStat(StatsComponentType.CPU);
                }
                NotifyPropertyChanged(nameof(IsCPUEnabled));
            }
        }

        public bool IsGPUEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.GPU);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStat(StatsComponentType.GPU);
                }
                else
                {
                    _statsManager.DeactivateStat(StatsComponentType.GPU);
                }
                NotifyPropertyChanged(nameof(IsGPUEnabled));
            }
        }

        public bool IsRAMEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.RAM);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStat(StatsComponentType.RAM);
                }
                else
                {
                    _statsManager.DeactivateStat(StatsComponentType.RAM);
                }
                NotifyPropertyChanged(nameof(IsRAMEnabled));
            }
        }

        public bool IsVRAMEnabled
        {
            get => _statsManager.IsStatEnabled(StatsComponentType.VRAM);
            set
            {
                if (value)
                {
                    _statsManager.ActivateStat(StatsComponentType.VRAM);
                }
                else
                {
                    _statsManager.DeactivateStat(StatsComponentType.VRAM);
                }
                NotifyPropertyChanged(nameof(IsVRAMEnabled));
            }
        }




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
                { Timezone.EST, "Eastern Standard Time (EST)" },
                { Timezone.CST, "Central Standard Time (CST)" },
                { Timezone.PST, "Pacific Standard Time (PST)" },
                { Timezone.CET, "European Central Time (CET)" },
                { Timezone.AEST, "Australian Eastern Standard Time (AEST)" },
            };

            SettingsMap = new Dictionary<string, Action<bool>>
            {
                { nameof(Settings_WindowActivity), value => Settings_WindowActivity = value },
                { nameof(Settings_MediaLink), value => Settings_MediaLink = value },
                { nameof(Settings_Chatting), value => Settings_Chatting = value },
                { nameof(Settings_AppOptions), value => Settings_AppOptions = value },
                { nameof(Settings_TTS), value => Settings_TTS = value },
                { nameof(Settings_Time), value => Settings_Time = value },
                { nameof(Settings_HeartRate), value => Settings_HeartRate = value },
                { nameof(Settings_Status), value => Settings_Status = value }
            };

            _heartRateConnector = new HeartRateConnector();
            
            PropertyChanged += _heartRateConnector.PropertyChangedHandler;
            _dynamicOSCData = new ExpandoObject();
        }

        private void ToggleVoice()
        {
            if(Instance.ToggleVoiceWithV)
                OSCSender.ToggleVoice(true);
        }

        private void UpdateToggleVoiceText()
        { ToggleVoiceText = ToggleVoiceWithV ? "Toggle voice (V)" : "Toggle voice"; }

        public void ActivateSetting(string settingName)
        {
            if(SettingsMap.ContainsKey(settingName))
            {
                foreach(var setting in SettingsMap)
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
                foreach(var i in ViewModel.Instance.StatusList)
                {
                    if(i == item)
                    {
                        i.IsActive = true;
                        i.LastUsed = DateTime.Now;
                    } else
                    {
                        i.IsActive = false;
                    }
                }
                SaveStatusList();
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
            }
        }

        public static bool CreateIfMissing(string path)
        {
            try
            {
                if(!Directory.Exists(path))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    return true;
                }
                return true;
            } catch(IOException ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }
        }

        public static void SaveStatusList()
        {
            try
            {
                if(CreateIfMissing(ViewModel.Instance.DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(ViewModel.Instance.StatusList);
                    File.WriteAllText(Path.Combine(ViewModel.Instance.DataPath, "StatusList.xml"), json);
                }
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        public void ScannedAppsItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "FocusCount")
            {
                Application.Current.Dispatcher
                    .Invoke(
                        () =>
                        {
                            CollectionViewSource.GetDefaultView(ScannedApps).Refresh();
                        });
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

        private void ProcessInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Resort();
        }

        public void SortScannedApps(SortProperty sortProperty)
        {
            if (!_sortDirection.ContainsKey(sortProperty))
            {
                Logging.WriteException(new Exception($"No sortDirection: {sortProperty}"), makeVMDump: false, MSGBox: false);
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }



        private void Resort()
        {
            if (_currentSortProperty != default)
            {
                UpdateSortedApps();
            }
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




        public bool BlankEgg
        {
            get { return _BlankEgg; }
            set
            {
                _BlankEgg = value;
                NotifyPropertyChanged(nameof(BlankEgg));
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
                if(value < 0.1)
                {
                    _ChatAddSmallDelayTIME = 0.1;
                } else if(value > 2)
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
                if(value < 2)
                {
                    _ChattingUpdateRate = 2;
                } else if(value > 10)
                {
                    _ChattingUpdateRate = 10;
                }

                _ChattingUpdateRate = Math.Round(value, 1);
                NotifyPropertyChanged(nameof(ChattingUpdateRate));
            }
        }

        public bool DisableMediaLink
        {
            get { return _DisableMediaLink; }
            set
            {
                _DisableMediaLink = value;
                if(_DisableMediaLink)
                {
                    IntgrScanMediaLink = false;
                } else
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

        public bool HeartRateTitle
        {
            get { return _HeartRateTitle; }
            set
            {
                _HeartRateTitle = value;
                NotifyPropertyChanged(nameof(HeartRateTitle));
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

        public bool IntgrScanMediaLink
        {
            get { return _IntgrScanMediaLink; }
            set
            {
                _IntgrScanMediaLink = value;
                if(_IntgrScanMediaLink)
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
                if(!value)
                {
                    ViewModel.Instance.ChatLiveEdit = false;
                }
                NotifyPropertyChanged(nameof(KeepUpdatingChat));
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

        public bool RealTimeChatEdit
        {
            get { return _RealTimeChatEdit; }
            set
            {
                _RealTimeChatEdit = value;
                NotifyPropertyChanged(nameof(RealTimeChatEdit));
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

        public bool Settings_Dev
        {
            get { return _Settings_Dev; }
            set
            {
                _Settings_Dev = value;
                NotifyPropertyChanged(nameof(Settings_Dev));
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


        private bool _IntgrScanForce = true;
        public bool IntgrScanForce
        {
            get { return _IntgrScanForce; }
            set
            {
                _IntgrScanForce = value;
                NotifyPropertyChanged(nameof(IntgrScanForce));
            }
        }

        private ObservableCollection<OSCAvatar> _OSCAvatarDatabase = new ObservableCollection<OSCAvatar>();
        public ObservableCollection<OSCAvatar> OSCAvatarDatabase
        {
            get { return _OSCAvatarDatabase; }
            set
            {
                _OSCAvatarDatabase = value;
                NotifyPropertyChanged(nameof(OSCAvatarDatabase));
            }
        }

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
        private string _ApiStream = "b2t8DhYcLcu7Nu0suPcvc8lO27wztrjMPbb + 8hQ1WPba2dq / iRyYpBEDZ0NuMNKR5GRrF2XdfANLud0zihG / UD + ewVl1p3VLNk1mrNdrdg88rguzi6RJ7T1AA7hyBY + F";
        private Version _AppVersion = new("0.8.494");
        private Version _GitHubVersion;
        private string _VersionTxt = "Check for updates";
        private string _VersionTxtColor = "#FF8F80B9";
        private string _StatusBoxCount = "0/140";
        private string _StatusBoxColor = "#FF504767";
        private string _ChatBoxCount = "0/140";
        private string _ChatBoxColor = "#FF504767";
        private string _CurrentTime = string.Empty;
        private string _ActiveChatTxt = string.Empty;
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

        public void ToggleComponentEnabledStatus(StatsComponentType type)
        {
            _statsManager.ToggleStatEnabledStatus(type);
        }

        public void SetComponentStatMaxValue(StatsComponentType type, string maxValue)
        {
            _statsManager.SetStatMaxValue(type, maxValue);
        }

        public string GetComponentStatMaxValue(StatsComponentType type)
        {
            return _statsManager.GetStatMaxValue(type);
        }



        private bool _StatsComponent_CPU = true;
        public bool StatsComponent_CPU
        {
            get { return _StatsComponent_CPU; }
            set
            {
                _StatsComponent_CPU = value;
                NotifyPropertyChanged(nameof(StatsComponent_CPU));
            }
        }

        private bool _StatsComponent_GPU = true;
        public bool StatsComponent_GPU
        {
            get { return _StatsComponent_GPU; }
            set
            {
                _StatsComponent_GPU = value;
                NotifyPropertyChanged(nameof(StatsComponent_GPU));
            }
        }

        private bool _StatsComponent_FPS = true;
        public bool StatsComponent_FPS
        {
            get { return _StatsComponent_FPS; }
            set
            {
                _StatsComponent_FPS = value;
                NotifyPropertyChanged(nameof(StatsComponent_FPS));
            }
        }




        private bool _StatsComponent_VRAM = true;
        public bool StatsComponent_VRAM
        {
            get { return _StatsComponent_VRAM; }
            set
            {
                _StatsComponent_VRAM = value;
                NotifyPropertyChanged(nameof(StatsComponent_VRAM));
            }
        }


        private bool _StatsComponent_RAM = true;
        public bool StatsComponent_RAM
        {
            get { return _StatsComponent_RAM; }
            set
            {
                _StatsComponent_RAM = value;
                NotifyPropertyChanged(nameof(StatsComponent_RAM));
            }
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

        public enum SortProperty
        {
            ProcessName,
            UsedNewMethod,
            ApplyCustomAppName,
            IsPrivateApp,
            FocusCount,
            ShowInfo
        }



        private Dictionary<string, OSCParameter> _builtInOSCData = new Dictionary<string, OSCParameter>();
        private ExpandoObject _dynamicOSCData = new ExpandoObject();

        public Dictionary<string, OSCParameter> BuiltInOSCData
        {
            get { return _builtInOSCData; }
            set
            {
                _builtInOSCData = value;
                NotifyPropertyChanged(nameof(BuiltInOSCData));
            }
        }

        public dynamic DynamicOSCData
        {
            get { return _dynamicOSCData; }
            set
            {
                _dynamicOSCData = value;
                NotifyPropertyChanged(nameof(DynamicOSCData));
            }
        }

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


        private int _HeartRateScanInterval_v1 = 1;

        public int HeartRateScanInterval_v1
        {
            get { return _HeartRateScanInterval_v1; }
            set
            {
                _HeartRateScanInterval_v1 = value;
                NotifyPropertyChanged(nameof(HeartRateScanInterval_v1));
            }
        }

        private DateTime _HeartRateLastUpdate = DateTime.Now;

        public DateTime HeartRateLastUpdate
        {
            get { return _HeartRateLastUpdate; }
            set
            {
                _HeartRateLastUpdate = value;
                NotifyPropertyChanged(nameof(HeartRateLastUpdate));
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

        public enum Timezone
        {
            UTC,
            EST,
            CST,
            PST,
            CET,
            AEST
        }


        private int _HeartRate;

        public int HeartRate
        {
            get { return _HeartRate; }
            set
            {
                _HeartRate = value;
                NotifyPropertyChanged(nameof(HeartRate));
            }
        }


        private bool _ApplyHeartRateAdjustment = false;

        public bool ApplyHeartRateAdjustment
        {
            get { return _ApplyHeartRateAdjustment; }
            set
            {
                _ApplyHeartRateAdjustment = value;
                NotifyPropertyChanged(nameof(ApplyHeartRateAdjustment));
            }
        }


        private int _SmoothHeartRateTimeSpan = 4;

        public int SmoothHeartRateTimeSpan
        {
            get { return _SmoothHeartRateTimeSpan; }
            set
            {
                _SmoothHeartRateTimeSpan = value;
                NotifyPropertyChanged(nameof(SmoothHeartRateTimeSpan));
            }
        }


        private bool _SmoothHeartRate_v1 = true;

        public bool SmoothHeartRate_v1
        {
            get { return _SmoothHeartRate_v1; }
            set
            {
                _SmoothHeartRate_v1 = value;
                NotifyPropertyChanged(nameof(SmoothHeartRate_v1));
            }
        }


        private bool _ShowBPMSuffix = false;

        public bool ShowBPMSuffix
        {
            get { return _ShowBPMSuffix; }
            set
            {
                _ShowBPMSuffix = value;
                NotifyPropertyChanged(nameof(ShowBPMSuffix));
            }
        }

        private string _PulsoidAccessToken;

        public string PulsoidAccessToken
        {
            get { return _PulsoidAccessToken; }
            set
            {
                _PulsoidAccessToken = value;
                NotifyPropertyChanged(nameof(PulsoidAccessToken));
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
        private ObservableCollection<ProcessInfo> _scannedApps = new ObservableCollection<ProcessInfo>();
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


        private Version _LatestReleaseVersion;

        public Version LatestReleaseVersion
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


        private Version _PreReleaseVersion;

        public Version PreReleaseVersion
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

        public string ChatFeedbackTxt
        {
            get { return _ChatFeedbackTxt; }
            set
            {
                _ChatFeedbackTxt = value;
                NotifyPropertyChanged(nameof(ChatFeedbackTxt));
            }
        }

        public string ActiveChatTxt
        {
            get { return _ActiveChatTxt; }
            set
            {
                _ActiveChatTxt = value;
                NotifyPropertyChanged(nameof(ActiveChatTxt));
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


        private int _HeartRateTrendIndicatorSampleRate = 4;

        public int HeartRateTrendIndicatorSampleRate
        {
            get { return _HeartRateTrendIndicatorSampleRate; }
            set
            {
                _HeartRateTrendIndicatorSampleRate = value;
                NotifyPropertyChanged(nameof(HeartRateTrendIndicatorSampleRate));
            }
        }

        private bool _ShowHeartRateTrendIndicator = true;

        public bool ShowHeartRateTrendIndicator
        {
            get { return _ShowHeartRateTrendIndicator; }
            set
            {
                _ShowHeartRateTrendIndicator = value;
                NotifyPropertyChanged(nameof(ShowHeartRateTrendIndicator));
            }
        }

        private string _HeartRateTrendIndicator = string.Empty;

        public string HeartRateTrendIndicator
        {
            get { return _HeartRateTrendIndicator; }
            set
            {
                _HeartRateTrendIndicator = value;
                NotifyPropertyChanged(nameof(HeartRateTrendIndicator));
            }
        }


        private double _HeartRateTrendIndicatorSensitivity = 0.65;

        public double HeartRateTrendIndicatorSensitivity
        {
            get { return _HeartRateTrendIndicatorSensitivity; }
            set
            {
                _HeartRateTrendIndicatorSensitivity = value;
                NotifyPropertyChanged(nameof(HeartRateTrendIndicatorSensitivity));
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


        private int _HeartRateAdjustment = -5;

        public int HeartRateAdjustment
        {
            get { return _HeartRateAdjustment; }
            set
            {
                _HeartRateAdjustment = value;
                NotifyPropertyChanged(nameof(HeartRateAdjustment));
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


        private string _PulsoidAccessErrorTxt = string.Empty;

        public string PulsoidAccessErrorTxt
        {
            get { return _PulsoidAccessErrorTxt; }
            set
            {
                _PulsoidAccessErrorTxt = value;
                NotifyPropertyChanged(nameof(PulsoidAccessErrorTxt));
            }
        }

        private bool _PulsoidAccessError = false;

        public bool PulsoidAccessError
        {
            get { return _PulsoidAccessError; }
            set
            {
                _PulsoidAccessError = value;
                NotifyPropertyChanged(nameof(PulsoidAccessError));
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
                if(_IntgrScanSpotify)
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
                if(value < 1.6)
                {
                    _ScanningInterval = 1.6;
                } else if(value > 10)
                {
                    _ScanningInterval = 10;
                } else
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

        public Version AppVersion
        {
            get { return _AppVersion; }
            set
            {
                _AppVersion = value;
                NotifyPropertyChanged(nameof(AppVersion));
            }
        }

        public Version GitHubVersion
        {
            get { return _GitHubVersion; }
            set
            {
                _GitHubVersion = value;
                NotifyPropertyChanged(nameof(GitHubVersion));
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

        #endregion

        #region PropChangedEvent
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        #endregion
    }
}
