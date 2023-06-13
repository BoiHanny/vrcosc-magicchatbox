using System.Globalization;
using Windows.Media;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.ViewModels
{
    public class MediaSessionInfo
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

        public string AlbumArtist = "AlbumArtist";
        public string AlbumTitle = "AlbumTitle";
        public string Artist = "Artist";
        public MediaPlaybackType PlaybackType = MediaPlaybackType.Music;
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
    }
}
