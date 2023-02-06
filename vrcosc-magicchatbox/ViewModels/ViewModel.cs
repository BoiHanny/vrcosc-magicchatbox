using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ViewModel : INotifyPropertyChanged
    {
        public ICommand ActivateStatusCommand { get; set; }


        public ViewModel()
        {
            ActivateStatusCommand = new RelayCommand(ActivateStatus);
        }

        public void ActivateStatus(object parameter)
        {
            var item = parameter as StatusItem;
            foreach (var i in StatusList)
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

        public void SaveStatusList()
        {
            try
            {
                if (CreateIfMissing(DataPath) == true)
                {
                    string json = JsonConvert.SerializeObject(StatusList);
                    File.WriteAllText(Path.Combine(DataPath, "StatusList.xml"), json);
                }

            }
            catch (Exception)
            {

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
                return false;
            }

        }

        #region Properties     

        private ObservableCollection<StatusItem> _StatusList = new ObservableCollection<StatusItem>();
        private string _PlayingSongTitle = "";
        private string _NewStatusItemTxt = "";
        private string _FocusedWindow = "";
        private string _StatusTopBarTxt = "";
        private bool _SpotifyActive = false;
        private bool _SpotifyPaused = false;
        private bool _IsVRRunning = false;
        private bool _MasterSwitch = false;
        private bool _OnlyShowTimeVR = true;
        private bool _PrefixTime = false;
        private bool _PrefixIconMusic = true;
        private bool _PrefixIconStatus = true;
        private bool _Time24H = false;
        private string _OSCtoSent = "";
        private Version _AppVersion = new("0.4.0");
        private Version _GitHubVersion;
        private string _VersionTxt = "Check for updates";
        private string _VersionTxtColor = "#FF8F80B9";
        private string _StatusBoxCount = "0/140";
        private string _StatusBoxColor = "#FF504767";
        private string _CurrentTime = "";
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

        public string StatusTopBarTxt
        {
            get { return _StatusTopBarTxt; }
            set
            {
                _StatusTopBarTxt = value;
                NotifyPropertyChanged(nameof(StatusTopBarTxt));
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

        public string StatusBoxCount
        {
            get { return _StatusBoxCount; }
            set
            {
                _StatusBoxCount = value;
                NotifyPropertyChanged(nameof(StatusBoxCount));
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
            get  { return _OSCIP; }
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
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
