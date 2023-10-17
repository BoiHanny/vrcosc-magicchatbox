using System;
using System.ComponentModel;

namespace vrcosc_magicchatbox.ViewModels
{
    public class StatusItem : INotifyPropertyChanged
    {
        private DateTime _CreationDate;


        private string _editMsg = "";
        private bool _IsActive;


        private bool _IsEditing = false;
        private bool _IsFavorite;


        private DateTime _LastEdited;
        private DateTime _LastUsed;
        private string _msg = "";
        private int _MSGID;

        public DateTime CreationDate
        {
            get { return _CreationDate; }
            set
            {
                _CreationDate = value;
                NotifyPropertyChanged(nameof(CreationDate));
            }
        }

        public string editMsg
        {
            get { return _editMsg; }
            set
            {
                _editMsg = value;
                NotifyPropertyChanged(nameof(editMsg));
            }
        }

        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                NotifyPropertyChanged(nameof(IsActive));
            }
        }

        public bool IsEditing
        {
            get { return _IsEditing; }
            set
            {
                _IsEditing = value;
                NotifyPropertyChanged(nameof(IsEditing));
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

        public DateTime LastEdited
        {
            get
            {
                if(_LastEdited == null)
                {
                    return _CreationDate;
                } else
                {
                    return _LastEdited;
                }
            }

            set
            {
                _LastEdited = value;
                NotifyPropertyChanged(nameof(LastEdited));
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

        public string msg
        {
            get { return _msg; }
            set
            {
                _msg = value;
                NotifyPropertyChanged(nameof(msg));
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


        #region PropChangedEvent
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(string name)
        {
            if(PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}
