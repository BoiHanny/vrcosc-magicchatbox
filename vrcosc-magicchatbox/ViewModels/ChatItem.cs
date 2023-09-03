using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using vrcosc_magicchatbox.Classes;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.ViewModels
{


    public class ChatItem : INotifyPropertyChanged
    {
        private DateTime _creationDate;
        private string _msg = "";
        private string _opacity;
        private int _ID;

        public ChatItem()
        {
            CopyToClipboardCommand = new RelayCommand(CopyToClipboard);

        }


        private bool _CancelLiveEdit = false;
        public bool CancelLiveEdit
        {
            get { return _CancelLiveEdit; }
            set
            {
                _CancelLiveEdit = value;
                NotifyPropertyChanged(nameof(CancelLiveEdit));
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


        private string _Opacity_backup;
        public string Opacity_backup
        {
            get { return _Opacity_backup; }
            set
            {
                _Opacity_backup = value;
                NotifyPropertyChanged(nameof(Opacity_backup));
            }
        }

        private bool _CanLiveEditRun = false;
        public bool CanLiveEditRun
        {
            get { return _CanLiveEditRun; }
            set
            {
                _CanLiveEditRun = value;
                NotifyPropertyChanged(nameof(CanLiveEditRun));
            }
        }

        private bool _CanLiveEdit = false;
        public bool CanLiveEdit
        {
            get { return _CanLiveEdit; }
            set
            {
                _CanLiveEdit = value;
                NotifyPropertyChanged(nameof(CanLiveEdit));
            }
        }

        private string _MsgReplace = "";
        public string MsgReplace
        {
            get { return _MsgReplace; }
            set
            {
                _MsgReplace = value;
                NotifyPropertyChanged(nameof(MsgReplace));
            }
        }




        private string _LiveEditButtonTxt = "Sending...";
        public string LiveEditButtonTxt
        {
            get { return _LiveEditButtonTxt; }
            set
            {
                _LiveEditButtonTxt = value;
                NotifyPropertyChanged(nameof(LiveEditButtonTxt));
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

        public string Msg
        {
            get { return _msg; }
            set
            {
                _msg = value;
                NotifyPropertyChanged(nameof(Msg));
            }
        }

        private string _MainMsg = "";
        public string MainMsg
        {
            get { return _MainMsg; }
            set
            {
                _MainMsg = value;
                NotifyPropertyChanged(nameof(MainMsg));
            }
        }
        public DateTime CreationDate
        {
            get { return _creationDate; }
            set
            {
                _creationDate = value;
                NotifyPropertyChanged(nameof(CreationDate));
            }
        }

        public ICommand CopyToClipboardCommand { get; }

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


        private bool _IsRunning = false;
        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                _IsRunning = value;
                NotifyPropertyChanged(nameof(IsRunning));
            }
        }


        #region PropChangedEvent

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

    }

}
