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
            _VM.PlayingSongTitle = _SPOT.CurrentPlayingSong();
            _VM.FocusedWindow = _ACTIV.GetForegroundProcessName();
            _VM.OSCtoSent = "Using '" + _VM.FocusedWindow + "' | listening to '" + _SPOT.CurrentPlayingSong() +"'";
        }
    }

}
