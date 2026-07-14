using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
    private readonly EventHandler<ConsentChangedEventArgs> _consentChangedHandler;

    private MediaSession? currentSession = null;
    private TimeSpan GracePeriod => TimeSpan.FromSeconds(_mediaLinkSettings.SessionTimeout);
    private MediaManager? mediaManager = null;
    private static readonly TimeSpan MediaSnapshotResyncInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TimelineRefreshAfterMediaChangeDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan TimelineBackwardDriftTolerance = TimeSpan.FromMilliseconds(1250);
    private static readonly TimeSpan TimelineBackwardJumpThreshold = TimeSpan.FromSeconds(5);
    private Timer? mediaSnapshotResyncTimer;
    private int mediaSnapshotResyncInProgress;
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

        _consentChangedHandler = (_, e) =>
        {
            if (e.Hook == PrivacyHook.MediaSession)
            {
                if (e.NewState == ConsentState.Approved)
                {
                    if (ShouldRunForCurrentMode() && mediaManager == null)
                        Start();
                }
                else if (e.NewState == ConsentState.Denied)
                {
                    Stop();
                    _toast?.Show("🔒 Media Session", "Media session access paused — privacy consent revoked.", ToastType.Privacy, key: "medialink-privacy-denied");
                }
            }
        };
        _consentService.ConsentChanged += _consentChangedHandler;

        if (shouldStart && _consentService.IsApproved(PrivacyHook.MediaSession))
            Start();
    }

    public void SelectMediaSession(MediaSessionInfo sessionInfo)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => SelectMediaSession(sessionInfo));
            return;
        }

        SelectActiveSession(sessionInfo);
        LastMediaChangeTime = DateTime.UtcNow;
    }

    private void QueueRefreshActiveSelection(MediaSessionInfo? sessionInfo, bool allowSingleSessionFallback)
        => _dispatcher.BeginInvoke(() => RefreshActiveSelection(sessionInfo, allowSingleSessionFallback));

    private void RefreshActiveSelection(MediaSessionInfo? changedSession, bool allowSingleSessionFallback)
    {
        var sessions = _mediaLink.MediaSessions.ToList();
        if (sessions.Count == 0)
            return;

        var activeSession = sessions.FirstOrDefault(s => s.IsActive);
        MediaSessionInfo? selectedSession = null;

        if (_mediaLinkSettings.AutoSwitch)
        {
            var playingSessions = sessions
                .Where(s => s.AutoSwitch && s.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .ToList();

            if (playingSessions.Count > 0)
            {
                selectedSession = playingSessions.FirstOrDefault(s => SessionIdsMatch(s, changedSession))
                    ?? (activeSession != null && playingSessions.Any(s => SessionIdsMatch(s, activeSession)) ? activeSession : null)
                    ?? playingSessions[0];
            }
        }

        if (selectedSession == null && activeSession != null)
            return;

        if (selectedSession == null && allowSingleSessionFallback && sessions.Count == 1)
        {
            selectedSession = sessions[0];
        }
        else if (selectedSession == null &&
                 activeSession == null &&
                 changedSession != null &&
                 sessions.Any(s => SessionIdsMatch(s, changedSession)))
        {
            selectedSession = changedSession;
        }

        if (selectedSession != null)
            SelectActiveSession(selectedSession);
    }

    private void SelectActiveSession(MediaSessionInfo selectedSession)
    {
        foreach (var item in _mediaLink.MediaSessions)
            item.IsActive = SessionIdsMatch(item, selectedSession);
    }

    private static bool SessionIdsMatch(MediaSessionInfo? left, MediaSessionInfo? right)
    {
        if (left == null || right == null)
            return false;

        string leftId = left.Session?.Id;
        string rightId = right.Session?.Id;

        return !string.IsNullOrEmpty(leftId) &&
               string.Equals(leftId, rightId, StringComparison.Ordinal);
    }

    private static bool ApplyTimelineProperties(
        MediaSessionInfo sessionInfo,
        GlobalSystemMediaTransportControlsSessionTimelineProperties args,
        bool rejectUnchangedStaleTimeline = false)
    {
        TimeSpan fullTime = args.EndTime - args.StartTime;
        TimeSpan currentTime = args.Position;
        if (args.StartTime != TimeSpan.Zero)
            currentTime -= args.StartTime;

        if (fullTime > TimeSpan.Zero)
        {
            if (currentTime < TimeSpan.Zero)
                currentTime = TimeSpan.Zero;
            if (currentTime > fullTime)
                currentTime = fullTime;

            if (rejectUnchangedStaleTimeline
                && sessionInfo.IsTimelineStale
                && TimelineValuesMatch(sessionInfo.FullTime, fullTime)
                && TimelineValuesMatch(sessionInfo.StoredCurrentTime, currentTime))
            {
                return false;
            }

            if (ShouldIgnoreRegressiveTimelineUpdate(sessionInfo, fullTime, currentTime))
                return false;

            sessionInfo.FullTime = fullTime;
            sessionInfo.CurrentTime = currentTime;
            sessionInfo.TimePeekEnabled = true;
            sessionInfo.MarkTimelineFresh();
            return true;
        }

        sessionInfo.TimePeekEnabled = false;
        sessionInfo.MarkTimelineFresh();
        return false;
    }

    private static bool TimelineValuesMatch(TimeSpan left, TimeSpan right)
        => Math.Abs((left - right).TotalMilliseconds) <= 500;

    private static bool ShouldIgnoreRegressiveTimelineUpdate(MediaSessionInfo sessionInfo, TimeSpan fullTime, TimeSpan currentTime)
    {
        if (sessionInfo.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            return false;

        // A timeline marked stale (e.g. metadata changed) must always accept the next snapshot —
        // otherwise the seekbar can get stuck on the previous song when the new track happens
        // to fall inside the drift window.
        if (sessionInfo.IsTimelineStale)
            return false;

        if (!TimelineValuesMatch(sessionInfo.FullTime, fullTime))
            return false;

        TimeSpan storedCurrentTime = sessionInfo.StoredCurrentTime;

        // Any meaningful backward movement in the *stored* (non-extrapolated) position is a
        // legitimate user seek — honor it even when it's small (a few seconds). Only a tiny
        // jitter tolerance is allowed so the player can re-emit the same snapshot.
        if (currentTime < storedCurrentTime - TimeSpan.FromMilliseconds(250))
            return false;

        // Suppress only when the incoming position is slightly behind our *extrapolated*
        // CurrentTime — that pattern indicates clock/scheduler drift, not a real seek.
        TimeSpan extrapolatedDelta = sessionInfo.CurrentTime - currentTime;
        return extrapolatedDelta > TimelineBackwardDriftTolerance &&
               extrapolatedDelta <= TimelineBackwardJumpThreshold;
    }

    private void ApplyPlaybackSnapshot(
        MediaSession session,
        MediaSessionInfo sessionInfo,
        bool includeTimeline = true,
        bool rejectUnchangedStaleTimeline = false)
    {
        ApplyPlaybackStateSnapshot(session, sessionInfo);

        if (includeTimeline)
            TryApplyTimelineSnapshot(session, sessionInfo, rejectUnchangedStaleTimeline);
    }

    private void ApplyPlaybackStateSnapshot(MediaSession session, MediaSessionInfo sessionInfo)
    {
        try
        {
            var playbackInfo = session.ControlSession.GetPlaybackInfo();
            sessionInfo.PlaybackStatus = playbackInfo.PlaybackStatus;
            if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                sessionInfo.CurrentTime = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Unable to read media playback snapshot for {session.Id}: {ex.Message}");
        }
    }

    private bool TryApplyTimelineSnapshot(
        MediaSession session,
        MediaSessionInfo sessionInfo,
        bool rejectUnchangedStaleTimeline = false)
    {
        try
        {
            return ApplyTimelineProperties(
                sessionInfo,
                session.ControlSession.GetTimelineProperties(),
                rejectUnchangedStaleTimeline);
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Unable to read media timeline snapshot for {session.Id}: {ex.Message}");
            return false;
        }
    }

    private bool ApplyMediaProperties(
        MediaSessionInfo sessionInfo,
        GlobalSystemMediaTransportControlsSessionMediaProperties properties,
        bool staleTimelineOnChange)
    {
        bool changed = !string.Equals(sessionInfo.Title, properties.Title, StringComparison.Ordinal) ||
                       !string.Equals(sessionInfo.Artist, properties.Artist, StringComparison.Ordinal) ||
                       !string.Equals(sessionInfo.AlbumTitle, properties.AlbumTitle, StringComparison.Ordinal);

        if (changed)
            LastMediaChangeTime = DateTime.UtcNow;

        sessionInfo.AlbumArtist = properties.AlbumArtist;
        sessionInfo.AlbumTitle = properties.AlbumTitle;
        sessionInfo.Artist = properties.Artist;
        sessionInfo.Title = properties.Title;

        if (staleTimelineOnChange && changed)
            sessionInfo.MarkTimelineStale();

        return changed;
    }

    private async Task RefreshTimelineAfterMediaChangeAsync(MediaSession session, MediaSessionInfo sessionInfo)
    {
        try
        {
            await Task.Delay(TimelineRefreshAfterMediaChangeDelay).ConfigureAwait(false);
            if (!ReferenceEquals(sessionInfo.Session, session))
                return;

            if (!TryApplyTimelineSnapshot(session, sessionInfo, rejectUnchangedStaleTimeline: true)
                && ReferenceEquals(sessionInfo.Session, session))
            {
                Logging.WriteInfo($"MediaLink timeline for {session.Id} still stale after metadata change; waiting for resync.");
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Unable to refresh media timeline after metadata change for {session.Id}: {ex.Message}");
        }
    }

    private async Task<bool> ApplyMediaPropertySnapshotAsync(
        MediaSession session,
        MediaSessionInfo sessionInfo,
        bool staleTimelineOnChange = true)
    {
        try
        {
            var properties = await session.ControlSession.TryGetMediaPropertiesAsync();
            if (properties == null)
                return false;

            bool changed = ApplyMediaProperties(sessionInfo, properties, staleTimelineOnChange);
            if (changed && (staleTimelineOnChange || !sessionInfo.TimePeekEnabled || sessionInfo.IsTimelineStale))
                _ = RefreshTimelineAfterMediaChangeAsync(session, sessionInfo);

            return changed;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Unable to read media properties snapshot for {session.Id}: {ex.Message}");
            return false;
        }
    }

    private void CleanupExpiredSessions()
    {
        var expiredSessions = recentlyClosedSessions
            .Where(kvp => DateTime.Now - kvp.Value.Item2 > GracePeriod)
            .ToList();

        foreach (var expiredSession in expiredSessions)
        {
            // Conditional remove: skip entries refreshed by a reopen/re-close since the snapshot.
            if (recentlyClosedSessions.TryRemove(expiredSession))
                expiredSession.Value.Item1.Dispose();
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
            ApplyMediaProperties(sessionInfo, args, staleTimelineOnChange: true);
            ApplyPlaybackSnapshot(sender, sessionInfo, includeTimeline: false);
            _ = RefreshTimelineAfterMediaChangeAsync(sender, sessionInfo);
            QueueRefreshActiveSelection(sessionInfo, allowSingleSessionFallback: true);
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
                else if (args.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    TryApplyTimelineSnapshot(
                        sender,
                        sessionInfo,
                        rejectUnchangedStaleTimeline: sessionInfo.IsTimelineStale);
                }

                QueueRefreshActiveSelection(sessionInfo, allowSingleSessionFallback: true);
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

            if (sessionInfo != null && !sessionInfo.IsDisposed)
            {
                sessionInfo.TimeoutRestore = true;
                recentlyClosedSessions[session.Id] = (sessionInfo, DateTime.Now);

                bool wasActive = sessionInfo.IsActive;

                _dispatcher.BeginInvoke(
                    () =>
                    {
                        if (!ReferenceEquals(sessionInfo.Session, session))
                            return;

                        _mediaLink.MediaSessions.Remove(sessionInfo);
                        if (wasActive)
                            RefreshActiveSelection(null, allowSingleSessionFallback: true);
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

    private async void MediaManager_OnAnySessionOpened(MediaSession session)
    {
        try
        {
            MediaSessionInfo? sessionInfo = null;

            if (recentlyClosedSessions.TryGetValue(session.Id, out var recentSessionInfo))
            {
                var (recentInfo, closeTime) = recentSessionInfo;

                if (DateTime.Now - closeTime <= GracePeriod && !recentInfo.IsDisposed)
                {
                    sessionInfo = recentInfo;
                    sessionInfo.Session = session;
                    sessionInfo.TimeoutRestore = false;
                    recentlyClosedSessions.TryRemove(session.Id, out _);
                }
                else
                {
                    // Expired or disposed park entry — drop it so a fresh, live instance is built below.
                    recentlyClosedSessions.TryRemove(session.Id, out _);
                }
            }

            if (sessionInfo == null)
            {
                sessionInfo = new MediaSessionInfo(_mediaLinkSettings, _mediaLink) { Session = session };
            }

            sessionInfoLookup[session] = sessionInfo;
            currentSession = session;
            SessionRestore(sessionInfo);
            ApplyPlaybackSnapshot(session, sessionInfo);

            LastMediaChangeTime = DateTime.UtcNow;

            _dispatcher.BeginInvoke(
                    () =>
                    {
                        foreach (MediaSessionInfo duplicate in _mediaLink.MediaSessions
                                     .Where(s => !ReferenceEquals(s, sessionInfo) && SessionIdsMatch(s, sessionInfo))
                                     .ToList())
                        {
                            _mediaLink.MediaSessions.Remove(duplicate);

                            // Dispose dropped duplicates unless still parked for a grace-period restore.
                            if (!recentlyClosedSessions.Values.Any(v => ReferenceEquals(v.Item1, duplicate)))
                            {
                                if (duplicate.Session != null)
                                    sessionInfoLookup.TryRemove(duplicate.Session, out _);
                                duplicate.Dispose();
                            }
                        }

                        if (!_mediaLink.MediaSessions.Contains(sessionInfo))
                            _mediaLink.MediaSessions.Add(sessionInfo);

                        RefreshActiveSelection(sessionInfo, allowSingleSessionFallback: true);
                    });

            await ApplyMediaPropertySnapshotAsync(session, sessionInfo, staleTimelineOnChange: false);
            if (!sessionInfo.TimePeekEnabled || sessionInfo.IsTimelineStale)
                TryApplyTimelineSnapshot(session, sessionInfo);

            QueueRefreshActiveSelection(sessionInfo, allowSingleSessionFallback: true);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private void MediaManager_OnFocusedSessionChanged(MediaSession? session)
    {
        if (session is null)
        {
            currentSession = null;
            QueueRefreshActiveSelection(null, allowSingleSessionFallback: true);
            return;
        }

        currentSession = session;
        if (sessionInfoLookup.TryGetValue(session, out MediaSessionInfo? sessionInfo))
        {
            LastMediaChangeTime = DateTime.UtcNow;
            QueueRefreshActiveSelection(sessionInfo, allowSingleSessionFallback: true);
        }
    }

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IntgrScanMediaLink" ||
            e.PropertyName == "IntgrMediaLink_VR" ||
            e.PropertyName == "IntgrMediaLink_DESKTOP" ||
            e.PropertyName == "IsVRRunning")
        {
            if (ShouldRunForCurrentMode())
            {
                if (mediaManager == null)
                    Start();
            }
            else
            {
                if (mediaManager != null)
                    Stop();
            }
        }
    }

    private bool ShouldRunForCurrentMode()
        => _integrationSettings.IntgrScanMediaLink &&
           (_appState.IsVRRunning
               ? _integrationSettings.IntgrMediaLink_VR
               : _integrationSettings.IntgrMediaLink_DESKTOP);

    private void StartMediaSnapshotResyncTimer()
    {
        if (mediaSnapshotResyncTimer != null)
            return;

        mediaSnapshotResyncTimer = new Timer(
            _ => QueueMediaSnapshotResync(),
            null,
            MediaSnapshotResyncInterval,
            MediaSnapshotResyncInterval);
    }

    private void StopMediaSnapshotResyncTimer()
    {
        mediaSnapshotResyncTimer?.Dispose();
        mediaSnapshotResyncTimer = null;
        Interlocked.Exchange(ref mediaSnapshotResyncInProgress, 0);
    }

    private void QueueMediaSnapshotResync()
    {
        if (mediaManager == null)
            return;

        if (Interlocked.CompareExchange(ref mediaSnapshotResyncInProgress, 1, 0) == 1)
            return;

        _ = ResyncMediaSnapshotsAsync();
    }

    private async Task ResyncMediaSnapshotsAsync()
    {
        try
        {
            var sessions = _dispatcher.Invoke(() => _mediaLink.MediaSessions
                    .Where(session => session.IsTimelineStale &&
                                      (session.IsActive ||
                                       session.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing))
                    .ToList());

            foreach (var sessionInfo in sessions)
            {
                MediaSession session = sessionInfo.Session;
                if (session == null || !ReferenceEquals(sessionInfo.Session, session))
                    continue;

                bool mediaChanged = await ApplyMediaPropertySnapshotAsync(session, sessionInfo).ConfigureAwait(false);
                ApplyPlaybackSnapshot(
                    session,
                    sessionInfo,
                    includeTimeline: true,
                    rejectUnchangedStaleTimeline: sessionInfo.IsTimelineStale || mediaChanged);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            Interlocked.Exchange(ref mediaSnapshotResyncInProgress, 0);
        }
    }

    /// <summary>
    /// Stops the media manager and disposes tracked sessions without unhooking the settings/consent
    /// listeners, so a later settings toggle or consent approval can restart the module.
    /// </summary>
    private void Stop()
    {
        StopMediaSnapshotResyncTimer();

        if (mediaManager != null)
        {
            mediaManager.OnAnySessionOpened -= MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed -= MediaManager_OnAnySessionClosed;
            mediaManager.OnFocusedSessionChanged -= MediaManager_OnFocusedSessionChanged;
            mediaManager.OnAnyPlaybackStateChanged -= MediaManager_OnAnyPlaybackStateChanged;
            mediaManager.OnAnyMediaPropertyChanged -= MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.OnAnyTimelinePropertyChanged -= MediaManager_OnAnyTimelinePropertyChanged;

            mediaManager.Dispose();
            mediaManager = null;
        }

        currentSession = null;
        DisposeTrackedSessions();
    }

    private void DisposeTrackedSessions()
    {
        var trackedSessions = new HashSet<MediaSessionInfo>(_mediaLink.MediaSessions);
        foreach (var info in sessionInfoLookup.Values)
            trackedSessions.Add(info);
        foreach (var (info, _) in recentlyClosedSessions.Values)
            trackedSessions.Add(info);

        _mediaLink.MediaSessions.Clear();
        sessionInfoLookup.Clear();
        recentlyClosedSessions.Clear();

        foreach (var info in trackedSessions)
            info.Dispose();
    }

    /// <summary>
    /// Unsubscribes all event handlers and disposes the underlying <see cref="MediaManager"/> instance.
    /// Reserved for app shutdown — runtime enable/disable goes through <see cref="Stop"/>.
    /// </summary>
    public void Dispose()
    {
        _appState.PropertyChanged -= ViewModel_PropertyChanged;
        _integrationSettings.PropertyChanged -= ViewModel_PropertyChanged;
        _consentService.ConsentChanged -= _consentChangedHandler;
        Stop();
    }

    public async Task MediaManager_NextAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        await S.ControlSession.TrySkipNextAsync();
    }

    /// <summary>
    /// Updates the current session's timeline properties (position and duration) from the Windows media API.
    /// </summary>
    public void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args)
    {
        var sessionInfo = sessionInfoLookup.GetValueOrDefault(sender);

        if (sessionInfo != null)
        {
            ApplyTimelineProperties(sessionInfo, args, rejectUnchangedStaleTimeline: sessionInfo.IsTimelineStale);
        }
    }

    public async Task MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;

        await S.ControlSession.TryTogglePlayPauseAsync();
    }

    public async Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo)
    {
        MediaSession S = sessionInfo.Session;

        if (S == null)
            return;
        await S.ControlSession.TrySkipPreviousAsync();
        if (sessionInfo.CurrentTime > TimeSpan.FromSeconds(2))
        {
            await S.ControlSession.TrySkipPreviousAsync();
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
        MediaSessionSettings matchingSettings;

        lock (MediaSessionSettings.SavedSessionsLock)
        {
            matchingSettings = _mediaLink.SavedSessionSettings
                .FirstOrDefault(s => s.SessionId == session.Session.Id);

            if (matchingSettings != null)
            {
                savedSettings.ShowTitle = matchingSettings.ShowTitle;
                savedSettings.AutoSwitch = matchingSettings.AutoSwitch;
                savedSettings.ShowArtist = matchingSettings.ShowArtist;
                savedSettings.IsVideo = matchingSettings.IsVideo;
                savedSettings.KeepSaved = matchingSettings.KeepSaved;
            }
        }

        // Apply outside the lock — the setters re-enter SaveOrDeleteSettings, which locks itself.
        if (matchingSettings != null && !session.TimeoutRestore)
        {
            session.ShowTitle = savedSettings.ShowTitle;
            session.AutoSwitch = savedSettings.AutoSwitch;
            session.ShowArtist = savedSettings.ShowArtist;
            session.IsVideo = savedSettings.IsVideo;
            session.KeepSaved = savedSettings.KeepSaved;
        }
    }

    /// <summary>
    /// Starts the <see cref="MediaManager"/> and subscribes to all media session events.
    /// </summary>
    public void Start()
    {
        if (mediaManager != null)
            return;

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
            StartMediaSnapshotResyncTimer();
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
