using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private bool _applyCustomAppName;
        private string _customAppName;
        private int _focusCount;
        private bool _isPrivateApp;


        private string? _lastTitle = "";
        private string _processName;


        private bool _ShowTitle = false;
        private bool _usedNewMethod;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

        public bool ApplyCustomAppName
        {
            get { return _applyCustomAppName; }
            set
            {
                _applyCustomAppName = value;
                NotifyPropertyChanged(nameof(ApplyCustomAppName));
            }
        }

        public string CustomAppName
        {
            get { return _customAppName; }
            set
            {
                _customAppName = value;
                NotifyPropertyChanged(nameof(CustomAppName));
            }
        }

        public int FocusCount
        {
            get { return _focusCount; }
            set
            {
                _focusCount = value;
                NotifyPropertyChanged(nameof(FocusCount));
            }
        }

        public bool IsPrivateApp
        {
            get { return _isPrivateApp; }
            set
            {
                _isPrivateApp = value;
                NotifyPropertyChanged(nameof(IsPrivateApp));
            }
        }

        public string? LastTitle
        {
            get { return _lastTitle; }
            set
            {
                _lastTitle = value;
                NotifyPropertyChanged(nameof(LastTitle));
            }
        }

        public string ProcessName
        {
            get { return _processName; }
            set
            {
                _processName = value;
                NotifyPropertyChanged(nameof(ProcessName));
            }
        }

        public bool ShowTitle
        {
            get { return _ShowTitle; }
            set
            {
                _ShowTitle = value;
                NotifyPropertyChanged(nameof(ShowTitle));
            }
        }


        public bool UsedNewMethod
        {
            get { return _usedNewMethod; }
            set
            {
                _usedNewMethod = value;
                NotifyPropertyChanged(nameof(UsedNewMethod));
            }
        }
    }
}
