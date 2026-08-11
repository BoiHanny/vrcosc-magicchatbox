using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;
using static WindowsMediaController.MediaManager;

namespace vrcosc_magicchatbox.ViewModels.Models
{
    /// <summary>
    /// Represents an active media playback session, exposing properties for title, artist,
    /// playback status, seek position, and per-session user preferences.
    /// </summary>
    [DebuggerDisplay("{FriendlyAppName} - {TimePeekEnabled} - {TimePosition}/{CurrentTime}/{FullTime} live:{IsLiveTime}")]
    public class MediaSessionInfo : INotifyPropertyChanged, IDisposable
    {
        private readonly MediaLinkSettings _mediaLinkSettings;

        private readonly MediaLinkDisplayState _mediaLink;

        private bool _AutoSwitch;

        private Timer _updateTimer;
        private bool _disposed;

        /// <summary>True once Dispose has run; a disposed instance has a dead update timer and must not be revived.</summary>
        public bool IsDisposed => _disposed;

        private bool _IsActive;

        private bool _IsVideo;

        private bool _KeepSaved = false;

        private bool _ShowArtist = true;

        private bool _ShowTitle = true;

        private void UpdateCurrentTime(object state)
        {
            if (PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                // Match the OSC pipeline, which reads the extrapolated CurrentTime each scan tick.
                // Raise both notifications so any UI bound to CurrentTime or TimePosition advances
                // smoothly between Windows media-controller timeline events.
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(nameof(CurrentTime)));
                    handler(this, new PropertyChangedEventArgs(nameof(TimePosition)));
                }
            }
        }

        private bool _TimeoutRestore = false;
        private MediaSession session;

        private string _albumArtist = "Album-Artist";
        public string AlbumArtist
        {
            get => _albumArtist;
            set
            {
                if (!string.Equals(_albumArtist, value, StringComparison.Ordinal))
                {
                    _albumArtist = value;
                    NotifyPropertyChanged(nameof(AlbumArtist));
                }
            }
        }

        private string _albumTitle = "Album-Title";
        public string AlbumTitle
        {
            get => _albumTitle;
            set
            {
                if (!string.Equals(_albumTitle, value, StringComparison.Ordinal))
                {
                    _albumTitle = value;
                    NotifyPropertyChanged(nameof(AlbumTitle));
                }
            }
        }

        private string _artist = "Artist";
        public string Artist
        {
            get => _artist;
            set
            {
                if (!string.Equals(_artist, value, StringComparison.Ordinal))
                {
                    _artist = value;
                    NotifyPropertyChanged(nameof(Artist));
                }
            }
        }


        private GlobalSystemMediaTransportControlsSessionPlaybackStatus _PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
        public GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus
        {
            get { return _PlaybackStatus; }
            set
            {
                if (_PlaybackStatus == value)
                    return;

                _PlaybackStatus = value;

                // Restart the extrapolation baseline only on a real transition. Re-applying the
                // same "Playing" status (the resync loop does this every 2s) used to rewind the
                // displayed position back to the last stored snapshot.
                _lastUpdateTime = DateTime.UtcNow;
                NotifyPropertyChanged(nameof(PlaybackStatus));
                NotifyPropertyChanged(nameof(PlayingNow));
            }
        }


        public bool PlayingNow
        {
            get { return PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }

        }

        private string _title = "Title";
        public string Title
        {
            get => _title;
            set
            {
                if (!string.Equals(_title, value, StringComparison.Ordinal))
                {
                    _title = value;
                    NotifyPropertyChanged(nameof(Title));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SaveOrDeleteSettings()
        {
            lock (MediaSessionSettings.SavedSessionsLock)
            {
                if (_KeepSaved)
                {
                    var savedSettings = _mediaLink.SavedSessionSettings
                        .FirstOrDefault(s => s.SessionId == Session.Id);
                    if (savedSettings != null)
                    {
                        savedSettings.ShowTitle = _ShowTitle;
                        savedSettings.AutoSwitch = _AutoSwitch;
                        savedSettings.ShowArtist = _ShowArtist;
                        savedSettings.IsVideo = _IsVideo;
                        savedSettings.KeepSaved = _KeepSaved;
                    }
                    else
                    {
                        _mediaLink.SavedSessionSettings
                            .Add(
                                new MediaSessionSettings
                                {
                                    SessionId = Session.Id,
                                    ShowTitle = _ShowTitle,
                                    AutoSwitch = _AutoSwitch,
                                    ShowArtist = _ShowArtist,
                                    IsVideo = _IsVideo,
                                    KeepSaved = _KeepSaved
                                });
                    }
                }
                else
                {
                    var savedSettings = _mediaLink.SavedSessionSettings
                        .FirstOrDefault(s => s.SessionId == Session.Id);
                    if (savedSettings != null)
                    {
                        _mediaLink.SavedSessionSettings.Remove(savedSettings);
                    }
                }
            }
        }

        private void UpdateFriendlyAppName()
        {
            string id = Session.Id;
            try
            {
                if (!id.Contains('.') && !id.Contains('!') && char.IsUpper(id[0]))
                {
                    FriendlyAppName = id;
                    return;
                }

                if (id.Contains('!'))
                {
                    id = id.Split('!')[1];
                }

                if (id.Contains(".exe"))
                {
                    id = Path.GetFileNameWithoutExtension(id);
                }

                if (id.Contains("OperaSoftware"))
                {
                    FriendlyAppName = "Opera";
                    return;
                }

                FriendlyAppName = id;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                FriendlyAppName = id;
            }
        }

        protected void NotifyPropertyChanged(string name)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }

        public bool AutoSwitch
        {
            get { return _AutoSwitch; }
            set
            {
                _AutoSwitch = value;
                NotifyPropertyChanged(nameof(AutoSwitch));
                SaveOrDeleteSettings();
            }
        }

        public string FriendlyAppName { get; private set; }

        public bool IsActive
        {
            get { return _IsActive; }
            set
            {
                _IsActive = value;
                NotifyPropertyChanged(nameof(IsActive));
            }
        }

        public bool IsVideo
        {
            get { return _IsVideo; }
            set
            {
                _IsVideo = value;
                NotifyPropertyChanged(nameof(IsVideo));
                SaveOrDeleteSettings();
            }
        }

        private bool _TimePeekEnabled = false;
        private bool _IsTimelineStale;

        public bool TimePeekEnabled
        {
            get { return _TimePeekEnabled; }
            set
            {
                if (_TimePeekEnabled != value)
                {
                    _TimePeekEnabled = value;
                    NotifyPropertyChanged(nameof(TimePeekEnabled));
                }
            }
        }

        public bool IsTimelineStale
        {
            get { return _IsTimelineStale; }
            private set
            {
                if (_IsTimelineStale != value)
                {
                    _IsTimelineStale = value;
                    NotifyPropertyChanged(nameof(IsTimelineStale));
                }
            }
        }

        private DateTime? _timelineStaleSinceUtc;

        /// <summary>
        /// How long this session has been waiting for a usable timeline, or <see cref="TimeSpan.Zero"/>
        /// when it is not stale. Drives the policy's grace window so a player that stops emitting
        /// timeline events cannot keep the seekbar hidden forever.
        /// </summary>
        public TimeSpan TimelineStaleAge
            => _timelineStaleSinceUtc is { } since
                ? DateTime.UtcNow - since
                : TimeSpan.Zero;

        /// <summary>
        /// True when this source publishes no duration at all (live stream, call audio, a
        /// notification sound) rather than being mid-transition. Keeps the seekbar hidden and takes
        /// the session out of the resync poll loop until a real timeline or a track change arrives.
        /// </summary>
        public bool HasNoTimeline { get; private set; }

        /// <param name="restartStaleClock">
        /// True when this is a fresh wait — a track change. The clock must restart then, or the
        /// new track inherits the previous one's accumulated age and settles immediately.
        /// Repeated stale markings within one track keep the original timestamp so the age
        /// actually accumulates.
        /// </param>
        public void MarkTimelineStale(bool restartStaleClock = false)
        {
            if (restartStaleClock)
                _timelineStaleSinceUtc = DateTime.UtcNow;
            else
                _timelineStaleSinceUtc ??= DateTime.UtcNow;

            IsTimelineStale = true;
            HasNoTimeline = false;
            TimePeekEnabled = false;
            NotifyPropertyChanged(nameof(TimePosition));
        }

        /// <summary>
        /// Settles a source that has reported no duration for long enough that this is clearly its
        /// nature rather than a transition. Unlike <see cref="MarkTimelineStale"/> this stops the
        /// 2-second resync loop from re-polling it across the WinRT boundary for as long as it plays.
        /// </summary>
        public void MarkTimelineDurationless()
        {
            _timelineStaleSinceUtc = null;
            IsTimelineStale = false;
            HasNoTimeline = true;
            TimePeekEnabled = false;
            NotifyPropertyChanged(nameof(TimePosition));
        }

        public void MarkTimelineFresh()
        {
            _timelineStaleSinceUtc = null;
            IsTimelineStale = false;
            HasNoTimeline = false;
        }

        private DateTime _lastUpdateTime;


        public bool IsLiveTime
        {
            get { return FullTime >= TimeSpan.FromHours(14); }
        }
        public int TimePosition
        {
            get
            {
                double fullMilliseconds = FullTime.TotalMilliseconds;
                double currentMilliseconds = CurrentTime.TotalMilliseconds;

                if (fullMilliseconds <= 0 || double.IsNaN(fullMilliseconds) || double.IsInfinity(fullMilliseconds))
                    return 0;

                if (double.IsNaN(currentMilliseconds) || double.IsInfinity(currentMilliseconds))
                    return 0;

                double percent = currentMilliseconds / fullMilliseconds * 100;
                return (int)Math.Clamp(percent, 0, 100);
            }
        }



        private TimeSpan _CurrentTime = new TimeSpan(0, 0, 0);

        public TimeSpan StoredCurrentTime => _CurrentTime;

        public TimeSpan CurrentTime
        {
            get
            {
                // Only extrapolate when the player is actively playing AND we have a known
                // duration. For live streams or unknown duration (FullTime <= 0) we keep the
                // stored value so the UI doesn't show an ever-growing fake position.
                if (PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                    && _FullTime > TimeSpan.Zero)
                {
                    var elapsedTime = DateTime.UtcNow - _lastUpdateTime;
                    if (elapsedTime < TimeSpan.Zero)
                        elapsedTime = TimeSpan.Zero;

                    TimeSpan livePosition = _CurrentTime + elapsedTime;
                    if (livePosition > _FullTime)
                        return _FullTime;

                    return livePosition;
                }
                return _CurrentTime;
            }
            set
            {
                _CurrentTime = value;
                _lastUpdateTime = DateTime.UtcNow;
                NotifyPropertyChanged(nameof(CurrentTime));
                NotifyPropertyChanged(nameof(TimePosition));
            }
        }

        public MediaSessionInfo(MediaLinkSettings mediaLinkSettings, MediaLinkDisplayState mediaLink)
        {
            _mediaLinkSettings = mediaLinkSettings;
            _mediaLink = mediaLink;
            _AutoSwitch = _mediaLinkSettings.AutoSwitchSpawn;
            _lastUpdateTime = DateTime.UtcNow;
            _updateTimer = new Timer(UpdateCurrentTime, null, 0, 1000);
        }

        private TimeSpan _FullTime = new TimeSpan(0, 0, 0);

        public TimeSpan FullTime
        {
            get { return _FullTime; }
            set
            {
                if (_FullTime != value)
                {
                    _FullTime = value;
                    NotifyPropertyChanged(nameof(FullTime));
                    NotifyPropertyChanged(nameof(TimePosition));
                }
            }
        }

        public bool KeepSaved
        {
            get { return _KeepSaved; }
            set
            {
                _KeepSaved = value;
                NotifyPropertyChanged(nameof(KeepSaved));
                SaveOrDeleteSettings();
            }
        }

        public MediaSession Session
        {
            get { return session; }
            set
            {
                session = value;
                UpdateFriendlyAppName();
            }
        }

        public bool ShowArtist
        {
            get { return _ShowArtist; }
            set
            {
                _ShowArtist = value;
                NotifyPropertyChanged(nameof(ShowArtist));
                SaveOrDeleteSettings();
            }
        }

        public bool ShowTitle
        {
            get { return _ShowTitle; }
            set
            {
                _ShowTitle = value;
                NotifyPropertyChanged(nameof(ShowTitle));
                SaveOrDeleteSettings();
            }
        }

        public bool TimeoutRestore
        {
            get { return _TimeoutRestore; }
            set
            {
                _TimeoutRestore = value;
                NotifyPropertyChanged(nameof(TimeoutRestore));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _updateTimer?.Dispose();
            _updateTimer = null;
        }


    }

    /// <summary>
    /// Stores persisted user preferences for a media session, keyed by session ID.
    /// </summary>
    public class MediaSessionSettings
    {
        /// <summary>
        /// Guards every read and write of the shared SavedSessionSettings list, which is
        /// touched from media-manager callback threads, the UI thread, and persistence.
        /// </summary>
        public static readonly object SavedSessionsLock = new object();

        public bool AutoSwitch { get; set; }

        public bool IsVideo { get; set; }

        public bool KeepSaved { get; set; }

        public string SessionId { get; set; }

        public bool ShowArtist { get; set; }

        public bool ShowTitle { get; set; }
    }
}
