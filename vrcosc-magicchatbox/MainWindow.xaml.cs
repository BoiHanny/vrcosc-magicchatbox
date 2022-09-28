using System.Windows;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox
{
    public partial class MainWindow : Window
    {
        private ViewModel _VM;
        private SpotifyActivity _SPOT;
        private OscController _OSC;
        private SystemStats _STATS;
        private WindowActivity _ACTIV;

        public MainWindow()
        {
            _VM = new ViewModel();
            _SPOT = new SpotifyActivity(_VM);
            _OSC = new OscController(_VM);
            _STATS = new SystemStats(_VM);
            _ACTIV = new WindowActivity(_VM);

            this.DataContext = _VM;
            InitializeComponent();

        }  
    }

}
