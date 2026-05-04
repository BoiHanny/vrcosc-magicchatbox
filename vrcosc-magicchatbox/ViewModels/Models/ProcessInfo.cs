using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels
{
    /// <summary>
    /// Represents a tracked Windows process with display customization options
    /// for the Window Activity feature.
    /// </summary>
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
        private bool _useCustomRegex;
        private string _customRegex = string.Empty;
        private string _contentFilter = string.Empty;
        private int _contentFilterMode; // 0=None, 1=Exclude, 2=Include

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

        /// <summary>When true, apply <see cref="CustomRegex"/> to the window title before displaying.</summary>
        public bool UseCustomRegex
        {
            get { return _useCustomRegex; }
            set
            {
                _useCustomRegex = value;
                NotifyPropertyChanged(nameof(UseCustomRegex));
            }
        }

        /// <summary>Regex pattern applied to the window title. First capture group is used as display text.</summary>
        public string CustomRegex
        {
            get { return _customRegex; }
            set
            {
                _customRegex = value;
                NotifyPropertyChanged(nameof(CustomRegex));
            }
        }

        /// <summary>
        /// Per-app content filter pattern (case-insensitive substring match).
        /// Applied after regex extraction. Works with <see cref="ContentFilterMode"/>.
        /// </summary>
        public string ContentFilter
        {
            get { return _contentFilter; }
            set
            {
                _contentFilter = value ?? string.Empty;
                NotifyPropertyChanged(nameof(ContentFilter));
            }
        }

        /// <summary>
        /// Filter mode for this app: 0=None (no filter), 1=Exclude (hide when matches), 2=Include (show only when matches).
        /// Stored as int for JSON serialization compatibility.
        /// </summary>
        public int ContentFilterMode
        {
            get { return _contentFilterMode; }
            set
            {
                _contentFilterMode = value;
                NotifyPropertyChanged(nameof(ContentFilterMode));
                NotifyPropertyChanged(nameof(HasContentFilter));
                NotifyPropertyChanged(nameof(ContentFilterEnabled));
            }
        }

        /// <summary>Whether the content filter textbox should be enabled (mode is not None).</summary>
        public bool ContentFilterEnabled => _contentFilterMode != 0;

        /// <summary>Whether this app has an active content filter configured.</summary>
        public bool HasContentFilter => _contentFilterMode != 0 && !string.IsNullOrWhiteSpace(_contentFilter);
    }
}
