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


        private bool _UseInCycle = false;
        private string? _groupId;
        private bool _isSelected;

        public string? GroupId
        {
            get { return _groupId; }
            set
            {
                if (_groupId != value)
                {
                    _groupId = value;
                    NotifyPropertyChanged(nameof(GroupId));
                }
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    NotifyPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool UseInCycle
        {
            get { return _UseInCycle; }
            set
            {
                if (_UseInCycle != value)
                {
                    _UseInCycle = value;
                    NotifyPropertyChanged(nameof(UseInCycle));
                }
            }
        }


        public DateTime CreationDate
        {
            get { return _CreationDate; }
            set
            {
                if (_CreationDate != value)
                {
                    _CreationDate = value;
                    NotifyPropertyChanged(nameof(CreationDate));
                }
            }
        }

        public string editMsg
        {
            get { return _editMsg; }
            set
            {
                if (_editMsg != value)
                {
                    _editMsg = value;
                    NotifyPropertyChanged(nameof(editMsg));
                }
            }
        }

        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                if (_IsActive != value)
                {
                    _IsActive = value;
                    NotifyPropertyChanged(nameof(IsActive));
                }
            }
        }

        public bool IsEditing
        {
            get { return _IsEditing; }
            set
            {
                if (_IsEditing != value)
                {
                    _IsEditing = value;
                    NotifyPropertyChanged(nameof(IsEditing));
                }
            }
        }

        public bool IsFavorite
        {
            get { return _IsFavorite; }
            set
            {
                if (_IsFavorite != value)
                {
                    _IsFavorite = value;
                    NotifyPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public DateTime LastEdited
        {
            get
            {
                if (_LastEdited == null)
                {
                    return _CreationDate;
                }
                else
                {
                    return _LastEdited;
                }
            }

            set
            {
                if (_LastEdited != value)
                {
                    _LastEdited = value;
                    NotifyPropertyChanged(nameof(LastEdited));
                }
            }
        }

        public DateTime LastUsed
        {
            get { return _LastUsed; }
            set
            {
                if (_LastUsed != value)
                {
                    _LastUsed = value;
                    NotifyPropertyChanged(nameof(LastUsed));
                }
            }
        }

        public string msg
        {
            get { return _msg; }
            set
            {
                if (_msg != value)
                {
                    _msg = value;
                    NotifyPropertyChanged(nameof(msg));
                }
            }
        }

        public int MSGID
        {
            get { return _MSGID; }
            set
            {
                if (_MSGID != value)
                {
                    _MSGID = value;
                    NotifyPropertyChanged(nameof(MSGID));
                }
            }
        }


        #region PropChangedEvent
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
