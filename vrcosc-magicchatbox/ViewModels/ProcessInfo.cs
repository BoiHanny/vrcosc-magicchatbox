using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.ViewModels
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private string _processName;
        private bool _usedNewMethod;
        private bool _applyCustomAppName;
        private string _customAppName;
        private bool _isPrivateApp;
        private int _focusCount;

        public string ProcessName
        {
            get { return _processName; }
            set
            {
                _processName = value;
                NotifyPropertyChanged(nameof(ProcessName));
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

        public bool IsPrivateApp
        {
            get { return _isPrivateApp; }
            set
            {
                _isPrivateApp = value;
                NotifyPropertyChanged(nameof(IsPrivateApp));
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
