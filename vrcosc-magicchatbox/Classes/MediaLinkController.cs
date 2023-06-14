using System;
using System.Collections.Generic;
using System.Windows;
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
        private static Dictionary<MediaSession, MediaSessionInfo> sessionInfoLookup = new Dictionary<MediaSession, MediaSessionInfo>();

        public MediaLinkController(bool shouldStart)
        {
            ViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
            if (shouldStart)
                Start();
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
        }


        public static void Dispose()
        {
            if (mediaManager != null)
            {
                // Unsubscribe from the events
                mediaManager.OnAnySessionOpened -= MediaManager_OnAnySessionOpened;
                mediaManager.OnAnySessionClosed -= MediaManager_OnAnySessionClosed;
                mediaManager.OnFocusedSessionChanged -= MediaManager_OnFocusedSessionChanged;
                mediaManager.OnAnyPlaybackStateChanged -= MediaManager_OnAnyPlaybackStateChanged;
                mediaManager.OnAnyMediaPropertyChanged -= MediaManager_OnAnyMediaPropertyChanged;

                // Dispose the mediaManager
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                ViewModel.Instance.MediaSessions.Add(sessionInfo);
                });

            sessionInfoLookup[session] = sessionInfo;

            currentSession = session;
        }

        private static void MediaManager_OnAnySessionClosed(MediaSession session)
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(session);

            if (sessionInfo != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.MediaSessions.Remove(sessionInfo);
                    });
                sessionInfoLookup.Remove(session);
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
                var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

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
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

            if (sessionInfo != null)
            {
                sessionInfo.AlbumArtist = args.AlbumArtist;
                sessionInfo.AlbumTitle = args.AlbumTitle;
                sessionInfo.Artist = args.Artist;
                sessionInfo.Title = args.Title;

                // Get and update the PlaybackStatus
                var playbackStatus = sender.ControlSession.GetPlaybackInfo().PlaybackStatus;
                sessionInfo.PlaybackStatus = playbackStatus;
            }
        }

    }
}
