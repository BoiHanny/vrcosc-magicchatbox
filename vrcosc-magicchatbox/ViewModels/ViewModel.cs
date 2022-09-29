using System.Collections.ObjectModel;
using System.ComponentModel;


namespace vrcosc_magicchatbox.ViewModels
{
    public class ViewModel : INotifyPropertyChanged
    {
        #region Properties

        private int _ScanInterval = 1;
        private string _PlayingSongTitle = "";
        private string _FocusedWindow = "";
        private bool _SpotifyActive = false;
        private string _OSCtoSent = "";

        public string OSCtoSent
        {
            get { return _OSCtoSent; }
            set
            {
                _OSCtoSent = value;
                NotifyPropertyChanged(nameof(OSCtoSent));
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
