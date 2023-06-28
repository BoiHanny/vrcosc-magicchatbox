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
            if (e.PropertyName == "IntgrScanMediaLink" || e.PropertyName == "IntgrMediaLink_VR" || e.PropertyName == "IntgrMediaLink_DESKTOP" || e.PropertyName == "IsVRRunning")
            {
                if (ViewModel.Instance.IntgrScanMediaLink && ViewModel.Instance.IntgrMediaLink_VR && ViewModel.Instance.IsVRRunning || ViewModel.Instance.IntgrScanMediaLink && ViewModel.Instance.IntgrMediaLink_DESKTOP && !ViewModel.Instance.IsVRRunning)
                {
                    if (mediaManager == null)
                        Start();
                }
                else
                {
                    if (mediaManager != null)
                        Dispose();
                }
            }
        }

        // this function will start the media manager and subscribe to all the events that we need to listen to for media sessions
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

        // this function will stop the media manager and unsubscribe from all the events that we were listening to for media sessions
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
                ViewModel.Instance.MediaSessions.Clear();
                mediaManager = null;
            }
        }

        // this fucntion will be called whe the user opens a new media session.
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
            SessionRestore(sessionInfo);
        }

        public static void SessionRestore(MediaSessionInfo session)
        {
            MediaSessionSettings savedSettings = new MediaSessionSettings();

            MediaSessionSettings matchingSettings = ViewModel.Instance.SavedSessionSettings
                .FirstOrDefault(s => s.SessionId == session.Session.Id);

            if (matchingSettings != null)
            {
                // Copy the values from matchingSettings to savedSettings
                savedSettings.ShowTitle = matchingSettings.ShowTitle;
                savedSettings.AutoSwitch = matchingSettings.AutoSwitch;
                savedSettings.ShowArtist = matchingSettings.ShowArtist;
                savedSettings.IsVideo = matchingSettings.IsVideo;
                savedSettings.KeepSaved = matchingSettings.KeepSaved;

                if (savedSettings != null && !session.TimeoutRestore)
                {
                    session.ShowTitle = savedSettings.ShowTitle;
                    session.AutoSwitch = savedSettings.AutoSwitch;
                    session.ShowArtist = savedSettings.ShowArtist;
                    session.IsVideo = savedSettings.IsVideo;
                    session.KeepSaved = savedSettings.KeepSaved;
                }
            }
        }


        // this function will be called when the user closes a media session, we temporarily store the session info in a dictionary so we can restore it if the user reopens the session within a certain time period
        private static async void MediaManager_OnAnySessionClosed(MediaSession session)
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(session);

            if (sessionInfo != null)
            {
                sessionInfo.TimeoutRestore = true;
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

        // this funtion will clean up any expired sessions that have been closed for longer than the grace period.
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

        // this function will be  called when the user changes the focused media session
        private static void MediaManager_OnFocusedSessionChanged(MediaSession session)
        {
            currentSession = session;
        }

        // this function will be called when the user changes the playback state of a media session
        private static void MediaManager_OnAnyPlaybackStateChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
        {
            try
            {
                var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

                if (sessionInfo != null)
                {
                    sessionInfo.PlaybackStatus = args.PlaybackStatus;
                    AutoSwitchMediaSession(sessionInfo);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }

        // this function will be called when the user changes the media properties of a media session
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

                AutoSwitchMediaSession(sessionInfo);

            }
        }

        // this function is used to automatically switch to a media session if the user has the auto switch setting enabled and the media session is playing and the media session is set to auto switch
        private static void AutoSwitchMediaSession(MediaSessionInfo sessionInfo)
        {
            if (ViewModel.Instance.MediaSession_AutoSwitch && sessionInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && sessionInfo.AutoSwitch)
            {
                foreach (var item in ViewModel.Instance.MediaSessions)
                {
                    if (item.Session.Id == sessionInfo.Session.Id)
                    {
                        item.IsActive = true;
                    }
                }
            }
        }

    }
}
