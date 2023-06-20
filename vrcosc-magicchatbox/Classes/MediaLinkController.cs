using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        // this is the main MediaManager object that will be used to get all the media sessions
        private static MediaManager? mediaManager = null;
        private static MediaSession? currentSession = null;

        // this is a lookup of all sessions that have been opened by the MediaManager and their associated info
        private static ConcurrentDictionary<MediaSession, MediaSessionInfo> sessionInfoLookup = new ConcurrentDictionary<MediaSession, MediaSessionInfo>();
        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(ViewModel.Instance.MediaSession_Timeout);
        private static ConcurrentDictionary<string, (MediaSessionInfo, DateTime)> recentlyClosedSessions = new ConcurrentDictionary<string, (MediaSessionInfo, DateTime)>();


        // this is the main function that will be called when a new media session is opened
        public MediaLinkController(bool shouldStart)
        {
            ViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
            if (shouldStart)
                Start();
        }

        // this funion will be called when the user changes the setting to enable/disable the media link
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IntgrScanMediaLink")
            {
                ViewModel.Instance.MediaSessions.Clear();
                if (ViewModel.Instance.IntgrScanMediaLink)
                    Start();
                else
                    Dispose();


            }
        }


        public static void Start()
        {
            try
            {
                mediaManager = new MediaManager();
                mediaManager.OnAnySessionOpened += MediaManager_OnAnySessionOpened;
                mediaManager.OnAnySessionClosed += MediaManager_OnAnySessionClosed;
                mediaManager.OnFocusedSessionChanged += MediaManager_OnFocusedSessionChanged;
                mediaManager.OnAnyPlaybackStateChanged += MediaManager_OnAnyPlaybackStateChanged;
                mediaManager.OnAnyMediaPropertyChanged += MediaManager_OnAnyMediaPropertyChanged;
                mediaManager.Start();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }


        public static void Dispose()
        {
            if (mediaManager != null)
            {
                mediaManager.OnAnySessionOpened -= MediaManager_OnAnySessionOpened;
                mediaManager.OnAnySessionClosed -= MediaManager_OnAnySessionClosed;
                mediaManager.OnFocusedSessionChanged -= MediaManager_OnFocusedSessionChanged;
                mediaManager.OnAnyPlaybackStateChanged -= MediaManager_OnAnyPlaybackStateChanged;
                mediaManager.OnAnyMediaPropertyChanged -= MediaManager_OnAnyMediaPropertyChanged;

                mediaManager.Dispose();
                mediaManager = null;
            }
        }


        private static void MediaManager_OnAnySessionOpened(MediaSession session)
        {
            MediaSessionInfo sessionInfo = null;

            if (recentlyClosedSessions.TryGetValue(session.Id, out var recentSessionInfo))
            {
                var (recentInfo, closeTime) = recentSessionInfo;

                if ((DateTime.Now - closeTime) <= GracePeriod)
                {
                    sessionInfo = recentInfo;
                    sessionInfo.Session = session;
                    recentlyClosedSessions.TryRemove(session.Id, out _);
                }
            }

            if (sessionInfo == null)
            {
                sessionInfo = new MediaSessionInfo { Session = session };
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ViewModel.Instance.MediaSessions.Add(sessionInfo);
            });

            sessionInfoLookup[session] = sessionInfo;
            currentSession = session;
        }

        private static async void MediaManager_OnAnySessionClosed(MediaSession session)
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(session);

            if (sessionInfo != null)
            {
                recentlyClosedSessions[session.Id] = (sessionInfo, DateTime.Now);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.MediaSessions.Remove(sessionInfo);
                });

                sessionInfoLookup.TryRemove(session, out _);
            }

            if (currentSession == session)
            {
                currentSession = null;
            }

            await Task.Run(() => CleanupExpiredSessions());
        }

        private static void CleanupExpiredSessions()
        {
            var expiredSessions = recentlyClosedSessions
                .Where(kvp => (DateTime.Now - kvp.Value.Item2) > GracePeriod)
                .ToList();

            foreach (var expiredSession in expiredSessions)
            {
                recentlyClosedSessions.TryRemove(expiredSession.Key, out _);
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

                var playbackStatus = sender.ControlSession.GetPlaybackInfo().PlaybackStatus;
                sessionInfo.PlaybackStatus = playbackStatus;
            }
        }

    }
}
