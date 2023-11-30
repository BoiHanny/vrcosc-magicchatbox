using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public class ChatItem : INotifyPropertyChanged
    {
        private bool _CancelLiveEdit = false;

        private bool _CanLiveEdit = false;

        private bool _CanLiveEditRun = false;
        private DateTime _creationDate;
        private int _ID;


        private bool _IsRunning = false;


        private string _LiveEditButtonTxt = "Sending...";

        private string _MainMsg = "";
        private string _msg = "";

        private string _MsgReplace = "";
        private string _opacity;


        private string _Opacity_backup;

        public ChatItem() { CopyToClipboardCommand = new RelayCommand(CopyToClipboard); }

        public void CopyToClipboard(object parameter)
        {
            try
            {
                if (parameter is string text)
                {
                    Clipboard.SetDataObject(text);
                    ViewModel.Instance.ChatFeedbackTxt = "Message copied";
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
            }
        }

        public bool CancelLiveEdit
        {
            get { return _CancelLiveEdit; }
            set
            {
                _CancelLiveEdit = value;
                NotifyPropertyChanged(nameof(CancelLiveEdit));
            }
        }

        public bool CanLiveEdit
        {
            get { return _CanLiveEdit; }
            set
            {
                _CanLiveEdit = value;
                NotifyPropertyChanged(nameof(CanLiveEdit));
            }
        }

        public bool CanLiveEditRun
        {
            get { return _CanLiveEditRun; }
            set
            {
                _CanLiveEditRun = value;
                NotifyPropertyChanged(nameof(CanLiveEditRun));
            }
        }

        public ICommand CopyToClipboardCommand { get; }

        public DateTime CreationDate
        {
            get { return _creationDate; }
            set
            {
                _creationDate = value;
                NotifyPropertyChanged(nameof(CreationDate));
            }
        }

        public int ID
        {
            get { return _ID; }
            set
            {
                _ID = value;
                NotifyPropertyChanged(nameof(ID));
            }
        }

        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                _IsRunning = value;
                NotifyPropertyChanged(nameof(IsRunning));
            }
        }

        public string LiveEditButtonTxt
        {
            get { return _LiveEditButtonTxt; }
            set
            {
                _LiveEditButtonTxt = value;
                NotifyPropertyChanged(nameof(LiveEditButtonTxt));
            }
        }

        public string MainMsg
        {
            get { return _MainMsg; }
            set
            {
                _MainMsg = value;
                NotifyPropertyChanged(nameof(MainMsg));
            }
        }

        public string Msg
        {
            get { return _msg; }
            set
            {
                _msg = value;
                NotifyPropertyChanged(nameof(Msg));
            }
        }

        public string MsgReplace
        {
            get { return _MsgReplace; }
            set
            {
                _MsgReplace = value;
                NotifyPropertyChanged(nameof(MsgReplace));
            }
        }


        public string Opacity
        {
            get { return _opacity; }
            set
            {
                _opacity = value;
                NotifyPropertyChanged(nameof(Opacity));
            }
        }

        public string Opacity_backup
        {
            get { return _Opacity_backup; }
            set
            {
                _Opacity_backup = value;
                NotifyPropertyChanged(nameof(Opacity_backup));
            }
        }

        #region PropChangedEvent
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        #endregion
    }
}
