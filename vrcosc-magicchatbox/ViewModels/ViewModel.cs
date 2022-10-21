using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ViewModel : INotifyPropertyChanged
    {
        #region Properties

        private string _PlayingSongTitle = "";
        private string _FocusedWindow = "";
        private bool _SpotifyActive = false;
        private bool _SpotifyPaused = false;
        private bool _IsVRRunning = false;
        private bool _MasterSwitch = false;
        private string _OSCtoSent = "";
        private string _AppVersion = "0.3.2";
        private string _NewVersion = "Check for updates";
        private string _CurrentTIme = "";
        private bool _IntgrScanWindowActivity = false;
        private bool _IntgrScanWindowTime = false;
        private bool _IntgrScanSpotify = false;
        private int _ScanInterval = 4;
        private int _OSCmsg_count = 0;
        private string _OSCmsg_countUI = "";
        private string _OSCIP = "127.0.0.1";
        private int _OSCPort = 9000;
        private string _DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox");

        public bool MasterSwitch
        {
            get { return _MasterSwitch; }
            set
            {
                _MasterSwitch = value;
                NotifyPropertyChanged(nameof(MasterSwitch));
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

        public int OSCPort
        {
            get { return _OSCPort; }
            set
            {
                _OSCPort = value;
                NotifyPropertyChanged(nameof(OSCPort));
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

        public string CurrentTIme
        {
            get { return _CurrentTIme; }
            set
            {
                _CurrentTIme = value;
                NotifyPropertyChanged(nameof(CurrentTIme));
            }
        }



        public string NewVersion
        {
            get { return _NewVersion; }
            set
            {
                _NewVersion = value;
                NotifyPropertyChanged(nameof(NewVersion));
            }
        }
        public string AppVersion
        {
            get { return _AppVersion; }
            set
            {
                _AppVersion = value;
                NotifyPropertyChanged(nameof(AppVersion));
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
