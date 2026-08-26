using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    public partial class ChatItem : INotifyPropertyChanged
    {
        internal static ChatStatusDisplayState? DefaultChatStatus { get; set; }

        private readonly ChatStatusDisplayState _chatStatus;
        private bool _CancelLiveEdit = false;

        private bool _CanLiveEdit = false;

        private bool _CanLiveEditRun = false;
        private DateTime _creationDate;
        private int _ID;


        private bool _IsRunning = false;


        private string _LiveEditButtonTxt = "Edit";

        private string _MainMsg = "";
        private string _msg = "";

        private string _MsgReplace = "";
        private string? _opacity;


        private string? _Opacity_backup;

        public ChatItem(ChatStatusDisplayState chatStatus)
        {
            _chatStatus = chatStatus;
        }

        public ChatItem()
        {
            _chatStatus = DefaultChatStatus
                ?? throw new InvalidOperationException("ChatItem.DefaultChatStatus must be set before deserialization.");
        }

        [RelayCommand]
        private void CopyToClipboard(object parameter)
        {
            try
            {
                if (parameter is string text)
                {
                    Clipboard.SetDataObject(text);
                    _chatStatus.ChatFeedbackTxt = "Message copied";
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public bool CancelLiveEdit
        {
            get { return _CancelLiveEdit; }
            set
            {
                if (_CancelLiveEdit != value)
                {
                    _CancelLiveEdit = value;
                    NotifyPropertyChanged(nameof(CancelLiveEdit));
                }
            }
        }

        public bool CanLiveEdit
        {
            get { return _CanLiveEdit; }
            set
            {
                if (_CanLiveEdit != value)
                {
                    _CanLiveEdit = value;
                    NotifyPropertyChanged(nameof(CanLiveEdit));
                }
            }
        }

        public bool CanLiveEditRun
        {
            get { return _CanLiveEditRun; }
            set
            {
                if (_CanLiveEditRun != value)
                {
                    _CanLiveEditRun = value;
                    NotifyPropertyChanged(nameof(CanLiveEditRun));
                }
            }
        }

        public DateTime CreationDate
        {
            get { return _creationDate; }
            set
            {
                if (_creationDate != value)
                {
                    _creationDate = value;
                    NotifyPropertyChanged(nameof(CreationDate));
                }
            }
        }

        public int ID
        {
            get { return _ID; }
            set
            {
                if (_ID != value)
                {
                    _ID = value;
                    NotifyPropertyChanged(nameof(ID));
                }
            }
        }

        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                if (_IsRunning != value)
                {
                    _IsRunning = value;
                    NotifyPropertyChanged(nameof(IsRunning));
                }
            }
        }

        public string LiveEditButtonTxt
        {
            get { return _LiveEditButtonTxt; }
            set
            {
                if (_LiveEditButtonTxt != value)
                {
                    _LiveEditButtonTxt = value;
                    NotifyPropertyChanged(nameof(LiveEditButtonTxt));
                }
            }
        }

        public string MainMsg
        {
            get { return _MainMsg; }
            set
            {
                if (_MainMsg != value)
                {
                    _MainMsg = value;
                    NotifyPropertyChanged(nameof(MainMsg));
                }
            }
        }

        public string Msg
        {
            get { return _msg; }
            set
            {
                if (_msg != value)
                {
                    _msg = value;
                    NotifyPropertyChanged(nameof(Msg));
                }
            }
        }

        public string MsgReplace
        {
            get { return _MsgReplace; }
            set
            {
                if (_MsgReplace != value)
                {
                    _MsgReplace = value;
                    NotifyPropertyChanged(nameof(MsgReplace));
                }
            }
        }


        public string? Opacity
        {
            get { return _opacity; }
            set
            {
                if (_opacity != value)
                {
                    _opacity = value;
                    NotifyPropertyChanged(nameof(Opacity));
                }
            }
        }

        public string? Opacity_backup
        {
            get { return _Opacity_backup; }
            set
            {
                if (_Opacity_backup != value)
                {
                    _Opacity_backup = value;
                    NotifyPropertyChanged(nameof(Opacity_backup));
                }
            }
        }

        #region PropChangedEvent
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        #endregion
    }
}
