using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using Windows.Media.Control;
using WindowsMediaController;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Classes
{
    public class MediaLinkController
    {
        private static MediaManager? mediaManager = null;
        private static MediaSession? currentSession = null;

        public MediaLinkController()
        {
            ViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IntgrScanMediaLink")
            {
                if (ViewModel.Instance.IntgrScanMediaLink)
                    Start();
                else
                    Dispose();
            }
        }

        public static void Start()
        {
            mediaManager = new MediaManager();
            mediaManager.OnAnySessionOpened += MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed += MediaManager_OnAnySessionClosed;
            mediaManager.OnFocusedSessionChanged += MediaManager_OnFocusedSessionChanged;
            mediaManager.OnAnyPlaybackStateChanged += MediaManager_OnAnyPlaybackStateChanged;
            mediaManager.OnAnyMediaPropertyChanged += MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.Start();

            foreach (var session in mediaManager.CurrentMediaSessions.Values)
            {
                var playbackInfo = session.ControlSession.GetPlaybackInfo();
                MediaManager_OnAnyPlaybackStateChanged(session, playbackInfo);
            }
        }

        public static void Dispose()
        {
            if (mediaManager != null)
            {
                mediaManager.Dispose();
                mediaManager = null;
            }
        }

        private static void MediaManager_OnAnySessionOpened(MediaSession session)
        {
            var sessionInfo = new MediaSessionInfo
            {
                Session = session
            };

            ViewModel.Instance.MediaSessions.Add(sessionInfo);

            currentSession = session;
        }

        private static void MediaManager_OnAnySessionClosed(MediaSession session)
        {
            var sessionInfo = ViewModel.Instance.MediaSessions.FirstOrDefault(s => s.Session == session);

            if (sessionInfo != null)
            {
                ViewModel.Instance.MediaSessions.Remove(sessionInfo);
            }

            if (currentSession == session)
            {
                currentSession = null;
            }
        }

        private static void MediaManager_OnFocusedSessionChanged(MediaSession session)
        {
            currentSession = session;
        }

        private static void MediaManager_OnAnyPlaybackStateChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
        {
            try
            {
                var sessionInfo = ViewModel.Instance.MediaSessions.FirstOrDefault(s => s.Session == sender);

                if (sessionInfo != null)
                {
                    sessionInfo.PlaybackStatus = args.PlaybackStatus;
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        private static void MediaManager_OnAnyMediaPropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionMediaProperties args)
        {
            var sessionInfo = ViewModel.Instance.MediaSessions.FirstOrDefault(s => s.Session == sender);

            if (sessionInfo != null)
            {
                sessionInfo.AlbumArtist = args.AlbumArtist;
                sessionInfo.AlbumTitle = args.AlbumTitle;
                sessionInfo.Artist = args.Artist;
                sessionInfo.PlaybackType = (Windows.Media.MediaPlaybackType)args.PlaybackType;
                sessionInfo.Title = args.Title;

            }
        }
    }
}
