using System;
using System.ComponentModel;
using vrcosc_magicchatbox.ViewModels;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;

namespace vrcosc_magicchatbox.ViewModels
{


    public class ChatItem : INotifyPropertyChanged
    {
        private ViewModel _VM;
        private OscController _OSC;
        private DateTime _creationDate;
        private string _msg = "";
        private string _opacity;
        private int _ID;

        public ChatItem(ViewModel vm)
        {
            _VM = vm;
            _OSC = new OscController(_VM);
            CopyToClipboardCommand = new RelayCommand(CopyToClipboard);
            SendAgainCommand = new RelayCommand(OnSendAgain);
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
        public ICommand SendAgainCommand { get; }

        public void CopyToClipboard(object parameter)
        {
            if (parameter is string text)
            {
                Clipboard.SetDataObject(text);
                _VM.ChatFeedbackTxt = "Message copied";
            }
        }

        public void OnSendAgain(object parameter)
        {
            if (parameter is string text)
            {
                string savedtxt = _VM.NewChattingTxt;
                _VM.NewChattingTxt = text;
                _OSC.CreateChat(false);
                _OSC.SentOSCMessage(true);
                _VM.NewChattingTxt = savedtxt;
                _VM.ChatFeedbackTxt = "Message sent again";
            }
        }


        #region PropChangedEvent

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

}
