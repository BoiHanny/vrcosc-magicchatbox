using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ViewModel : INotifyPropertyChanged
    {
        public static readonly ViewModel Instance = new ViewModel();

        public ICommand ActivateStatusCommand { get; set; }
        public ICommand ToggleVoiceCommand { get; }

        public ViewModel()
        {
            ActivateStatusCommand = new RelayCommand(ActivateStatus);
            ToggleVoiceCommand = new RelayCommand(ToggleVoice);
        }

        private void ToggleVoice()
        {
            if (Instance.ToggleVoiceWithV)
                OscSender.ToggleVoice(true);
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
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                return false;
            }

        }
        private void UpdateToggleVoiceText()
        {
            ToggleVoiceText = ToggleVoiceWithV ? "Toggle voice (V)" : "Toggle voice";
        }

        #region Properties     

        private ObservableCollection<StatusItem> _StatusList = new ObservableCollection<StatusItem>();
        private ObservableCollection<ChatItem> _LastMessages = new ObservableCollection<ChatItem>();
        private string _aesKey = "g5X5pFei6G8W6UwK6UaA6YhC6U8W6ZbP";
        private string _PlayingSongTitle = "";
        private bool _ScanPause = false;
        private bool _Topmost = false;
        private int _ScanPauseTimeout = 25;
        private int _ScanPauseCountDown = 0;
        private string _NewStatusItemTxt = "";
        private string _NewChattingTxt = "";
        private string _ChatFeedbackTxt = "";
        private string _FocusedWindow = "";
        private string _StatusTopBarTxt = "";
        private string _ChatTopBarTxt = "";
        private bool _SpotifyActive = false;
        private bool _SpotifyPaused = false;
        private bool _IsVRRunning = false;
        private bool _MasterSwitch = false;
        private bool _OnlyShowTimeVR = true;
        private bool _PrefixTime = false;
        private bool _PrefixChat = true;
        private bool _ChatFX = true;
        private bool _TypingIndicator = false;
        private bool _PrefixIconMusic = true;
        private bool _PauseIconMusic = true;
        private bool _PrefixIconStatus = true;
        private bool _CountDownUI = true;
        private bool _Time24H = false;
        private string _OSCtoSent = "";
        private string _ApiStream = "b2t8DhYcLcu7Nu0suPcvc8lO27wztrjMPbb + 8hQ1WPba2dq / iRyYpBEDZ0NuMNKR5GRrF2XdfANLud0zihG / UD + ewVl1p3VLNk1mrNdrdg88rguzi6RJ7T1AA7hyBY + F";
        private Version _AppVersion = new("0.7.0");
        private Version _GitHubVersion;
        private string _VersionTxt = "Check for updates";
        private string _VersionTxtColor = "#FF8F80B9";
        private string _StatusBoxCount = "0/140";
        private string _StatusBoxColor = "#FF504767";
        private string _ChatBoxCount = "0/140";
        private string _ChatBoxColor = "#FF504767";
        private string _CurrentTime = "";
        private string _ActiveChatTxt = "";
        private bool _IntgrStatus = false;
        private bool _IntgrScanWindowActivity = false;
        private bool _IntgrScanWindowTime = false;
        private bool _IntgrScanSpotify = false;
        private int _ScanInterval = 4;
        private int _CurrentMenuItem = 0;
        private string _MenuItem_0_Visibility = "Hidden";
        private string _MenuItem_1_Visibility = "Hidden";
        private string _MenuItem_2_Visibility = "Hidden";
        private string _MenuItem_3_Visibility = "Visible";
        private int _OSCmsg_count = 0;
        private string _OSCmsg_countUI = "";
        private string _OSCIP = "127.0.0.1";
        private string _Char_Limit = "Hidden";
        private string _Spotify_Opacity = "1";
        private string _Status_Opacity = "1";
        private string _Window_Opacity = "1";
        private string _Time_Opacity = "1";
        private int _OSCPortOut = 9000;
        private string _DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");
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
        private string _NewVersionURL;
        private bool _CanUpdate;
        private string _toggleVoiceText = "Toggle voice (V)";
        private bool _AutoUnmuteTTS = true;
        private bool _ToggleVoiceWithV = true;
        private bool _TTSBtnShadow = false;
        private float _TTSVolume = 0.2f;


        private bool _GetForegroundProcessNew = true;
        public bool GetForegroundProcessNew
        {
            get { return _GetForegroundProcessNew; }
            set
            {
                _GetForegroundProcessNew = value;
                NotifyPropertyChanged(nameof(GetForegroundProcessNew));
            }
        }

        private bool _IntgrIntelliWing = false;
        public bool IntgrIntelliWing
        {
            get { return _IntgrIntelliWing; }
            set
            {
                _IntgrIntelliWing = value;
                NotifyPropertyChanged(nameof(IntgrIntelliWing));
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


        private ObservableCollection<ChatModelMsg> _OpenAIAPIBuiltInActions;
        public ObservableCollection<ChatModelMsg> OpenAIAPIBuiltInActions
        {
            get { return _OpenAIAPIBuiltInActions; }
            set
            {
                _OpenAIAPIBuiltInActions = value;
                NotifyPropertyChanged(nameof(OpenAIAPIBuiltInActions));
            }
        }

        private string _OpenAIAPITestResponse;
        public string OpenAIAPITestResponse
        {
            get { return _OpenAIAPITestResponse; }
            set
            {
                _OpenAIAPITestResponse = value;
                NotifyPropertyChanged(nameof(OpenAIAPITestResponse));
            }
        }
        private int _OpenAIUsedTokens;
        public int OpenAIUsedTokens
        {
            get { return _OpenAIUsedTokens; }
            set
            {
                _OpenAIUsedTokens = value;
                NotifyPropertyChanged(nameof(OpenAIUsedTokens));
            }
        }


        private bool _IntelliChatModeration = true;
        public bool IntelliChatModeration
        {
            get { return _IntelliChatModeration; }
            set
            {
                _IntelliChatModeration = value;
                NotifyPropertyChanged(nameof(IntelliChatModeration));
            }
        }

        private string _OpenAIModerationUrl;
        public string OpenAIModerationUrl
        {
            get { return _OpenAIModerationUrl; }
            set
            {
                _OpenAIModerationUrl = value;
                NotifyPropertyChanged(nameof(OpenAIModerationUrl));
            }
        }

        private bool _IntgrIntelliChat = true;
        public bool IntgrIntelliChat
        {
            get { return _IntgrIntelliChat; }
            set
            {
                _IntgrIntelliChat = value;
                NotifyPropertyChanged(nameof(IntgrIntelliChat));
            }
        }

        private string _OpenAIAPISelectedModel;
        public string OpenAIAPISelectedModel
        {
            get { return _OpenAIAPISelectedModel; }
            set
            {
                _OpenAIAPISelectedModel = value;
                NotifyPropertyChanged(nameof(OpenAIAPISelectedModel));
            }
        }

        private ObservableCollection<string> _OpenAIAPIModels;
        public ObservableCollection<string> OpenAIAPIModels
        {
            get { return _OpenAIAPIModels; }
            set
            {
                _OpenAIAPIModels = value;
                NotifyPropertyChanged(nameof(OpenAIAPIModels));
            }
        }

        private string _OpenAIAPIUrl;
        public string OpenAIAPIUrl
        {
            get { return _OpenAIAPIUrl; }
            set
            {
                _OpenAIAPIUrl = value;
                NotifyPropertyChanged(nameof(OpenAIAPIUrl));
            }
        }

        private string _OpenAIAPIKey;
        public string OpenAIAPIKey
        {
            get { return _OpenAIAPIKey; }
            set
            {
                _OpenAIAPIKey = value;
                NotifyPropertyChanged(nameof(OpenAIAPIKey));
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
        public string NewVersionURL
        {
            get { return _NewVersionURL; }
            set
            {
                _NewVersionURL = value;
                NotifyPropertyChanged(nameof(NewVersionURL));
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
            set { _auxOutputDevices = value; NotifyPropertyChanged(nameof(AuxOutputDevices)); }
        }
        public List<AudioDevice> PlaybackOutputDevices
        {
            get { return _playbackOutputDevices; }
            set { _playbackOutputDevices = value; NotifyPropertyChanged(nameof(PlaybackOutputDevices)); }
        }
        public AudioDevice SelectedAuxOutputDevice
        {
            get { return _selectedAuxOutputDevice; }
            set { _selectedAuxOutputDevice = value; NotifyPropertyChanged(nameof(SelectedAuxOutputDevice)); }
        }
        public AudioDevice SelectedPlaybackOutputDevice
        {
            get { return _selectedPlaybackOutputDevice; }
            set { _selectedPlaybackOutputDevice = value; NotifyPropertyChanged(nameof(SelectedPlaybackOutputDevice)); }
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
        public bool OnlyShowTimeVR
        {
            get { return _OnlyShowTimeVR; }
            set
            {
                _OnlyShowTimeVR = value;
                NotifyPropertyChanged(nameof(OnlyShowTimeVR));
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

        public bool IntgrScanSpotify
        {
            get { return _IntgrScanSpotify; }
            set
            {
                _IntgrScanSpotify = value;
                NotifyPropertyChanged(nameof(IntgrScanSpotify));
            }
        }

        public int ScanInterval
        {
            get { return _ScanInterval; }
            set
            {
                _ScanInterval = value;
                NotifyPropertyChanged(nameof(ScanInterval));
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
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
