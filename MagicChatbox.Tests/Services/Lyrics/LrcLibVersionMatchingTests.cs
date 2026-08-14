using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Services.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Services.Lyrics;

/// <summary>
/// Picking the right version of a song out of a database that holds several of them. The version is
/// whatever trails the song name, a search may drop it once the exact title finds nothing, and the
/// running time then decides which of the same-named records is playing.
/// </summary>
public class LrcLibVersionMatchingTests
{
    private static LyricsQuery Query(string title, string artist, double seconds) => new()
    {
        Title = title,
        Artist = artist,
        Duration = TimeSpan.FromSeconds(seconds),
    };

    private static LyricsCandidate Candidate(string title, string artist, double seconds, bool synced = true)
        => new(title, artist, "Album", seconds, false, synced);

    #region The general rule

    [Fact]
    public void A_version_filed_under_the_plain_song_name_is_found_by_its_length()
    {
        // The playing track names a version; the database does not, and records the difference only
        // in its running time.
        var candidates = new[]
        {
            Candidate("Song", "Artist", 180),
            Candidate("Song", "Artist", 300),
        };

        var match = LyricsMatchScorer.PickBest(candidates, Query("Song (Some Version)", "Artist", 300));

        Assert.Equal(1, match.Index);
    }

    [Fact]
    public void A_different_version_of_the_same_length_loses_to_the_plain_record()
    {
        // Same song, same artist, same length, real lyrics on both. Only the version differs, so
        // nothing but the qualifier can separate them.
        var candidates = new[]
        {
            Candidate("Song (Another Version)", "Artist", 300),
            Candidate("Song", "Artist", 300),
        };

        var match = LyricsMatchScorer.PickBest(candidates, Query("Song (Some Version)", "Artist", 300));

        Assert.Equal(1, match.Index);
    }

    [Fact]
    public void A_version_that_agrees_is_preferred_over_one_that_says_nothing()
    {
        var candidates = new[]
        {
            Candidate("Song", "Artist", 300),
            Candidate("Song (Some Version)", "Artist", 300),
        };

        var match = LyricsMatchScorer.PickBest(candidates, Query("Song (Some Version)", "Artist", 300));

        Assert.Equal(1, match.Index);
    }

    [Fact]
    public void Records_without_synced_lyrics_never_win_however_well_they_are_named()
    {
        var candidates = new[]
        {
            Candidate("Song (Some Version)", "Artist", 300, synced: false),
            Candidate("Song", "Artist", 300),
        };

        var match = LyricsMatchScorer.PickBest(candidates, Query("Song (Some Version)", "Artist", 300));

        Assert.Equal(1, match.Index);
    }

    [Theory]
    [InlineData("Song (Some Version)")]
    [InlineData("Song - Some Version")]
    [InlineData("Song [Some Version]")]
    public void The_version_is_recognised_however_it_is_punctuated(string playingTitle)
    {
        var candidates = new[] { Candidate("Song", "Artist", 300) };

        Assert.Equal(0, LyricsMatchScorer.PickBest(candidates, Query(playingTitle, "Artist", 300)).Index);
    }

    #endregion

    #region Dropping detail costs tolerance

    [Fact]
    public void A_full_title_search_tolerates_a_loosely_matching_length()
    {
        var candidates = new[] { Candidate("Song", "Artist", 309) };

        Assert.True(LyricsMatchScorer.PickBest(candidates, Query("Song", "Artist", 300)).Index >= 0);
    }

    [Fact]
    public void A_stripped_search_does_not()
    {
        // Nine seconds out is within the usual slack, but this result came back from a search that
        // had the version thrown away - so the length is the only evidence left and it has to hold.
        var candidates = new[] { Candidate("Song", "Artist", 309) };

        var match = LyricsMatchScorer.PickBest(
            candidates,
            Query("Song (Some Version)", "Artist", 300),
            LyricsMatchOptions.For(LyricsMatchStrictness.Balanced, requireCloseDuration: true));

        Assert.Equal(-1, match.Index);
    }

    [Fact]
    public void A_stripped_search_still_accepts_a_length_that_really_matches()
    {
        var candidates = new[] { Candidate("Song", "Artist", 301) };

        var match = LyricsMatchScorer.PickBest(
            candidates,
            Query("Song (Some Version)", "Artist", 300),
            LyricsMatchOptions.For(LyricsMatchStrictness.Balanced, requireCloseDuration: true));

        Assert.Equal(0, match.Index);
    }

    [Fact]
    public void A_stripped_search_refuses_to_guess_when_the_length_is_unknown()
    {
        var candidates = new[] { Candidate("Song", "Artist", 300) };

        var match = LyricsMatchScorer.PickBest(
            candidates,
            Query("Song (Some Version)", "Artist", 0),
            LyricsMatchOptions.For(LyricsMatchStrictness.Balanced, requireCloseDuration: true));

        Assert.Equal(-1, match.Index);
    }

    #endregion

    #region The ladder

    [Fact]
    public void The_full_title_is_always_tried_first()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song (Some Version)", "Artist", 300));

        Assert.Contains("Some%20Version", steps[0].Url, StringComparison.Ordinal);
        Assert.False(steps[0].RequiresCloseDuration);
    }

    [Fact]
    public void The_version_is_only_dropped_after_the_exact_title_has_been_tried()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song (Some Version)", "Artist", 300));

        int firstStripped = steps.ToList().FindIndex(s => s.RequiresCloseDuration);
        Assert.True(firstStripped > 0, "a stripped attempt must never be the opening move");
        Assert.All(steps.Take(firstStripped), s => Assert.False(s.RequiresCloseDuration));
    }

    [Fact]
    public void Every_stripped_attempt_demands_a_close_length()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song (Some Version)", "Artist", 300));

        Assert.Contains(steps, s => s.RequiresCloseDuration);
        Assert.All(
            steps.Where(s => s.RequiresCloseDuration),
            s => Assert.DoesNotContain("Some%20Version", s.Url, StringComparison.Ordinal));
    }

    [Fact]
    public void A_title_with_no_version_produces_no_stripped_attempts_and_no_duplicates()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song", "Artist", 300));

        Assert.DoesNotContain(steps, s => s.RequiresCloseDuration);
        Assert.Equal(steps.Count, steps.Select(s => s.Url).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_credit_list_earns_one_last_attempt_under_the_lead_name_alone()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song", "Lead, Second & Third", 300));

        Assert.Contains(steps, s => s.Url.EndsWith("q=Lead%20Song", StringComparison.Ordinal));
    }

    #endregion

    #region A full search response

    // The shape a search comes back in when a database holds several versions of one song. Three
    // records share a length, so only the version separates them: one is named exactly like the
    // playing track but carries no synced lyrics, one is a different version of the same length, and
    // one is the right recording filed under the plain song name with the version in the album.
    private const string SearchResponse = """
    [
      { "id": 1, "trackName": "paper lanterns",                    "artistName": "Northbound",             "albumName": "Slow Hours",           "duration": 167.063,    "instrumental": false, "syncedLyrics": null },
      { "id": 2, "trackName": "paper lanterns - Halcyon Remix",    "artistName": "Northbound",             "albumName": "Slow Hours (Reworks)", "duration": 267.0,      "instrumental": false, "syncedLyrics": null },
      { "id": 3, "trackName": "paper lanterns",                    "artistName": "Northbound, Aria Vale",  "albumName": "Slow Hours",           "duration": 167.063968, "instrumental": false, "syncedLyrics": "[00:12.37] line one\n[00:16.62] line two" },
      { "id": 4, "trackName": "paper lanterns",                    "artistName": "Northbound, Aria Vale",  "albumName": "Slow Hours (Reworks)", "duration": 267.0,      "instrumental": false, "syncedLyrics": "[00:12.37] line one\n[00:16.62] line two" },
      { "id": 5, "trackName": "paper lanterns (feat. Aria Vale)",  "artistName": "Northbound",             "albumName": "Slow Hours",           "duration": 167.0,      "instrumental": false, "syncedLyrics": "[00:12.37] line one\n[00:16.62] line two" },
      { "id": 6, "trackName": "paper lanterns (Vesper Remix)",     "artistName": "Northbound",             "albumName": "Slow Hours",           "duration": 267.0,      "instrumental": false, "syncedLyrics": "[00:12.37] line one\n[00:16.62] line two" }
    ]
    """;

    private static readonly LyricsQuery PlayingTrack = Query("paper lanterns (Halcyon Remix)", "Northbound", 267);

    private static JObject? PickFromResponse(LyricsQuery query, bool strict = true)
        => LrcLibLyricsProvider.PickBest(
            JArray.Parse(SearchResponse).OfType<JObject>().ToList(),
            query,
            LyricsMatchOptions.For(LyricsMatchStrictness.Balanced, strict));

    [Fact]
    public void The_right_record_is_chosen_from_a_full_response()
    {
        JObject? picked = PickFromResponse(PlayingTrack);

        Assert.NotNull(picked);
        Assert.Equal(267, picked!["duration"]!.Value<double>(), 0);
        Assert.Equal("paper lanterns", picked["trackName"]!.ToString());
        Assert.NotNull(picked["syncedLyrics"]!.ToString());
    }

    [Fact]
    public void The_shorter_recording_of_the_same_song_is_not_used()
    {
        Assert.NotEqual(167, PickFromResponse(PlayingTrack)!["duration"]!.Value<double>(), 0);
    }

    [Fact]
    public void Asking_for_the_short_recording_still_gets_the_short_recording()
    {
        JObject? picked = PickFromResponse(Query("paper lanterns", "Northbound", 167), strict: false);

        Assert.NotNull(picked);
        Assert.Equal(167, picked!["duration"]!.Value<double>(), 0);
    }

    [Fact]
    public void A_single_record_answer_is_scored_rather_than_trusted()
    {
        const string wrongLength = """
        { "trackName": "Song", "artistName": "Artist", "duration": 180.0, "instrumental": false, "syncedLyrics": "[00:12.37] line one" }
        """;

        Assert.Null(LrcLibLyricsProvider.ParseResponse(wrongLength, Query("Song (Some Version)", "Artist", 300)));
    }

    [Fact]
    public void A_single_record_answer_that_matches_is_accepted()
    {
        const string rightLength = """
        { "trackName": "Song", "artistName": "Artist", "duration": 300.0, "instrumental": false, "syncedLyrics": "[00:12.37] line one" }
        """;

        Assert.NotNull(LrcLibLyricsProvider.ParseResponse(rightLength, Query("Song (Some Version)", "Artist", 300)));
    }

    #endregion
}
