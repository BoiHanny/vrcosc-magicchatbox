using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Media;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class MediaLinkOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly MediaLinkSettings _mls;
    private readonly SpotifySettings _spotify;
    private readonly AppSettings _app;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly SpotifyDisplayState _spotifyDisplay;
    private readonly LyricsDisplayState _lyricsDisplay;
    private readonly Lazy<IMediaLinkService> _mediaLinkSvc;

    public MediaLinkOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<MediaLinkSettings> mlsProvider,
        ISettingsProvider<SpotifySettings> spotifyProvider,
        ISettingsProvider<AppSettings> appProvider,
        MediaLinkDisplayState mediaLink,
        SpotifyDisplayState spotifyDisplay,
        LyricsDisplayState lyricsDisplay,
        Lazy<IMediaLinkService> mediaLinkSvc)
    {
        _intgr = intgrProvider.Value;
        _mls = mlsProvider.Value;
        _spotify = spotifyProvider.Value;
        _app = appProvider.Value;
        _mediaLink = mediaLink;
        _spotifyDisplay = spotifyDisplay;
        _lyricsDisplay = lyricsDisplay;
        _mediaLinkSvc = mediaLinkSvc;
    }

    public string SortKey => "MediaLink";
    public string UiKey => "MediaLink";
    public int Priority => 20;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrScanMediaLink && (isVR ? _intgr.IntgrMediaLink_VR : _intgr.IntgrMediaLink_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrScanMediaLink)
            return null;

        if (_intgr.IntgrLyrics && _lyricsDisplay.SuppressMediaTitle)
            return null;

        if (!TransientWindow.ShouldShow(
                _mls.ShowOnlyOnChange,
                _mediaLinkSvc.Value.LastMediaChangeTime,
                DateTime.UtcNow,
                _mls.TransientDuration))
            return null;

        string text = BuildMediaText(context);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (_mls.UpperCase)
            text = text.ToUpper(CultureInfo.CurrentCulture);

        return new OscSegment { Text = text };
    }

    #region Core MediaLink logic (moved from OSCController.AddMediaLink)

    private IReadOnlyList<MediaSessionInfo> SnapshotSessions() => _mediaLink.MediaSessionsSnapshot;

    private string BuildMediaText(OscBuildContext context)
    {
        IEnumerable<MediaSessionInfo> sessions = SnapshotSessions().Where(s => s.IsActive);
        if (ShouldSuppressSpotifySessions(context.IsVRRunning))
            sessions = sessions.Where(s => !IsSpotifySession(s));

        MediaSessionInfo? session = sessions.FirstOrDefault();
        if (session == null)
            return BuildNoSessionText();

        var isPaused = session.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
        var isPlaying = session.PlaybackStatus == Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

        if (!isPaused && !isPlaying)
            return string.Empty;

        if (isPaused)
            return BuildPausedText(session);

        return BuildPlayingText(session, context);
    }

    private bool ShouldSuppressSpotifySessions(bool isVR)
    {
        if (_spotify.MediaLinkCoexistence != SpotifyMediaLinkCoexistence.PreferSpotify)
            return false;

        if (!_intgr.IntgrSpotify)
            return false;

        if (isVR ? !_intgr.IntgrSpotify_VR : !_intgr.IntgrSpotify_DESKTOP)
            return false;

        if (!_spotifyDisplay.IsConnected || !_spotifyDisplay.HasPlayback)
            return false;

        return _spotifyDisplay.IsPlaying || _spotify.PauseOutputMode != SpotifyPauseOutputMode.Hide;
    }

    private static bool IsSpotifySession(MediaSessionInfo session)
    {
        string friendlyName = session.FriendlyAppName ?? string.Empty;
        string sessionId = session.Session?.Id ?? string.Empty;
        return friendlyName.Contains("spotify", StringComparison.OrdinalIgnoreCase) ||
               sessionId.Contains("spotify", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildNoSessionText()
    {
        string stopIcon = ResolveStopIcon();
        string pausedText = ResolvePausedText();
        return _mls.PauseIconMusic && _app.PrefixIconMusic && !string.IsNullOrWhiteSpace(stopIcon)
            ? stopIcon
            : pausedText;
    }

    private string BuildPausedText(MediaSessionInfo session)
    {
        string pauseIcon = ResolvePauseIcon();
        if (_mls.PauseIconMusic && _app.PrefixIconMusic && !string.IsNullOrWhiteSpace(pauseIcon))
            return pauseIcon;

        return ResolveActionText(session, isPlaying: false);
    }

    private string BuildPlayingText(MediaSessionInfo session, OscBuildContext context)
    {
        string actionText = ResolveActionText(session, isPlaying: true);
        string playIcon = ResolvePlayIcon(session);
        bool wantsSeekbar = !session.IsLiveTime && session.TimePeekEnabled && !session.IsTimelineStale;

        string Line(string body)
        {
            if (string.IsNullOrEmpty(body))
                return actionText;

            return _app.PrefixIconMusic && !string.IsNullOrWhiteSpace(playIcon)
                ? $"{playIcon} {body}"
                : $"{actionText} {body}";
        }

        IReadOnlyList<string> bodies = BuildBodyLadder(session);

        foreach (MediaLinkTimeSeekbar style in BuildSeekbarLadder(wantsSeekbar))
        {
            foreach (string body in bodies)
            {
                string candidate = wantsSeekbar
                    ? ApplySeekbar(Line(body), session, context, style)
                    : Line(body);

                if (context.WouldFit(candidate))
                    return candidate;
            }
        }

        if (!_mls.ShortenToFit)
            return Line(bodies[0]);

        string bare = Line(bodies[^1]);
        return SegmentWriter.Truncate(bare, context.RemainingCharsIf(string.Empty));
    }

    private IReadOnlyList<string> BuildBodyLadder(MediaSessionInfo session)
    {
        string title = ResolveTitle(session);
        string artist = session.ShowArtist ? session.Artist ?? string.Empty : string.Empty;

        if (!_mls.ShortenToFit)
            return [Join(title, artist)];

        var bodies = new List<string>();

        void Add(string body)
        {
            if (body.Length > 0 && !bodies.Contains(body))
                bodies.Add(body);
        }

        Add(Join(title, artist));

        string plainTitle = MediaTitleCleaner.StripFeatured(title);
        Add(Join(plainTitle, artist));

        foreach (string rung in ArtistNameShortener.Ladder(artist))
            Add(Join(plainTitle, rung));

        Add(plainTitle);

        if (bodies.Count == 0)
            bodies.Add(string.Empty);

        return bodies;
    }

    private string ResolveTitle(MediaSessionInfo session)
    {
        if (!session.ShowTitle || string.IsNullOrEmpty(session.Title))
            return string.Empty;

        if (!_mls.TidyTitles)
            return session.Title;

        string cleaned = MediaTitleCleaner.Clean(session.Title, session.ShowArtist ? session.Artist : null);

        return cleaned.Length > 0 ? cleaned : session.Title;
    }

    private string Join(string title, string artist)
    {
        if (title.Length == 0)
            return artist;
        if (artist.Length == 0)
            return title;

        return $"{title}{ResolveSeparator()}{artist}";
    }

    private IReadOnlyList<MediaLinkTimeSeekbar> BuildSeekbarLadder(bool wantsSeekbar)
    {
        if (!wantsSeekbar)
            return [MediaLinkTimeSeekbar.None];

        var ladder = new List<MediaLinkTimeSeekbar> { _mls.TimeSeekStyle };
        if (!_mls.AutoDowngradeSeekbar)
            return ladder;

        if (_mls.TimeSeekStyle == MediaLinkTimeSeekbar.NumbersAndSeekBar)
            ladder.Add(MediaLinkTimeSeekbar.SmallNumbers);

        if (_mls.TimeSeekStyle != MediaLinkTimeSeekbar.None)
            ladder.Add(MediaLinkTimeSeekbar.None);

        return ladder;
    }

    #endregion

    #region Timestamp / Progress Bar (budget-aware, moved from OSCController)

    private string ApplySeekbar(string text, MediaSessionInfo session, OscBuildContext context, MediaLinkTimeSeekbar style)
    {
        TimeSpan current = session.CurrentTime;
        TimeSpan full = session.FullTime;

        if (current.TotalSeconds < 0 || full.TotalSeconds < 0 || current > full)
            return text;

        double pct = full.TotalSeconds == 0 ? 0 : (current.TotalSeconds / full.TotalSeconds) * 100;

        switch (style)
        {
            case MediaLinkTimeSeekbar.NumbersAndSeekBar:
                var barStyle = _mediaLink.SelectedMediaLinkSeekbarStyle;
                string bar = SeekbarUtilities.CreateProgressBar(pct, current, full, ToSeekbarOptions(barStyle));
                if (string.IsNullOrWhiteSpace(bar))
                    return text;

                return barStyle?.ProgressBarOnTop == true ? $"{bar}\n{text}" : $"{text}\n{bar}";

            case MediaLinkTimeSeekbar.SmallNumbers:
                return $"{text} {SeekbarUtilities.CreateSmallNumbers(current, full)}";

            case MediaLinkTimeSeekbar.None:
            default:
                return text;
        }
    }

    private static SeekbarStyleOptions ToSeekbarOptions(MediaLinkStyle? style)
    {
        if (style == null)
        {
            return new SeekbarStyleOptions();
        }

        return new SeekbarStyleOptions
        {
            DisplayTime = style.DisplayTime,
            FilledCharacter = style.FilledCharacter,
            MiddleCharacter = style.MiddleCharacter,
            NonFilledCharacter = style.NonFilledCharacter,
            ProgressBarLength = style.ProgressBarLength,
            ShowTimeInSuperscript = style.ShowTimeInSuperscript,
            SpaceAgainObjects = style.SpaceAgainObjects,
            SpaceBetweenPreSuffixAndTime = style.SpaceBetweenPreSuffixAndTime,
            TimePrefix = style.TimePrefix,
            TimePreSuffixOnTheInside = style.TimePreSuffixOnTheInside,
            TimeSuffix = style.TimeSuffix
        };
    }

    #endregion

    #region Helpers (moved from OSCController)

    private string ResolveActionText(MediaSessionInfo session, bool isPlaying)
    {
        if (isPlaying)
        {
            string t = _mls.TextPlaying;
            if (!string.IsNullOrWhiteSpace(t)) return t;
            return session.IsVideo ? "Watching" : "Listening to";
        }
        return ResolvePausedText();
    }

    private string ResolvePausedText()
    {
        string t = _mls.TextPaused;
        return string.IsNullOrWhiteSpace(t) ? "Paused" : t;
    }

    private string ResolvePlayIcon(MediaSessionInfo session)
    {
        string i = _mls.IconPlay;
        if (!string.IsNullOrWhiteSpace(i)) return i;
        return session.IsVideo ? "🎬" : "🎵";
    }

    private string ResolvePauseIcon()
    {
        string i = _mls.IconPause;
        return !string.IsNullOrWhiteSpace(i) ? i : "⏸";
    }

    private string ResolveStopIcon()
    {
        if (!_mls.ShowStopIcon) return string.Empty;
        string i = _mls.IconStop;
        return !string.IsNullOrWhiteSpace(i) ? i : "⏹️";
    }

    private string ResolveSeparator()
    {
        string s = _mls.Separator ?? " ᵇʸ ";
        return s.Replace("\\n", "\n").Replace("\\r", "\r");
    }

    #endregion
}
