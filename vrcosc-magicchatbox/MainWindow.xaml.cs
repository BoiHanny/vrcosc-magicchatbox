using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Threading;
using System;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private ViewModel _VM;
        private SpotifyActivity _SPOT;
        private OscController _OSC;
        private SystemStats _STATS;
        private WindowActivity _ACTIV;
        public float samplingTime = 1;

        public MainWindow()
        {
            _VM = new ViewModel();
            _SPOT = new SpotifyActivity(_VM);
            _OSC = new OscController(_VM);
            _STATS = new SystemStats(_VM);
            _ACTIV = new WindowActivity(_VM);

            this.DataContext = _VM;
            InitializeComponent();

            DispatcherTimer backgroundCheck = new DispatcherTimer();
            backgroundCheck.Tick += backgroundCheck_Tick; backgroundCheck.Interval = new TimeSpan(0, 0, _VM.ScanInterval); backgroundCheck.Start();

        }

        private void backgroundCheck_Tick(object sender, EventArgs e)
        {
            if (_VM.IntgrScanSpotify == true)
            { _VM.PlayingSongTitle = _SPOT.CurrentPlayingSong(); _VM.SpotifyActive = _SPOT.SpotifyIsRunning(); }
            if (_VM.IntgrScanWindowActivity == true)
            { _VM.FocusedWindow = _ACTIV.GetForegroundProcessName(); _VM.IsVRRunning = _ACTIV.IsVRRunning(); }
            BuildOSC();
            _OSC.SentOSCMessage();

        }

        public void BuildOSC()
        {
            string msg = "";
            if(_VM.IntgrScanWindowActivity == true)
            {
                if (_VM.IsVRRunning)
                {
                    msg = "Vibing in 'VRChat' using SteamVR";
                }
                else
                {
                    msg = "On desktop in '" + _VM.FocusedWindow + "'";
                }
            }
            if(_VM.IntgrScanSpotify)
            {
                if (_VM.SpotifyActive)
                    if (_VM.SpotifyPaused)
                    {
                        msg = msg + " | Music paused.";
                    }
                    else
                    {
                        msg = msg + " | listening to '" + _VM.PlayingSongTitle + "'.";
                    }
                ;
            }
            if(msg.Length > 0)
            {
                _VM.OSCtoSent = msg;
            }
            
        }
    }

}
