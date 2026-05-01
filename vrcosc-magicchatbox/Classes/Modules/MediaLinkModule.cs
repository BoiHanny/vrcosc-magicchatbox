using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;
using WindowsMediaController;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Manages Windows media sessions and exposes playback state and metadata for the VRChat chatbox.
/// </summary>
public class MediaLinkModule : vrcosc_magicchatbox.Services.IMediaLinkService
{
    private readonly IAppState _appState;
    private readonly MediaLinkDisplayState _mediaLink;

    private readonly IntegrationSettings _integrationSettings;
    private readonly MediaLinkSettings _mediaLinkSettings;
    private readonly IUiDispatcher _dispatcher;
    private readonly IPrivacyConsentService _consentService;
    private readonly IToastService? _toast;

    private MediaSession? currentSession = null;
    private TimeSpan GracePeriod => TimeSpan.FromSeconds(_mediaLinkSettings.SessionTimeout);
    private MediaManager? mediaManager = null;
    private ConcurrentDictionary<string, (MediaSessionInfo, DateTime)> recentlyClosedSessions = new ConcurrentDictionary<string, (MediaSessionInfo, DateTime)>(
        );

    private ConcurrentDictionary<MediaSession, MediaSessionInfo> sessionInfoLookup = new ConcurrentDictionary<MediaSession, MediaSessionInfo>(
        );

    public DateTime LastMediaChangeTime { get; private set; } = DateTime.UtcNow;


    /// <summary>
    /// Initializes the module, wires property-change listeners, and optionally starts the media manager.
    /// </summary>
    public MediaLinkModule(
        bool shouldStart,
        IPrivacyConsentService consentService,
        IAppState appState,
        MediaLinkDisplayState mediaLink,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<MediaLinkSettings> mediaLinkSettingsProvider,
        IUiDispatcher dispatcher,
        IToastService? toast = null)
    {
        _appState = appState;
        _mediaLink = mediaLink;
        _integrationSettings = integrationSettingsProvider.Value;
        _mediaLinkSettings = mediaLinkSettingsProvider.Value;
        _dispatcher = dispatcher;
        _consentService = consentService;
        _toast = toast;
        _appState.PropertyChanged += ViewModel_PropertyChanged;
        _integrationSettings.PropertyChanged += ViewModel_PropertyChanged;

        _consentService.ConsentChanged += (_, e) =>
        {
            if (e.Hook == PrivacyHook.MediaSession)
            {
                if (e.NewState == ConsentState.Approved)
                {
                    if (shouldStart && mediaManager == null)
                        Start();
                }
                else if (e.NewState == ConsentState.Denied)
                {
                    Dispose();
                    _toast?.Show("🔒 Media Session", "Media session access paused — privacy consent revoked.", ToastType.Privacy, key: "medialink-privacy-denied");
                }
            }
        };

        if (shouldStart && _consentService.IsApproved(PrivacyHook.MediaSession))
            Start();
    }

    private void AutoSwitchMediaSession(MediaSessionInfo sessionInfo)
    {
        if (_mediaLinkSettings.AutoSwitch &&
            sessionInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
            sessionInfo.AutoSwitch)
        {
            foreach (var item in _mediaLink.MediaSessions)
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

    private void CleanupExpiredSessions()
    {
        var expiredSessions = recentlyClosedSessions
            .Where(kvp => DateTime.Now - kvp.Value.Item2 > GracePeriod)
            .ToList();

        foreach (var expiredSession in expiredSessions)
        {
            recentlyClosedSessions.TryRemove(expiredSession.Key, out _);
        }
    }

    private TimeSpan GetSeekTime(MediaSessionInfo mediaSessionInfo, double progressbarValue)
    {
        MediaSession S = mediaSessionInfo.Session;

        if (S == null)
            return TimeSpan.Zero;

        TimeSpan FullMediaTime = S.ControlSession.GetTimelineProperties().EndTime - S.ControlSession.GetTimelineProperties().StartTime;

        double requestedPositionSeconds = FullMediaTime.TotalSeconds * progressbarValue / 100;

        return TimeSpan.FromSeconds(requestedPositionSeconds);
    }

    private void MediaManager_OnAnyMediaPropertyChanged(
        MediaSession sender,
        GlobalSystemMediaTransportControlsSessionMediaProperties args)
    {
        var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

        if (sessionInfo != null)
        {
            if (!string.Equals(sessionInfo.Title, args.Title, StringComparison.Ordinal) ||
                !string.Equals(sessionInfo.Artist, args.Artist, StringComparison.Ordinal))
            {
                LastMediaChangeTime = DateTime.UtcNow;
            }

            sessionInfo.AlbumArtist = args.AlbumArtist;
            sessionInfo.AlbumTitle = args.AlbumTitle;
            sessionInfo.Artist = args.Artist;
            sessionInfo.Title = args.Title;

            var playbackStatus = sender.ControlSession.GetPlaybackInfo().PlaybackStatus;
            sessionInfo.PlaybackStatus = playbackStatus;

            AutoSwitchMediaSession(sessionInfo);
        }
    }

    private void MediaManager_OnAnyPlaybackStateChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
    {
        try
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);
            if (sessionInfo != null)
            {
                if (sessionInfo.PlaybackStatus != args.PlaybackStatus)
                {
                    LastMediaChangeTime = DateTime.UtcNow;
                }

                sessionInfo.PlaybackStatus = args.PlaybackStatus;

                if (args.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    sessionInfo.CurrentTime = TimeSpan.Zero;
                }

                if (args.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                }

                AutoSwitchMediaSession(sessionInfo);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }



    private async void MediaManager_OnAnySessionClosed(MediaSession session)
    {
        try
        {
            var sessionInfo = sessionInfoLookup.GetValueOrDefault(session);

            if (sessionInfo != null)
            {
                sessionInfo.TimeoutRestore = true;
                recentlyClosedSessions[session.Id] = (sessionInfo, DateTime.Now);

                _dispatcher.BeginInvoke(
                        () =>
                        {
                            _mediaLink.MediaSessions.Remove(sessionInfo);
                        });

                sessionInfoLookup.TryRemove(session, out _);
            }

            if (currentSession == session)
            {
                currentSession = null;
            }

            await Task.Run(() => CleanupExpiredSessions());
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error handling media session closed: {ex.Message}");
        }
    }

    private void MediaManager_OnAnySessionOpened(MediaSession session)
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
            sessionInfo = new MediaSessionInfo(_mediaLinkSettings, _mediaLink) { Session = session };
        }

        _dispatcher.BeginInvoke(
                () =>
                {
                    _mediaLink.MediaSessions.Add(sessionInfo);
                });

        sessionInfoLookup[session] = sessionInfo;
        currentSession = session;
        SessionRestore(sessionInfo);
        LastMediaChangeTime = DateTime.UtcNow;
    }

    private void MediaManager_OnFocusedSessionChanged(MediaSession session) { currentSession = session; }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IntgrScanMediaLink" ||
            e.PropertyName == "IntgrMediaLink_VR" ||
            e.PropertyName == "IntgrMediaLink_DESKTOP" ||
            e.PropertyName == "IsVRRunning")
        {
            if (_integrationSettings.IntgrScanMediaLink &&
                _integrationSettings.IntgrMediaLink_VR &&
                _appState.IsVRRunning ||
                _integrationSettings.IntgrScanMediaLink &&
                _integrationSettings.IntgrMediaLink_DESKTOP &&
                !_appState.IsVRRunning)
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

    /// <summary>
    /// Unsubscribes all event handlers and disposes the underlying <see cref="MediaManager"/> instance.
    /// </summary>
    public void Dispose()
    {
        _appState.PropertyChanged -= ViewModel_PropertyChanged;
        _integrationSettings.PropertyChanged -= ViewModel_PropertyChanged;

        if (mediaManager != null)
        {
            mediaManager.OnAnySessionOpened -= MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed -= MediaManager_OnAnySessionClosed;
            mediaManager.OnFocusedSessionChanged -= MediaManager_OnFocusedSessionChanged;
            mediaManager.OnAnyPlaybackStateChanged -= MediaManager_OnAnyPlaybackStateChanged;
            mediaManager.OnAnyMediaPropertyChanged -= MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.OnAnyTimelinePropertyChanged -= MediaManager_OnAnyTimelinePropertyChanged;

            mediaManager.Dispose();
            _mediaLink.MediaSessions.Clear();
            mediaManager = null;
        }
    }

    public void MediaManager_NextAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        S?.ControlSession.TrySkipNextAsync();
    }

    /// <summary>
    /// Updates the current session's timeline properties (position and duration) from the Windows media API.
    /// </summary>
    public void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args)
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

    public void MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        S?.ControlSession.TryTogglePlayPauseAsync();
    }

    public async Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo)
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




    public async Task MediaManager_SeekTo(MediaSessionInfo sessionInfo, double position)
    {
        MediaSession S = sessionInfo.Session;

        TimeSpan requestedtime = GetSeekTime(sessionInfo, position);

        long requestedPlaybackPosition = requestedtime.Ticks;

        if (S == null)
            return;

        long currentPlaybackPosition = S.ControlSession.GetTimelineProperties().Position.Ticks;

        await S.ControlSession.TryChangePlaybackPositionAsync(requestedPlaybackPosition);
    }








    /// <summary>
    /// Restores persisted per-session display settings (title, artist, auto-switch, etc.) onto <paramref name="session"/>.
    /// </summary>
    public void SessionRestore(MediaSessionInfo session)
    {
        MediaSessionSettings savedSettings = new MediaSessionSettings();

        MediaSessionSettings matchingSettings = _mediaLink.SavedSessionSettings
            .FirstOrDefault(s => s.SessionId == session.Session.Id);

        if (matchingSettings != null)
        {
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

    /// <summary>
    /// Starts the <see cref="MediaManager"/> and subscribes to all media session events.
    /// </summary>
    public void Start()
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



    /// <summary>
    /// Defines the progress-bar visual style (characters, length, time format) used by the media display.
    /// </summary>
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
                    string timeSegment = SeekbarUtilities.CreateProgressBar(
                        percentage,
                        TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(20)),
                        TimeSpan.FromMinutes(69),
                        new SeekbarStyleOptions
                        {
                            DisplayTime = DisplayTime,
                            FilledCharacter = !string.IsNullOrEmpty(FilledCharacter) ? FilledCharacter : "?",
                            MiddleCharacter = !string.IsNullOrEmpty(MiddleCharacter) ? MiddleCharacter : "?",
                            NonFilledCharacter = !string.IsNullOrEmpty(NonFilledCharacter) ? NonFilledCharacter : "⁉️",
                            ProgressBarLength = ProgressBarLength,
                            ShowTimeInSuperscript = ShowTimeInSuperscript,
                            SpaceAgainObjects = SpaceAgainObjects,
                            SpaceBetweenPreSuffixAndTime = SpaceBetweenPreSuffixAndTime,
                            TimePrefix = TimePrefix,
                            TimePreSuffixOnTheInside = TimePreSuffixOnTheInside,
                            TimeSuffix = TimeSuffix
                        });

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
