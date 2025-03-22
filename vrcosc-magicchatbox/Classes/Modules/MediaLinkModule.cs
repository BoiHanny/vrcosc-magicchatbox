using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using Windows.Media.Control;
using WindowsMediaController;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Classes.Modules;

public class MediaLinkModule
{
    private static MediaSession? currentSession = null;
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(ViewModel.Instance.MediaSession_Timeout);
    // this is the main MediaManager object that will be used to get all the media sessions
    private static MediaManager? mediaManager = null;
    private static ConcurrentDictionary<string, (MediaSessionInfo, DateTime)> recentlyClosedSessions = new ConcurrentDictionary<string, (MediaSessionInfo, DateTime)>(
        );

    // this is a lookup of all sessions that have been opened by the MediaManager and their associated info
    private static ConcurrentDictionary<MediaSession, MediaSessionInfo> sessionInfoLookup = new ConcurrentDictionary<MediaSession, MediaSessionInfo>(
        );


    // this is the main function that will be called when a new media session is opened
    public MediaLinkModule(bool shouldStart)
    {
        ViewModel.Instance.PropertyChanged += ViewModel_PropertyChanged;
        if (shouldStart)
            Start();
    }

    // this function is used to automatically switch to a media session if the user has the auto switch setting enabled and the media session is playing and the media session is set to auto switch
    private static void AutoSwitchMediaSession(MediaSessionInfo sessionInfo)
    {
        if (ViewModel.Instance.MediaSession_AutoSwitch &&
            sessionInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
            sessionInfo.AutoSwitch)
        {
            foreach (var item in ViewModel.Instance.MediaSessions)
            {
                if (item.Session.Id == sessionInfo.Session.Id)
                {
                    item.IsActive = true;
                }
                else
                {
                    item.IsActive = false;
                }
            }
        }
    }

    // this funtion will clean up any expired sessions that have been closed for longer than the grace period.
    private static void CleanupExpiredSessions()
    {
        var expiredSessions = recentlyClosedSessions
            .Where(kvp => DateTime.Now - kvp.Value.Item2 > GracePeriod)
            .ToList();

        foreach (var expiredSession in expiredSessions)
        {
            recentlyClosedSessions.TryRemove(expiredSession.Key, out _);
        }
    }

    private static TimeSpan GetSeekTime(MediaSessionInfo mediaSessionInfo, double progressbarValue)
    {
        MediaSession S = mediaSessionInfo.Session;

        if (S == null)
            return TimeSpan.Zero;

        TimeSpan FullMediaTime = S.ControlSession.GetTimelineProperties().EndTime - S.ControlSession.GetTimelineProperties().StartTime;

        double requestedPositionSeconds = FullMediaTime.TotalSeconds * progressbarValue / 100;

        return TimeSpan.FromSeconds(requestedPositionSeconds);
    }

    // this function will be called when the user changes the media properties of a media session
    private static void MediaManager_OnAnyMediaPropertyChanged(
        MediaSession sender,
        GlobalSystemMediaTransportControlsSessionMediaProperties args)
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

    // this function will be called when the user changes the playback state of a media session
    private static void MediaManager_OnAnyPlaybackStateChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
    {
        try
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);
            if (sessionInfo != null)
            {
                // Update the playback status.
                sessionInfo.PlaybackStatus = args.PlaybackStatus;

                // If necessary, update the CurrentTime.
                // This depends on how you decide to handle time updates.
                // For example, you might reset the CurrentTime when playback stops.
                if (args.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    sessionInfo.CurrentTime = TimeSpan.Zero;
                }

                // If the session is playing, you might want to adjust CurrentTime based on other properties.
                // This part is optional and depends on your specific requirements.
                if (args.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    // Potentially update CurrentTime based on args (if needed)
                }

                // Handle any additional logic such as auto-switching media session.
                AutoSwitchMediaSession(sessionInfo);
            }
        }
        catch (Exception ex)
        {
            // Log the exception as per your logging mechanism.
            Logging.WriteException(ex, MSGBox: false);
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

            Application.Current.Dispatcher
                .Invoke(
                    () =>
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

    // this fucntion will be called whe the user opens a new media session.
    private static void MediaManager_OnAnySessionOpened(MediaSession session)
    {
        MediaSessionInfo sessionInfo = null;

        if (recentlyClosedSessions.TryGetValue(session.Id, out var recentSessionInfo))
        {
            var (recentInfo, closeTime) = recentSessionInfo;

            if (DateTime.Now - closeTime <= GracePeriod)
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

        Application.Current.Dispatcher
            .Invoke(
                () =>
                {
                    ViewModel.Instance.MediaSessions.Add(sessionInfo);
                });

        sessionInfoLookup[session] = sessionInfo;
        currentSession = session;
        SessionRestore(sessionInfo);
    }

    // this function will be  called when the user changes the focused media session
    private static void MediaManager_OnFocusedSessionChanged(MediaSession session) { currentSession = session; }

    // this funion will be called when the user changes the setting to enable/disable the media link
    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IntgrScanMediaLink" ||
            e.PropertyName == "IntgrMediaLink_VR" ||
            e.PropertyName == "IntgrMediaLink_DESKTOP" ||
            e.PropertyName == "IsVRRunning")
        {
            if (ViewModel.Instance.IntgrScanMediaLink &&
                ViewModel.Instance.IntgrMediaLink_VR &&
                ViewModel.Instance.IsVRRunning ||
                ViewModel.Instance.IntgrScanMediaLink &&
                ViewModel.Instance.IntgrMediaLink_DESKTOP &&
                !ViewModel.Instance.IsVRRunning)
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
            mediaManager.OnAnyTimelinePropertyChanged -= MediaManager_OnAnyTimelinePropertyChanged;

            mediaManager.Dispose();
            ViewModel.Instance.MediaSessions.Clear();
            mediaManager = null;
        }
    }

    public static void MediaManager_NextAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        S?.ControlSession.TrySkipNextAsync();
    }

    public static void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args)
    {
        var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

        if (sessionInfo != null)
        {
            if (args.StartTime != TimeSpan.Zero || args.Position != TimeSpan.Zero)
            {
                sessionInfo.FullTime = args.EndTime - args.StartTime;
                sessionInfo.CurrentTime = args.Position;
                sessionInfo.TimePeekEnabled = true;
            }
            else
            {
                sessionInfo.TimePeekEnabled = false;
            }
        }
    }

    public static void MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        S?.ControlSession.TryTogglePlayPauseAsync();
    }

    public static async Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;
        S?.ControlSession.TrySkipPreviousAsync();
        if (sessionInfo.CurrentTime > TimeSpan.FromSeconds(2))
        {
            S?.ControlSession.TrySkipPreviousAsync();
        }

    }




    public static async Task MediaManager_SeekTo(MediaSessionInfo sessionInfo, double position)
    {
        MediaSession S = sessionInfo.Session;

        TimeSpan requestedtime = GetSeekTime(sessionInfo, position);

        long requestedPlaybackPosition = requestedtime.Ticks;

        if (S == null)
            return;

        //get the currentplayback position
        long currentPlaybackPosition = S.ControlSession.GetTimelineProperties().Position.Ticks;

        await S.ControlSession.TryChangePlaybackPositionAsync(requestedPlaybackPosition);
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
            mediaManager.OnAnyTimelinePropertyChanged += MediaManager_OnAnyTimelinePropertyChanged;
            mediaManager.Start();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }



    public enum MediaLinkTimeSeekbar
    {
        [Description("Small numbers")]
        SmallNumbers,
        [Description("Custom")]
        NumbersAndSeekBar,
        [Description("None")]
        None
    }


    public class MediaLinkStyle : INotifyPropertyChanged
    {
        private bool displayTime = true;
        private string filledCharacter = string.Empty;
        private int id = 0;
        private string middleCharacter = string.Empty;
        private string nonFilledCharacter = string.Empty;
        private int progressBarLength = 8;
        private bool progressBarOnTop = true;
        private bool showTimeInSuperscript = true;
        private bool spaceAgainObjects = true;
        private bool spaceBetweenPreSuffixAndTime = false;
        private bool systemDefault = false;
        private string timePrefix = string.Empty;
        private bool timePreSuffixOnTheInside = true;
        private string timeSuffix = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(StyleName));
            return true;
        }

        public bool DisplayTime
        {
            get => displayTime;
            set => SetProperty(ref displayTime, value);
        }

        public string FilledCharacter
        {
            get => filledCharacter;
            set => SetProperty(ref filledCharacter, value);
        }

        public int ID
        {
            get => id;
            set => SetProperty(ref id, value);
        }

        public string MiddleCharacter
        {
            get => middleCharacter;
            set => SetProperty(ref middleCharacter, value);
        }

        public string NonFilledCharacter
        {
            get => nonFilledCharacter;
            set => SetProperty(ref nonFilledCharacter, value);
        }

        public int ProgressBarLength
        {
            get => progressBarLength;
            set => SetProperty(ref progressBarLength, value);
        }

        public bool ProgressBarOnTop
        {
            get => progressBarOnTop;
            set => SetProperty(ref progressBarOnTop, value);
        }

        public bool ShowTimeInSuperscript
        {
            get => showTimeInSuperscript;
            set => SetProperty(ref showTimeInSuperscript, value);
        }


        public bool SpaceAgainObjects
        {
            get => spaceAgainObjects;
            set => SetProperty(ref spaceAgainObjects, value);
        }

        public bool SpaceBetweenPreSuffixAndTime
        {
            get => spaceBetweenPreSuffixAndTime;
            set => SetProperty(ref spaceBetweenPreSuffixAndTime, value);
        }

        public string StyleName
        {
            get
            {
                try
                {
                    double percentage = 80.0;
                    string currentTime = "4:20";
                    string fullTime = "69:00";

                    if (ShowTimeInSuperscript)
                    {
                        currentTime = DataController.TransformToSuperscript(currentTime);
                        fullTime = DataController.TransformToSuperscript(fullTime);
                    }

                    int totalBlocks = ProgressBarLength;
                    int filledBlocks = (int)(percentage / (100.0 / totalBlocks));

                    // Ensure characters are not empty or null before accessing
                    string filledChar = !string.IsNullOrEmpty(FilledCharacter) ? FilledCharacter : "?";
                    string nonFilledChar = !string.IsNullOrEmpty(NonFilledCharacter) ? NonFilledCharacter : "⁉️";
                    string middleChar = !string.IsNullOrEmpty(MiddleCharacter) ? MiddleCharacter : "?";

                    // Use the entire string for emojis and multi-character strings
                    string filledBar = string.Concat(Enumerable.Repeat(filledChar, filledBlocks));
                    string emptyBar = string.Concat(Enumerable.Repeat(nonFilledChar, totalBlocks - filledBlocks));
                    string progressBar = filledBar + middleChar + emptyBar;

                    string timeSegment;
                    string space = SpaceAgainObjects ? " " : string.Empty;

                    if (DisplayTime)
                    {
                        if (TimePreSuffixOnTheInside)
                        {
                            string preSuffixSpace = SpaceBetweenPreSuffixAndTime ? " " : string.Empty;
                            timeSegment = string.IsNullOrWhiteSpace(TimePrefix) || string.IsNullOrWhiteSpace(TimeSuffix) ?
                                $"{currentTime}{space}{progressBar}{space}{fullTime}" :
                                $"{currentTime}{preSuffixSpace}{TimePrefix}{progressBar}{TimeSuffix}{preSuffixSpace}{fullTime}";
                        }
                        else
                        {
                            string preSuffixSpace = SpaceBetweenPreSuffixAndTime ? " " : string.Empty;
                            timeSegment = $"{TimePrefix}{preSuffixSpace}{currentTime}{space}{progressBar}{space}{fullTime}{preSuffixSpace}{TimeSuffix}";
                        }
                    }
                    else
                    {
                        string preSuffixSpace = SpaceBetweenPreSuffixAndTime ? " " : string.Empty;
                        timeSegment = TimePreSuffixOnTheInside ?
                            $"{TimePrefix}{progressBar}{TimeSuffix}" :
                            $"{TimePrefix}{preSuffixSpace}{progressBar}{preSuffixSpace}{TimeSuffix}";
                    }

                    if (SystemDefault)
                    {
                        timeSegment += " | 🔒 Built-in";
                    }
                    else
                    {
                        timeSegment += " | 🎨 Custom";
                    }

                    return timeSegment;
                }
                catch (IndexOutOfRangeException ex)
                {
                    Logging.WriteException(ex, MSGBox: false);
                    return "Error generating style name.";
                }
                catch (Exception ex)
                {
                    Logging.WriteException(ex, MSGBox: false);
                    return "Unexpected error generating style name.";
                }
            }
        }

        public bool SystemDefault
        {
            get => systemDefault;
            set => SetProperty(ref systemDefault, value);
        }

        public string TimePrefix
        {
            get => timePrefix;
            set => SetProperty(ref timePrefix, value);
        }

        public bool TimePreSuffixOnTheInside
        {
            get => timePreSuffixOnTheInside;
            set => SetProperty(ref timePreSuffixOnTheInside, value);
        }

        public string TimeSuffix
        {
            get => timeSuffix;
            set => SetProperty(ref timeSuffix, value);
        }
    }

}
