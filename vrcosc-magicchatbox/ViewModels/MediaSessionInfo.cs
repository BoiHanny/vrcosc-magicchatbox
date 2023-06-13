using Windows.Media;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.ViewModels
{
    public class MediaSessionInfo
    {
        public MediaSession Session { get; set; }

        public string AlbumArtist = "AlbumArtist";
        public string AlbumTitle = "AlbumTitle";
        public string Artist = "Artist";
        public MediaPlaybackType PlaybackType = MediaPlaybackType.Music;
        public string Title = "Title";
        public GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
    }


}
