using System;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;
using Windows.Media.Control;
using Xunit;
using static WindowsMediaController.MediaManager;

namespace MagicChatbox.Tests.Core.Osc;

/// <summary>
/// A chatbox line over 144 characters is dropped whole rather than clipped, so a song with a long
/// credit list used to take itself off screen entirely. These pin the shortening that prevents it,
/// and the order it runs in relative to the seekbar downgrade.
/// </summary>
public class MediaLinkShorteningTests
{
    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class StubMediaLinkService : IMediaLinkService
    {
        public DateTime LastMediaChangeTime => DateTime.UtcNow;
        public void Start() { }
        public void Dispose() { }
        public void SelectMediaSession(MediaSessionInfo sessionInfo) { }
        public Task MediaManager_NextAsync(MediaSessionInfo sessionInfo) => Task.CompletedTask;
        public Task MediaManager_PlayPauseAsync(MediaSessionInfo sessionInfo) => Task.CompletedTask;
        public Task MediaManager_PreviousAsync(MediaSessionInfo sessionInfo) => Task.CompletedTask;
        public Task MediaManager_SeekTo(MediaSessionInfo sessionInfo, double position) => Task.CompletedTask;
        public void MediaManager_OnAnyTimelinePropertyChanged(MediaSession sender, GlobalSystemMediaTransportControlsSessionTimelineProperties args) { }
        public void SessionRestore(MediaSessionInfo session) { }
    }

    private const string CrowdedCredit =
        "The Weeknd, Ariana Grande, Doja Cat, Megan Thee Stallion, Post Malone, " +
        "Travis Scott, Kendrick Lamar, Bad Bunny, Dua Lipa, Billie Eilish";

    private static readonly TimeSpan TrackLength = TimeSpan.FromMinutes(3);

    private sealed class Harness
    {
        public required MediaLinkSettings Settings { get; init; }
        public required MediaSessionInfo Session { get; init; }
        public required MediaLinkOscProvider Provider { get; init; }

        public string Build()
        {
            var context = new OscBuildContext { Separator = " ┆ ", Prefix = string.Empty, Suffix = string.Empty };
            return Provider.TryBuild(context)?.Text ?? string.Empty;
        }
    }

    private static Harness Build(
        string title,
        string artist,
        bool shortenToFit,
        bool withSeekbar = true,
        bool tidyTitles = false)
    {
        var integrations = new IntegrationSettings
        {
            IntgrScanMediaLink = true,
            IntgrLyrics = false,
            IntgrSpotify = false,
        };

        var mediaLinkSettings = new MediaLinkSettings
        {
            ShowOnlyOnChange = false,
            ShortenToFit = shortenToFit,
            TidyTitles = tidyTitles,
            TimeSeekStyle = MediaLinkTimeSeekbar.SmallNumbers,
            AutoDowngradeSeekbar = true,
            TextPlaying = "Listening to",
            Separator = " ᵇʸ ",
        };

        // The icon prefix would make the expected length depend on emoji width, so the plain
        // "Listening to" wording is used instead.
        var appSettings = new AppSettings { PrefixIconMusic = false };

        var displayState = new MediaLinkDisplayState();

        var session = new MediaSessionInfo(mediaLinkSettings, displayState)
        {
            IsActive = true,
            PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            ShowTitle = true,
            ShowArtist = true,
            Title = title,
            Artist = artist,
            TimePeekEnabled = withSeekbar,
            FullTime = TrackLength,
        };
        session.SetPositionFromSample(TimeSpan.FromSeconds(30), DateTime.UtcNow);
        displayState.MediaSessions.Add(session);

        var provider = new MediaLinkOscProvider(
            new StubSettingsProvider<IntegrationSettings>(integrations),
            new StubSettingsProvider<MediaLinkSettings>(mediaLinkSettings),
            new StubSettingsProvider<SpotifySettings>(new SpotifySettings()),
            new StubSettingsProvider<AppSettings>(appSettings),
            displayState,
            new SpotifyDisplayState(),
            new LyricsDisplayState(),
            new Lazy<IMediaLinkService>(() => new StubMediaLinkService()));

        return new Harness { Settings = mediaLinkSettings, Session = session, Provider = provider };
    }

    [Fact]
    public void Without_shortening_a_crowded_credit_list_overflows_the_line()
    {
        string text = Build("Save Your Tears", CrowdedCredit, shortenToFit: false).Build();

        // The premise of the whole feature: this is what used to get thrown away downstream.
        Assert.True(text.Length > OscBuildContext.MaxOscLength,
            $"expected the unshortened line to overflow, but it was {text.Length} characters");
        Assert.Contains("Billie Eilish", text);
    }

    [Fact]
    public void Shortening_keeps_the_song_on_the_line()
    {
        string text = Build("Save Your Tears", CrowdedCredit, shortenToFit: true).Build();

        Assert.True(text.Length <= OscBuildContext.MaxOscLength,
            $"expected the shortened line to fit, but it was {text.Length} characters");
        Assert.Contains("Save Your Tears", text);
        Assert.Contains("The Weeknd", text);
    }

    [Fact]
    public void Shortening_counts_the_credits_it_dropped()
    {
        string text = Build("Save Your Tears", CrowdedCredit, shortenToFit: true).Build();

        Assert.Matches(@"\+\d+", text);
        Assert.DoesNotContain("Billie Eilish", text);
    }

    [Fact]
    public void Shortening_happens_before_the_seekbar_is_downgraded()
    {
        string text = Build("Save Your Tears", CrowdedCredit, shortenToFit: true).Build();

        // The configured style is SmallNumbers with auto-downgrade allowed. Dropping credits has to
        // be tried first, so the track length must still be on the line.
        Assert.Contains(TextUtilities.TransformToSuperscript("3:00"), text);
        Assert.Matches(@"\+\d+", text);
    }

    [Fact]
    public void The_seekbar_is_only_given_up_once_the_credits_cannot_shrink_further()
    {
        // A comma-free credit cannot be cut down, so the ladder runs out at "title on its own" - and
        // this title is sized to fit only once the seekbar goes too. That makes the bar the last
        // thing surrendered, which is the whole point of the ordering.
        string title = new('T', 125);
        string text = Build(title, new string('A', 60), shortenToFit: true).Build();

        Assert.True(text.Length <= OscBuildContext.MaxOscLength,
            $"expected the line to fit, but it was {text.Length} characters");
        Assert.DoesNotContain(TextUtilities.TransformToSuperscript("3:00"), text);
        Assert.DoesNotContain("AAAA", text);
        Assert.Contains("TTTTTTTTTT", text);
    }

    [Fact]
    public void A_song_that_already_fits_is_left_exactly_as_it_was()
    {
        string shortened = Build("Blinding Lights", "The Weeknd", shortenToFit: true).Build();
        string untouched = Build("Blinding Lights", "The Weeknd", shortenToFit: false).Build();

        Assert.Equal(untouched, shortened);
        Assert.Contains("Blinding Lights ᵇʸ The Weeknd", shortened);
    }

    [Fact]
    public void The_title_survives_even_when_no_artist_rendering_fits()
    {
        string title = new('T', 120);
        string text = Build(title, CrowdedCredit, shortenToFit: true).Build();

        Assert.True(text.Length <= OscBuildContext.MaxOscLength,
            $"expected the line to fit, but it was {text.Length} characters");
        Assert.Contains("TTTTTTTTTT", text);
    }

    private const string YoutubeUpload = "Rick Astley - Never Gonna Give You Up (Official Music Video) [4K]";

    [Fact]
    public void A_youtube_upload_loses_its_decoration_and_its_duplicated_channel_name()
    {
        string text = Build(YoutubeUpload, "RickAstleyVEVO", shortenToFit: true, tidyTitles: true).Build();

        Assert.Contains("Never Gonna Give You Up ᵇʸ RickAstleyVEVO", text);
        Assert.DoesNotContain("Official", text);
        Assert.DoesNotContain("4K", text);
    }

    [Fact]
    public void Tidying_a_youtube_upload_buys_back_a_third_of_the_line()
    {
        string tidied = Build(YoutubeUpload, "RickAstleyVEVO", shortenToFit: true, tidyTitles: true).Build();
        string raw = Build(YoutubeUpload, "RickAstleyVEVO", shortenToFit: true, tidyTitles: false).Build();

        Assert.True(raw.Length - tidied.Length >= 40,
            $"expected tidying to save real space, but it went from {raw.Length} to {tidied.Length}");
    }

    [Fact]
    public void Tidying_can_be_turned_off()
    {
        string text = Build(YoutubeUpload, "RickAstleyVEVO", shortenToFit: true, tidyTitles: false).Build();

        Assert.Contains("(Official Music Video)", text);
    }

    [Fact]
    public void A_title_too_long_on_its_own_is_cut_rather_than_dropped()
    {
        string title = new('T', 200);
        string text = Build(title, "The Weeknd", shortenToFit: true).Build();

        Assert.True(text.Length <= OscBuildContext.MaxOscLength,
            $"expected the line to fit, but it was {text.Length} characters");
        Assert.EndsWith("…", text);
    }
}
