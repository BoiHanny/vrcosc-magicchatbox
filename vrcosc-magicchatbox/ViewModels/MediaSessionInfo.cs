using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.ViewModels
{
    [DebuggerDisplay("{FriendlyAppName} - {TimePeekEnabled} - {TimePosition}/{CurrentTime}/{FullTime} live:{IsLiveTime}")]
    public class MediaSessionInfo : INotifyPropertyChanged
    {
        private bool _AutoSwitch = ViewModel.Instance.MediaSession_AutoSwitchSpawn;


        private bool _IsActive;

        private bool _IsVideo;

        private bool _KeepSaved = false;

        private bool _ShowArtist = true;

        private bool _ShowTitle = true;


        private bool _TimeoutRestore = false;
        private MediaSession session;

        public string AlbumArtist = "Album-Artist";
        public string AlbumTitle = "Album-Title";
        public string Artist = "Artist";
        public GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
        public string Title = "Title";

        public event PropertyChangedEventHandler PropertyChanged;

        private void SaveOrDeleteSettings()
        {
            if(_KeepSaved)
            {
                var savedSettings = ViewModel.Instance.SavedSessionSettings
                    .FirstOrDefault(s => s.SessionId == Session.Id);
                if(savedSettings != null)
                {
                    savedSettings.ShowTitle = _ShowTitle;
                    savedSettings.AutoSwitch = _AutoSwitch;
                    savedSettings.ShowArtist = _ShowArtist;
                    savedSettings.IsVideo = _IsVideo;
                    savedSettings.KeepSaved = _KeepSaved;
                } else
                {
                    ViewModel.Instance.SavedSessionSettings
                        .Add(
                            new MediaSessionSettings
                            {
                                SessionId = Session.Id,
                                ShowTitle = _ShowTitle,
                                AutoSwitch = _AutoSwitch,
                                ShowArtist = _ShowArtist,
                                IsVideo = _IsVideo,
                                KeepSaved = _KeepSaved
                            });
                }
            } else
            {
                var savedSettings = ViewModel.Instance.SavedSessionSettings
                    .FirstOrDefault(s => s.SessionId == Session.Id);
                if(savedSettings != null)
                {
                    ViewModel.Instance.SavedSessionSettings.Remove(savedSettings);
                }
            }
        }

        private void UpdateFriendlyAppName()
        {
            string id = Session.Id;
            try
            {
                if(!id.Contains('.') && !id.Contains('!') && char.IsUpper(id[0]))
                {
                    FriendlyAppName = id;
                } else
                {
                    if(id.Contains('!'))
                    {
                        id = id.Split('!')[1];
                    }

                    if(id.Contains(".exe"))
                    {
                        id = Path.GetFileNameWithoutExtension(id);
                    }
                    FriendlyAppName = id;
                }
            } catch(Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
                FriendlyAppName = id;
            }
        }

        protected void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }

        public bool AutoSwitch
        {
            get { return _AutoSwitch; }
            set
            {
                _AutoSwitch = value;
                NotifyPropertyChanged(nameof(AutoSwitch));
                SaveOrDeleteSettings();
            }
        }

        public string FriendlyAppName { get; private set; }

        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                NotifyPropertyChanged(nameof(IsActive));
            }
        }

        public bool IsVideo
        {
            get { return _IsVideo; }
            set
            {
                _IsVideo = value;
                NotifyPropertyChanged(nameof(IsVideo));
                SaveOrDeleteSettings();
            }
        }

        private bool _TimePeekEnabled = false;

        public bool TimePeekEnabled
        {
            get { return _TimePeekEnabled; }
            set
            {
                if (_TimePeekEnabled != value)
                {
                    _TimePeekEnabled = value;
                    NotifyPropertyChanged(nameof(TimePeekEnabled));
                }
            }
        }



        public bool IsLiveTime
        {
            get { return FullTime >= TimeSpan.FromHours(14); }
        }
        public int TimePosition
        {
            get
            {
                if (FullTime.TotalMilliseconds == 0) return 0;
                return (int)((CurrentTime.TotalMilliseconds / FullTime.TotalMilliseconds) * 100);
            }
        }


        private TimeSpan _CurrentTime = new TimeSpan(0, 0, 0);

        public TimeSpan CurrentTime
        {
            get { return _CurrentTime; }
            set
            {
                if (_CurrentTime != value)
                {
                    _CurrentTime = value;
                    NotifyPropertyChanged(nameof(CurrentTime));
                    NotifyPropertyChanged(nameof(TimePosition));
                }
            }
        }


        private TimeSpan _FullTime = new TimeSpan(0, 0, 0);

        public TimeSpan FullTime
        {
            get { return _FullTime; }
            set
            {
                if (_FullTime != value)
                {
                    _FullTime = value;
                    NotifyPropertyChanged(nameof(FullTime));
                    NotifyPropertyChanged(nameof(TimePosition));
                }
            }
        }

        public bool KeepSaved
        {
            get { return _KeepSaved; }
            set
            {
                _KeepSaved = value;
                NotifyPropertyChanged(nameof(KeepSaved));
                SaveOrDeleteSettings();
            }
        }

        public MediaSession Session
        {
            get { return session; }
            set
            {
                session = value;
                UpdateFriendlyAppName();
            }
        }

        public bool ShowArtist
        {
            get { return _ShowArtist; }
            set
            {
                _ShowArtist = value;
                NotifyPropertyChanged(nameof(ShowArtist));
                SaveOrDeleteSettings();
            }
        }

        public bool ShowTitle
        {
            get { return _ShowTitle; }
            set
            {
                _ShowTitle = value;
                NotifyPropertyChanged(nameof(ShowTitle));
                SaveOrDeleteSettings();
            }
        }

        public bool TimeoutRestore
        {
            get { return _TimeoutRestore; }
            set
            {
                _TimeoutRestore = value;
                NotifyPropertyChanged(nameof(TimeoutRestore));
            }
        }
    }

    public class MediaSessionSettings
    {
        public bool AutoSwitch { get; set; }

        public bool IsVideo { get; set; }

        public bool KeepSaved { get; set; }

        public string SessionId { get; set; }

        public bool ShowArtist { get; set; }

        public bool ShowTitle { get; set; }
    }
}
