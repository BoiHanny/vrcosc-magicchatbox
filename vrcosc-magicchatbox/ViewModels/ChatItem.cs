using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using vrcosc_magicchatbox.Classes;

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

        public void CopyToClipboard(object parameter)
        {
            if (parameter is string text)
            {
                Clipboard.SetDataObject(text);
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
