using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using vrcosc_magicchatbox.Services.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class LyricsMatchStrictnessTests
{
    private static LyricsQuery Query(string title = "Song Title Words", string artist = "Artist Name", double seconds = 300)
        => new() { Title = title, Artist = artist, Duration = TimeSpan.FromSeconds(seconds) };

    private static LyricsCandidate Candidate(string title, string artist, double seconds)
        => new(title, artist, "Album", seconds, false, true);

    private static bool Accepted(LyricsCandidate candidate, LyricsMatchStrictness strictness)
        => LyricsMatchScorer.PickBest([candidate], Query(), LyricsMatchOptions.For(strictness)).Index >= 0;

    /// <summary>A middling candidate: most of the title, some of the artist, a few seconds out.</summary>
    private static LyricsCandidate Borderline
        => Candidate("Song Title Other Different", "Artist Other Someone", 304);

    [Fact]
    public void Relaxed_accepts_a_borderline_match_that_strict_turns_down()
    {
        Assert.True(Accepted(Borderline, LyricsMatchStrictness.Relaxed));
        Assert.False(Accepted(Borderline, LyricsMatchStrictness.Strict));
    }

    [Fact]
    public void Loosening_the_setting_only_ever_accepts_more()
    {
        // The property that makes the setting meaningful, checked across a spread of quality rather
        // than trusting one hand-computed score.
        var spread = new[]
        {
            Candidate("Song Title Words", "Artist Name", 300),
            Candidate("Song Title Words", "Artist Name", 304),
            Candidate("Song Title Words Extra", "Artist Other Someone", 309),
            Borderline,
            Candidate("Song Title Other Different Words Here", "Artist Other", 310),
            Candidate("Something Entirely Different", "Nobody", 120),
        };

        foreach (var candidate in spread)
        {
            if (Accepted(candidate, LyricsMatchStrictness.Strict))
                Assert.True(Accepted(candidate, LyricsMatchStrictness.Balanced), $"balanced rejected {candidate.TrackName}");

            if (Accepted(candidate, LyricsMatchStrictness.Balanced))
                Assert.True(Accepted(candidate, LyricsMatchStrictness.Relaxed), $"relaxed rejected {candidate.TrackName}");
        }
    }

    [Fact]
    public void The_three_settings_are_not_all_the_same()
    {
        var spread = new[]
        {
            Candidate("Song Title Words", "Artist Name", 300),
            Borderline,
            Candidate("Song Title Words Extra", "Artist Other Someone", 309),
        };

        int strict = spread.Count(c => Accepted(c, LyricsMatchStrictness.Strict));
        int relaxed = spread.Count(c => Accepted(c, LyricsMatchStrictness.Relaxed));

        Assert.True(relaxed > strict, $"relaxed accepted {relaxed}, strict accepted {strict}");
    }

    [Fact]
    public void An_exact_match_is_accepted_at_every_strictness()
    {
        var exact = Candidate("Song Title Words", "Artist Name", 300);

        foreach (LyricsMatchStrictness strictness in Enum.GetValues<LyricsMatchStrictness>())
            Assert.True(Accepted(exact, strictness), $"{strictness} turned down an exact match");
    }

    [Fact]
    public void Nonsense_is_turned_down_at_every_strictness()
    {
        var nonsense = Candidate("Something Entirely Different", "Nobody", 120);

        foreach (LyricsMatchStrictness strictness in Enum.GetValues<LyricsMatchStrictness>())
            Assert.False(Accepted(nonsense, strictness), $"{strictness} accepted nonsense");
    }

    [Fact]
    public void The_default_options_behave_as_balanced()
    {
        Assert.Equal(
            Accepted(Borderline, LyricsMatchStrictness.Balanced),
            LyricsMatchScorer.PickBest([Borderline], Query()).Index >= 0);
    }

    [Fact]
    public void Turning_broadening_off_removes_the_stripped_attempts()
    {
        var query = Query("Song (Some Version)");

        var with = LrcLibLyricsProvider.BuildLookupSteps(query, allowBroadening: true);
        var without = LrcLibLyricsProvider.BuildLookupSteps(query, allowBroadening: false);

        Assert.Contains(with, s => s.RequiresCloseDuration);
        Assert.DoesNotContain(without, s => s.RequiresCloseDuration);
        Assert.True(without.Count < with.Count);
    }

    [Fact]
    public void Turning_broadening_off_still_tries_the_exact_title()
    {
        var steps = LrcLibLyricsProvider.BuildLookupSteps(Query("Song (Some Version)"), allowBroadening: false);

        Assert.NotEmpty(steps);
        Assert.All(steps, s => Assert.Contains("Some%20Version", s.Url, StringComparison.Ordinal));
    }
}
