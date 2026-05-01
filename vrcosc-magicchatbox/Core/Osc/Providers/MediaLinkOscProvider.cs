using System;
using System.Globalization;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: MediaLink (Windows media sessions) → OSC segment.
/// Budget-aware: uses <see cref="OscBuildContext"/> to decide whether
/// progress bars / timestamps fit within the 144-char limit.
/// </summary>
public sealed class MediaLinkOscProvider : IOscProvider
{
    private readonly IntegrationSettings _intgr;
    private readonly MediaLinkSettings _mls;
    private readonly SpotifySettings _spotify;
    private readonly AppSettings _app;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly Lazy<IMediaLinkService> _mediaLinkSvc;

    public MediaLinkOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<MediaLinkSettings> mlsProvider,
        ISettingsProvider<SpotifySettings> spotifyProvider,
        ISettingsProvider<AppSettings> appProvider,
        MediaLinkDisplayState mediaLink,
        Lazy<IMediaLinkService> mediaLinkSvc)
    {
        _intgr = intgrProvider.Value;
        _mls = mlsProvider.Value;
        _spotify = spotifyProvider.Value;
        _app = appProvider.Value;
        _mediaLink = mediaLink;
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

        if (_mls.ShowOnlyOnChange)
        {
            double elapsed = (DateTime.UtcNow - _mediaLinkSvc.Value.LastMediaChangeTime).TotalSeconds;
            if (elapsed > _mls.TransientDuration)
                return null;
        }

        string text = BuildMediaText(context);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (_mls.UpperCase)
            text = text.ToUpper(CultureInfo.CurrentCulture);

        return new OscSegment { Text = text };
    }

    #region Core MediaLink logic (moved from OSCController.AddMediaLink)

    private string BuildMediaText(OscBuildContext context)
    {
        var sessions = _mediaLink.MediaSessions?.Where(s => s.IsActive) ?? Enumerable.Empty<MediaSessionInfo>();
        if (_intgr.IntgrSpotify && _spotify.MediaLinkCoexistence == SpotifyMediaLinkCoexistence.PreferSpotify)
            sessions = sessions.Where(s => !IsSpotifySession(s));

        MediaSessionInfo session = sessions.FirstOrDefault();
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
        string title = CreateMediaLinkTitle(session);

        string text;
        if (string.IsNullOrEmpty(title))
        {
            text = actionText;
        }
        else
        {
            text = _app.PrefixIconMusic && !string.IsNullOrWhiteSpace(playIcon)
                ? $"{playIcon} {title}"
                : $"{actionText} {title}";

            if (!session.IsLiveTime && session.TimePeekEnabled)
                text = CreateTimeStamp(text, session, context);
        }

        return text;
    }

    #endregion

    #region Timestamp / Progress Bar (budget-aware, moved from OSCController)

    private string CreateTimeStamp(string text, MediaSessionInfo session, OscBuildContext context)
    {
        TimeSpan current = session.CurrentTime;
        TimeSpan full = session.FullTime;

        if (current.TotalSeconds < 0 || full.TotalSeconds < 0 || current > full)
            return text;

        double pct = full.TotalSeconds == 0 ? 0 : (current.TotalSeconds / full.TotalSeconds) * 100;
        int available = context.RemainingCharsIf(text) - 4; // leave small margin
        var style = _mediaLink.SelectedMediaLinkSeekbarStyle;

        switch (_mls.TimeSeekStyle)
        {
            case MediaLinkTimeSeekbar.NumbersAndSeekBar:
                string bar = SeekbarUtilities.CreateProgressBar(pct, session.CurrentTime, session.FullTime, ToSeekbarOptions(style));
                if (!string.IsNullOrWhiteSpace(bar))
                {
                    string candidate = style.ProgressBarOnTop ? $"{bar}\n{text}" : $"{text}\n{bar}";
                    if (context.WouldFit(candidate))
                        return candidate;
                }
                if (_mls.AutoDowngradeSeekbar)
                    goto case MediaLinkTimeSeekbar.SmallNumbers;
                break;

            case MediaLinkTimeSeekbar.SmallNumbers:
                string small = SeekbarUtilities.CreateSmallNumbers(current, full);
                string withSmall = $"{text} {small}";
                if (context.WouldFit(withSmall))
                    return withSmall;
                if (_mls.AutoDowngradeSeekbar)
                    goto case MediaLinkTimeSeekbar.None;
                break;

            case MediaLinkTimeSeekbar.None:
            default:
                return text;
        }

        return text;
    }

    private static SeekbarStyleOptions ToSeekbarOptions(MediaLinkStyle style)
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

    private string CreateMediaLinkTitle(MediaSessionInfo session)
    {
        var sb = new StringBuilder();
        if (session.ShowTitle && !string.IsNullOrEmpty(session.Title))
            sb.Append(session.Title);
        if (session.ShowArtist && !string.IsNullOrEmpty(session.Artist))
        {
            if (sb.Length > 0) sb.Append(ResolveSeparator());
            sb.Append(session.Artist);
        }
        return sb.ToString();
    }

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
