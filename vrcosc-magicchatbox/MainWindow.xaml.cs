using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Threading;
using System;
using System.Windows.Input;
using System.Diagnostics;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private ViewModel _VM;
        private SpotifyActivity _SPOT;
        private DataController _DATAC;
        private OscController _OSC;
        private SystemStats _STATS;
        private WindowActivity _ACTIV;
        public float samplingTime = 1;

        DispatcherTimer backgroundCheck = new DispatcherTimer();

        public MainWindow()
        {
            Closing += SaveDataToDisk;
            _VM = new ViewModel();
            _SPOT = new SpotifyActivity(_VM);
            _DATAC = new DataController(_VM);
            _OSC = new OscController(_VM);
            _STATS = new SystemStats(_VM);
            _ACTIV = new WindowActivity(_VM);

            this.DataContext = _VM;
            InitializeComponent();

            backgroundCheck.Tick += Timer; backgroundCheck.Interval = new TimeSpan(0, 0, _VM.ScanInterval); backgroundCheck.Start();
            _VM.IntgrScanWindowActivity = true;
            _VM.IntgrScanSpotify = true;
            _VM.IntgrScanWindowTime = true;
            _VM.IntgrStatus = true;
            _VM.MasterSwitch = true;
            _DATAC.LoadSettingsFromXML();
            scantick();
        }

        private void SaveDataToDisk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            _DATAC.SaveSettingsToXML();
            System.Environment.Exit(1);
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
            { 
                _VM.CurrentTime = _STATS.GetTime(); }
            _OSC.BuildOSC();
            _OSC.SentOSCMessage();
        }


        private void Update_Click(object sender, RoutedEventArgs e)
        {
            _OSC.BuildOSC();
        }

        private void Drag_area_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
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

        private void NewVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("explorer", "http://github.com/BoiHanny/vrcosc-magicchatbox/releases");
        }

        private void MasterSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_VM.MasterSwitch == true)
            {
                backgroundCheck.Start();
            }
            else
            {
                backgroundCheck.Stop();
            }

        }

        private void Status_switch_Click(object sender, RoutedEventArgs e)
        {

        }
    }

}
