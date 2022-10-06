using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Threading;
using System;
using System.Windows.Input;

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
            backgroundCheck.Tick += Timer; backgroundCheck.Interval = new TimeSpan(0, 0, _VM.ScanInterval); backgroundCheck.Start();
            _VM.IntgrScanWindowActivity = true;
            _VM.IntgrScanSpotify = true;
            _VM.IntgrScanWindowTime = true;
            scantick();

        }

        private void Timer(object sender, EventArgs e)
        {
            scantick();
        }

        public void scantick()
        {
            if (_VM.IntgrScanSpotify == true)
            { _VM.PlayingSongTitle = _SPOT.CurrentPlayingSong(); _VM.SpotifyActive = _SPOT.SpotifyIsRunning(); }
            if (_VM.IntgrScanWindowActivity == true)
            { _VM.FocusedWindow = _ACTIV.GetForegroundProcessName(); _VM.IsVRRunning = _ACTIV.IsVRRunning(); }
            if (_VM.IntgrScanWindowTime == true)
            { _VM.CurrentTIme = _STATS.GetTime(); }
            _OSC.BuildOSC();
            _OSC.SentOSCMessage();
        }


        private void Spotify_switch_Click(object sender, RoutedEventArgs e)
        {
            _OSC.BuildOSC();
        }

        private void WindowActivity_switch_Click(object sender, RoutedEventArgs e)
        {
            _OSC.BuildOSC();
        }

        private void Drag_area_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void SystemTime_switch_Click(object sender, RoutedEventArgs e)
        {
            _OSC.BuildOSC();
        }

        private void Button_close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
            this.Close();
        }

        private void Button_minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }

}
