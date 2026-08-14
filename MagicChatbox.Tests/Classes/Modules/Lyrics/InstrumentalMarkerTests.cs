using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Lyrics;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Lyrics;

public class InstrumentalMarkerTests
{
    private static LyricsSettings Settings(
        bool showGapMarker = true,
        LyricsInstrumentalMarker marker = LyricsInstrumentalMarker.Note)
        => new() { ShowGapMarker = showGapMarker, InstrumentalMarker = marker, MinimumCharacters = 24 };

    private static LyricCursor Gap()
        => new(LyricCursorKind.InstrumentalGap, 3, string.Empty, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40));

    private static LyricCursor Intro()
        => new(LyricCursorKind.BeforeFirstLine, -1, string.Empty, TimeSpan.Zero, TimeSpan.FromSeconds(12));

    private static LyricCursor Line(string text)
        => new(LyricCursorKind.Line, 0, text, TimeSpan.Zero, TimeSpan.FromSeconds(5));

    [Fact]
    public void A_fresh_install_gets_the_trailing_dots()
        => Assert.Equal(LyricsInstrumentalMarker.TrailingDots, new LyricsSettings().InstrumentalMarker);

    [Fact]
    public void The_stored_number_of_a_style_never_moves()
    {
        // These go into the settings file. Withdrawing a style must leave gaps rather than shuffle
        // the survivors down, or someone's saved choice quietly becomes a different style.
        Assert.Equal(0, (int)LyricsInstrumentalMarker.Note);
        Assert.Equal(1, (int)LyricsInstrumentalMarker.BouncingNotes);
        Assert.Equal(3, (int)LyricsInstrumentalMarker.TrailingDots);
        Assert.Equal(6, (int)LyricsInstrumentalMarker.Vinyl);
        Assert.Equal(8, (int)LyricsInstrumentalMarker.Pulse);
        Assert.Equal(9, (int)LyricsInstrumentalMarker.BouncingBall);
    }

    [Fact]
    public void A_withdrawn_style_left_in_a_settings_file_falls_back_to_a_note()
    {
        // The meters, the spinner and the sparkle - all removed because VRChat's font does not
        // draw the characters they needed.
        foreach (int withdrawn in new[] { 2, 4, 5, 7, 10 })
        {
            var settings = Settings(marker: (LyricsInstrumentalMarker)withdrawn);

            Assert.Equal("♪", LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(9), 140, settings));
        }
    }

    [Fact]
    public void A_still_marker_never_changes()
    {
        var seen = Enumerable.Range(0, 10)
            .Select(s => InstrumentalMarker.Render(LyricsInstrumentalMarker.Note, TimeSpan.FromSeconds(s)))
            .Distinct()
            .ToList();

        Assert.Equal(["♪"], seen);
    }

    public static TheoryData<LyricsInstrumentalMarker> AnimatedStyles()
    {
        var data = new TheoryData<LyricsInstrumentalMarker>();
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
        {
            if (InstrumentalMarker.Frames(style).Count > 1)
                data.Add(style);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AnimatedStyles))]
    public void An_animated_marker_uses_every_frame_it_has(LyricsInstrumentalMarker style)
    {
        var frames = InstrumentalMarker.Frames(style);

        var seen = Enumerable.Range(0, frames.Count * 3)
            .Select(s => InstrumentalMarker.Render(style, TimeSpan.FromSeconds(s)))
            .ToHashSet();

        Assert.Equal(frames.ToHashSet(), seen);
    }

    [Fact]
    public void Every_style_has_frames_and_none_of_them_are_blank()
    {
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
        {
            var frames = InstrumentalMarker.Frames(style);

            Assert.NotEmpty(frames);
            Assert.All(frames, f => Assert.False(string.IsNullOrWhiteSpace(f), $"{style} has a blank frame"));
        }
    }

    [Fact]
    public void No_frame_costs_double_by_living_outside_the_basic_plane()
    {
        // A surrogate pair spends two of the 144 characters while looking like one glyph.
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
        {
            foreach (string frame in InstrumentalMarker.Frames(style))
                Assert.All(frame, c => Assert.False(char.IsSurrogate(c), $"{style} frame \"{frame}\" is not BMP"));
        }
    }

    [Fact]
    public void No_style_is_wide_enough_to_crowd_the_line()
    {
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
            Assert.True(InstrumentalMarker.MaxWidth(style) <= 8, $"{style} is {InstrumentalMarker.MaxWidth(style)} wide");
    }

    [Fact]
    public void Every_style_is_offered_in_the_options_list()
    {
        Assert.Equal(
            Enum.GetValues<LyricsInstrumentalMarker>().ToHashSet(),
            LyricsSettings.AvailableInstrumentalMarkers.ToHashSet());
    }

    [Fact]
    public void The_frame_follows_the_song_rather_than_a_clock_of_its_own()
    {
        // Same position, same frame - so two ticks that report the same time never flicker.
        var at = TimeSpan.FromSeconds(41);

        Assert.Equal(
            InstrumentalMarker.Render(LyricsInstrumentalMarker.Vinyl, at),
            InstrumentalMarker.Render(LyricsInstrumentalMarker.Vinyl, at));
    }

    [Fact]
    public void A_negative_position_does_not_throw_or_index_backwards()
    {
        string frame = InstrumentalMarker.Render(LyricsInstrumentalMarker.BouncingNotes, TimeSpan.FromSeconds(-5));

        Assert.Contains(frame, InstrumentalMarker.Frames(LyricsInstrumentalMarker.BouncingNotes));
    }

    [Fact]
    public void An_instrumental_break_produces_a_marker()
    {
        // The regression that mattered: this used to come back empty, so the marker never reached
        // the chatbox at all.
        string text = LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(20), 140, Settings());

        Assert.Equal("♪", text);
    }

    [Fact]
    public void The_intro_before_the_first_line_produces_one_too()
    {
        string text = LyricSegmentFormatter.Build(Intro(), TimeSpan.FromSeconds(3), 140, Settings());

        Assert.Equal("♪", text);
    }

    [Fact]
    public void Turning_the_marker_off_leaves_the_break_silent()
    {
        Assert.Equal(
            string.Empty,
            LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(20), 140, Settings(showGapMarker: false)));
    }

    [Fact]
    public void The_chosen_style_is_the_one_that_shows()
    {
        var settings = Settings(marker: LyricsInstrumentalMarker.Vinyl);

        string text = LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(20), 140, settings);

        Assert.Contains(text, InstrumentalMarker.Frames(LyricsInstrumentalMarker.Vinyl));
    }

    [Fact]
    public void A_marker_is_not_held_to_the_minimum_a_lyric_line_must_meet()
    {
        // Four characters is far below MinimumCharacters, but a marker is complete at one.
        var settings = Settings();

        Assert.Equal("♪", LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(20), 4, settings));
        Assert.Equal(string.Empty, LyricSegmentFormatter.Build(Line("some words here"), TimeSpan.Zero, 4, settings));
    }

    [Fact]
    public void A_marker_too_wide_for_the_space_falls_back_to_a_plain_note()
    {
        var settings = Settings(marker: LyricsInstrumentalMarker.TrailingDots);

        Assert.Equal("♪", LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(20), 1, settings));
    }

    [Fact]
    public void A_style_that_fits_never_blinks_out_mid_animation()
    {
        // Budgeting frame by frame would show the narrow frames and drop the wide ones, so a style
        // whose frames differ in width has to be judged by its widest.
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
        {
            var settings = Settings(marker: style);
            int budget = InstrumentalMarker.MaxWidth(style);

            for (int second = 0; second < 20; second++)
            {
                string text = LyricSegmentFormatter.Build(Gap(), TimeSpan.FromSeconds(second), budget, settings);
                Assert.False(string.IsNullOrEmpty(text), $"{style} vanished at second {second}");
            }
        }
    }

    [Fact]
    public void A_lyric_line_still_wins_over_a_marker()
    {
        string text = LyricSegmentFormatter.Build(Line("the words"), TimeSpan.Zero, 140, Settings());

        Assert.Contains("the words", text);
    }

    [Fact]
    public void Every_style_reports_a_width_that_covers_its_widest_frame()
    {
        foreach (LyricsInstrumentalMarker style in Enum.GetValues<LyricsInstrumentalMarker>())
        {
            IReadOnlyList<string> frames = InstrumentalMarker.Frames(style);
            Assert.All(frames, f => Assert.True(f.Length <= InstrumentalMarker.MaxWidth(style)));
        }
    }
}
