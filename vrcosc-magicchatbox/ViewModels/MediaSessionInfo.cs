using System.ComponentModel;
using System.Globalization;
using Windows.Media;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.ViewModels
{
    public class MediaSessionInfo : INotifyPropertyChanged
    {
        private MediaSession session;

        public MediaSession Session
        {
            get { return session; }
            set
            {
                session = value;
                UpdateFriendlyAppName();
            }
        }


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

        private bool _IsVideo;
        public bool IsVideo
        {
            get { return _IsVideo; }
            set
            {
                _IsVideo = value;
                NotifyPropertyChanged(nameof(IsVideo));
            }
        }


        private bool _ShowArtist = true;
        public bool ShowArtist
        {
            get { return _ShowArtist; }
            set
            {
                _ShowArtist = value;
                NotifyPropertyChanged(nameof(ShowArtist));
            }
        }


        private bool _ShowTitle = true;
        public bool ShowTitle
        {
            get { return _ShowTitle; }
            set
            {
                _ShowTitle = value;
                NotifyPropertyChanged(nameof(ShowTitle));
            }
        }

        public string AlbumArtist = "Album-Artist";
        public string AlbumTitle = "Album-Title";
        public string Artist = "Artist";
        public string Title = "Title";
        public GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

        public string FriendlyAppName { get; private set; }

        private void UpdateFriendlyAppName()
        {
            string id = Session.Id;

            // If the Id is already user-friendly, use it directly
            if (!id.Contains('.') && !id.Contains('!') && char.IsUpper(id[0]))
            {
                FriendlyAppName = id;
            }
            else
            {
                // If the Id contains a '!', take the part after the '!'
                if (id.Contains('!'))
                {
                    id = id.Substring(id.IndexOf('!') + 1);
                }

                FriendlyAppName = id;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
