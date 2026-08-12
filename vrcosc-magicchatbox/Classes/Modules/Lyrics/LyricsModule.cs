using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Lyrics;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;

namespace vrcosc_magicchatbox.Classes.Modules.Lyrics;

public partial class LyricsModule : ObservableObject, IModule
{
    private const string SpotifySourceName = "Spotify";
    private const string MediaSourceName = "Windows media";

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private readonly ISettingsProvider<LyricsSettings> _settingsProvider;
    private readonly IntegrationSettings _integrationSettings;
    private readonly LyricsResolver _resolver;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly SpotifyDisplayState _spotify;
    private readonly LyricsDisplayState _display;
    private readonly IPrivacyConsentService _consent;
    private readonly object _lock = new();

    private Timer? _timer;
    private LyricTrack _track = LyricTrack.Empty;
    private string _trackIdentity = string.Empty;
    private int _lookupInFlight;
    private bool _disposed;

    public LyricsSettings Settings => _settingsProvider.Value;

    public string Name => "Lyrics";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning { get; private set; }

    public LyricsModule(
        ISettingsProvider<LyricsSettings> settingsProvider,
        IntegrationSettings integrationSettings,
        LyricsResolver resolver,
        MediaLinkDisplayState mediaLink,
        SpotifyDisplayState spotify,
        LyricsDisplayState display,
        IPrivacyConsentService consent)
    {
        _settingsProvider = settingsProvider;
        _integrationSettings = integrationSettings;
        _resolver = resolver;
        _mediaLink = mediaLink;
        _spotify = spotify;
        _display = display;
        _consent = consent;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_disposed || IsRunning)
                return Task.CompletedTask;

            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TickInterval);
            IsRunning = true;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            IsRunning = false;
        }

        _track = LyricTrack.Empty;
        _trackIdentity = string.Empty;
        _display.Reset("Stopped");
        return Task.CompletedTask;
    }

    public void PropertyChangedHandler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IntegrationSettings.IntgrLyrics))
            return;

        if (sender is not IntegrationSettings settings)
            return;

        if (settings.IntgrLyrics)
            _ = StartAsync();
        else
            _ = StopAsync();
    }

    private void Tick()
    {
        try
        {
            var source = ResolvePosition();

            if (source == null)
            {
                _track = LyricTrack.Empty;
                _trackIdentity = string.Empty;
                _display.Reset(DescribeNoSource());
                _display.Attach(ResolvePlacement(null));
                return;
            }

            _display.Attach(ResolvePlacement(source));

            if (source.Identity != _trackIdentity)
            {
                _trackIdentity = source.Identity;
                _track = LyricTrack.Empty;
                _display.HasTrack = false;
                _display.CurrentLine = string.Empty;
                _display.IsShowingLine = false;
                _display.SuppressMediaTitle = false;
                _display.NowPlaying = $"{source.Artist} - {source.Title}";
                _display.StatusText = "Looking up lyrics...";
                BeginLookup(source);
                return;
            }

            if (!_track.IsSynced)
                return;

            var cursor = LyricScheduler.Resolve(
                _track,
                source.Position,
                TimeSpan.FromMilliseconds(Settings.OffsetMs),
                TimeSpan.FromSeconds(Math.Max(1, Settings.GapThresholdSeconds)),
                TimeSpan.FromSeconds(Math.Max(1, Settings.LineHoldSeconds)));

            string line = cursor.Kind == LyricCursorKind.Line
                ? LyricSegmentFormatter.Sanitize(cursor.Text)
                : string.Empty;

            _display.Cursor = cursor;
            _display.Position = source.Position;
            _display.CurrentLine = line;
            _display.IsShowingLine = line.Length > 0;
            _display.PositionSource = source.SourceName;
            _display.SuppressMediaTitle =
                Settings.Coexistence == LyricsMediaCoexistence.PreferLyrics && line.Length > 0;
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Lyrics tick failed: {ex.Message}");
        }
    }

    private void BeginLookup(PositionSource source)
    {
        if (Interlocked.Exchange(ref _lookupInFlight, 1) == 1)
            return;

        string identity = source.Identity;

        _ = Task.Run(async () =>
        {
            try
            {
                var query = TrackQueryNormalizer.Normalize(
                    source.Title, source.Artist, source.Album, source.Duration);

                if (!LyricsLookupPolicy.ShouldLookUp(query, out string skipReason))
                {
                    _track = LyricTrack.Empty;
                    _display.HasTrack = false;
                    _display.LineCount = 0;
                    _display.StatusText = skipReason;
                    return;
                }

                var result = await _resolver.ResolveAsync(query).ConfigureAwait(false);

                if (identity != _trackIdentity)
                    return;

                _track = result.Track ?? LyricTrack.Empty;
                _display.HasTrack = _track.IsSynced;
                _display.LineCount = _track.Lines.Count;
                _display.ProviderName = result.ProviderName;
                _display.LastLookupUtc = DateTime.UtcNow;
                _display.StatusText = _track.IsSynced
                    ? $"{_track.Lines.Count} lines from {result.ProviderName}{(result.WasCached ? " (cached)" : string.Empty)}"
                    : BuildMissMessage(query);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Lyrics lookup failed: {ex.Message}");
                _display.StatusText = "Lookup failed";
            }
            finally
            {
                Interlocked.Exchange(ref _lookupInFlight, 0);
            }
        });
    }

    private string BuildMissMessage(LyricsQuery query)
    {
        if (!_consent.IsApproved(PrivacyHook.InternetAccess))
            return "Internet Access permission is off, so only local .lrc files can be used";

        return $"No synced lyrics found for \"{query.Artist} - {query.Title}\"";
    }

    private LyricsCardPlacement ResolvePlacement(PositionSource? source)
        => LyricsCardPlacement.Resolve(
            _integrationSettings.IntgrLyrics,
            hasSpotifySource: source != null && source.SourceName == SpotifySourceName,
            hasMediaSource: source != null && source.SourceName != SpotifySourceName,
            // A card with nothing playing parks on a host that lyrics are actually switched on for,
            // not merely on whichever integration happens to be enabled.
            mediaLinkEnabled: _integrationSettings.IntgrScanMediaLink && _integrationSettings.IntgrLyrics_MediaLink,
            spotifyEnabled: _integrationSettings.IntgrSpotify && _integrationSettings.IntgrLyrics_Spotify);

    private string DescribeNoSource()
    {
        var candidates = SnapshotSessions()
            .Select(s => new LyricsSourceCandidate(s.Title, s.PlaybackStatus.ToString()))
            .ToList();

        return LyricsSourceStatus.Describe(
            _integrationSettings.IntgrScanMediaLink,
            _integrationSettings.IntgrSpotify,
            candidates);
    }

    private List<MediaSessionInfo> SnapshotSessions()
    {
        try
        {
            var sessions = _mediaLink.MediaSessions;
            return sessions == null ? new List<MediaSessionInfo>() : sessions.ToList();
        }
        catch (InvalidOperationException)
        {
            return new List<MediaSessionInfo>();
        }
    }

    private PositionSource? ResolvePosition()
    {
        if (_integrationSettings.IntgrLyrics_Spotify
            && _spotify.IsConnected && _spotify.HasPlayback && _spotify.IsPlaying)
        {
            return new PositionSource(
                _spotify.Title,
                _spotify.Artist,
                _spotify.Album,
                TimeSpan.FromMilliseconds(_spotify.LiveProgressMs),
                TimeSpan.FromMilliseconds(Math.Max(0, _spotify.DurationMs)),
                SpotifySourceName,
                $"spotify:{_spotify.TrackId}");
        }

        if (!_integrationSettings.IntgrLyrics_MediaLink)
            return null;

        var sessions = SnapshotSessions();

        var session =
            sessions.FirstOrDefault(s =>
                s.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            ?? sessions.FirstOrDefault(s =>
                s.IsActive &&
                s.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
            ?? sessions.FirstOrDefault(s => s.IsActive);

        if (session == null || string.IsNullOrWhiteSpace(session.Title))
            return null;

        return new PositionSource(
            session.Title,
            session.Artist,
            session.AlbumTitle,
            session.CurrentTime,
            session.FullTime,
            MediaSourceName,
            $"smtc:{session.Artist}|{session.Title}");
    }

    public void SaveSettings() => _settingsProvider.Save();

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        StopAsync().GetAwaiter().GetResult();
    }

    private sealed record PositionSource(
        string Title,
        string Artist,
        string Album,
        TimeSpan Position,
        TimeSpan Duration,
        string SourceName,
        string Identity);
}
