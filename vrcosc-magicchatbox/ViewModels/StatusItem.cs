using System;
using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels
{
    public class StatusItem : INotifyPropertyChanged
    {

        private DateTime _CreationDate;
        private DateTime _LastUsed;
        private int _MSGLenght;
        private int _MSGID;
        private string _msg = "";
        private bool _IsFavorite;
        private bool _IsActive;

        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                NotifyPropertyChanged(nameof(IsActive));
            }
        }

        public bool IsFavorite
        {
            get { return _IsFavorite; }
            set
            {
                _IsFavorite = value;
                NotifyPropertyChanged(nameof(IsFavorite));
            }
        }

        public string msg
        {
            get { return _msg; }
            set
            {
                _msg = value;
                NotifyPropertyChanged(nameof(msg));
            }
        }

        public int MSGLenght
        {
            get { return _MSGLenght; }
            set
            {
                _MSGLenght = value;
                NotifyPropertyChanged(nameof(MSGLenght));
            }
        }

        public int MSGID
        {
            get { return _MSGID; }
            set
            {
                _MSGID = value;
                NotifyPropertyChanged(nameof(MSGID));
            }
        }


        public DateTime CreationDate
        {
            get { return _CreationDate; }
            set
            {
                _CreationDate = value;
                NotifyPropertyChanged(nameof(CreationDate));
            }
        }

        public DateTime LastUsed
        {
            get { return _LastUsed; }
            set
            {
                _LastUsed = value;
                NotifyPropertyChanged(nameof(LastUsed));
            }
        }



        #region PropChangedEvent
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
