using System;
using System.Globalization;
using System.Linq;
using System.Text;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
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
    private readonly AppSettings _app;
    private readonly MediaLinkDisplayState _mediaLink;
    private readonly Lazy<IMediaLinkService> _mediaLinkSvc;

    public MediaLinkOscProvider(
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<MediaLinkSettings> mlsProvider,
        ISettingsProvider<AppSettings> appProvider,
        MediaLinkDisplayState mediaLink,
        Lazy<IMediaLinkService> mediaLinkSvc)
    {
        _intgr = intgrProvider.Value;
        _mls = mlsProvider.Value;
        _app = appProvider.Value;
        _mediaLink = mediaLink;
        _mediaLinkSvc = mediaLinkSvc;
    }

    public string SortKey => "MediaLink";
    public string UiKey => "MediaLink";
    public int Priority => 20;

    public bool IsEnabledForCurrentMode(bool isVR)
        => isVR ? _intgr.IntgrMediaLink_VR : _intgr.IntgrMediaLink_DESKTOP;

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
        MediaSessionInfo session = _mediaLink.MediaSessions?.FirstOrDefault(s => s.IsActive);
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
                string bar = CreateProgressBar(pct, session, style);
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
                string small = $"{TextUtilities.TransformToSuperscript(FormatTimeSpan(current))} l {TextUtilities.TransformToSuperscript(FormatTimeSpan(full))}";
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

    private string CreateProgressBar(double percentage, MediaSessionInfo session, MediaLinkStyle style)
    {
        try
        {
            if (style == null
                || string.IsNullOrEmpty(style.FilledCharacter)
                || string.IsNullOrEmpty(style.MiddleCharacter)
                || string.IsNullOrEmpty(style.NonFilledCharacter))
                return string.Empty;

            int totalBlocks = style.ProgressBarLength;

            string currentStr = style.DisplayTime && style.ShowTimeInSuperscript
                ? TextUtilities.TransformToSuperscript(FormatTimeSpan(session.CurrentTime))
                : FormatTimeSpan(session.CurrentTime);

            string fullStr = style.DisplayTime && style.ShowTimeInSuperscript
                ? TextUtilities.TransformToSuperscript(FormatTimeSpan(session.FullTime))
                : FormatTimeSpan(session.FullTime);

            if (style.DisplayTime && totalBlocks > 0)
            {
                int timeLen = currentStr.Length + fullStr.Length + 1;
                if (totalBlocks > timeLen)
                    totalBlocks -= timeLen;
            }

            int filled = (int)(percentage / (100.0 / totalBlocks));
            string filledBar = string.Concat(Enumerable.Repeat(style.FilledCharacter, filled));
            string emptyBar = string.Concat(Enumerable.Repeat(style.NonFilledCharacter, totalBlocks - filled));
            string progressBar = filledBar + style.MiddleCharacter + emptyBar;

            return FormatProgressBarWithTime(currentStr, fullStr, progressBar, style);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return string.Empty;
        }
    }

    private static string FormatProgressBarWithTime(string current, string full, string bar, MediaLinkStyle style)
    {
        string sp = style.SpaceAgainObjects ? " " : "";
        string psp = style.SpaceBetweenPreSuffixAndTime ? " " : "";

        if (style.DisplayTime)
        {
            if (style.TimePreSuffixOnTheInside)
            {
                return string.IsNullOrWhiteSpace(style.TimePrefix) || string.IsNullOrWhiteSpace(style.TimeSuffix)
                    ? $"{current}{sp}{bar}{sp}{full}"
                    : $"{current}{psp}{style.TimePrefix}{bar}{style.TimeSuffix}{psp}{full}";
            }
            return $"{style.TimePrefix}{psp}{current}{sp}{bar}{sp}{full}{psp}{style.TimeSuffix}";
        }

        return style.TimePreSuffixOnTheInside
            ? $"{style.TimePrefix}{bar}{style.TimeSuffix}"
            : $"{style.TimePrefix}{psp}{bar}{psp}{style.TimeSuffix}";
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

    private static string FormatTimeSpan(TimeSpan ts)
    {
        return ts.Hours > 0
            ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    #endregion
}
