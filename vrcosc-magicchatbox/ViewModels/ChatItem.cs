using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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
            SendAgainCommand = new RelayCommand(OnSendAgainAsync);
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

        public void OnSendAgainAsync(object parameter)
        {
            try
            {
                if (ViewModel.Instance.MasterSwitch == false)
                {
                    ViewModel.Instance.ChatFeedbackTxt = "Sent to VRChat is off";
                    return;
                }
                if (parameter is string text)
                {
                    string savedtxt = ViewModel.Instance.NewChattingTxt;
                    ViewModel.Instance.NewChattingTxt = text;
                    OscSender.CreateChat(false);
                    OscSender.SendOSCMessage(true);
                    ViewModel.Instance.NewChattingTxt = savedtxt;

                    if (ViewModel.Instance.TTSTikTokEnabled == true)
                    {
                        if (DataAndSecurity.DataController.PopulateOutputDevices(true))
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Requesting TTS...";
                            MainWindow.TTSGOAsync(text, true);
                        }
                        else
                        {
                            ViewModel.Instance.ChatFeedbackTxt = "Error setting output device.";
                        }
                    }
                    else
                    {
                        ViewModel.Instance.ChatFeedbackTxt = "Message sent again";
                    }

                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
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
