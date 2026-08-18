using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Utilities;
using vrcosc_magicchatbox.Core.Osc.Text;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class TikTokLiveOutputTests
{
    private const int Line = 144;

    /// <summary>The username regex accepts at most this many characters.</summary>
    private const int UserNameLength = 24;

    #region What the segment can cost

    [Fact]
    public void The_shipped_templates_could_overrun_the_line_on_their_own()
    {
        var settings = new TikTokLiveSettings();
        string profile = MaxProfileSummary(settings);
        string live = MaxCommentEvent(settings);
        string combined = string.Join(settings.CombinedOutputSeparator, profile, live);

        // 54 for the profile summary, 89 for a comment event, 3 for the separator between them.
        // TikTok is the highest Priority number in the app, so the builder answered this by throwing
        // the whole segment away - the readout blinked out every time a comment arrived.
        Assert.Equal(54, profile.Length);
        Assert.Equal(89, live.Length);
        Assert.Equal(146, combined.Length);

        Assert.True(combined.Length > Line);
    }

    [Fact]
    public void That_same_worst_case_now_fits_and_keeps_the_half_that_changes()
    {
        var settings = new TikTokLiveSettings();
        string profile = MaxProfileSummary(settings);
        string live = MaxCommentEvent(settings);
        string combined = string.Join(settings.CombinedOutputSeparator, profile, live);

        string fitted = TikTokLiveOutput.Fit(Line, combined, profile, live);

        Assert.Equal(live, fitted);
        Assert.True(fitted.Length <= Line);
    }

    [Fact]
    public void A_gift_name_is_the_one_token_that_arrived_with_no_length_attached()
    {
        var settings = new TikTokLiveSettings();
        string huge = new string('g', 300);

        string uncapped = TikTokLiveOutput.Render(
            settings.GiftTemplate,
            Tokens(("user", Repeat('u', UserNameLength)), ("gift", huge), ("count", "1")));

        string capped = TikTokLiveOutput.Render(
            settings.GiftTemplate,
            Tokens(
                ("user", Repeat('u', UserNameLength)),
                ("gift", SegmentWriter.Truncate(huge, TikTokLiveOutput.GiftNameLength)),
                ("count", "1")));

        Assert.True(uncapped.Length > Line, $"the uncapped name only reached {uncapped.Length}");
        Assert.True(capped.Length <= Line, $"the capped name still reached {capped.Length}");
    }

    #endregion

    #region Fitting

    [Fact]
    public void Both_halves_are_kept_when_they_fit()
        => Assert.Equal("profile | live", TikTokLiveOutput.Fit(Line, "profile | live", "profile", "live"));

    [Fact]
    public void The_profile_half_is_what_is_left_when_there_is_no_live_half()
        => Assert.Equal("profile", TikTokLiveOutput.Fit(10, "profile", "profile", string.Empty));

    [Fact]
    public void A_half_too_long_on_its_own_is_cut_rather_than_dropped()
    {
        string live = new string('l', 200);

        string fitted = TikTokLiveOutput.Fit(Line, "profile | " + live, "profile", live);

        Assert.Equal(Line, fitted.Length);
        Assert.EndsWith(OscGlyphs.Ellipsis, fitted);
    }

    [Fact]
    public void Every_budget_is_honoured()
    {
        string profile = new string('p', 90);
        string live = new string('l', 90);
        string combined = profile + " | " + live;

        for (int budget = 0; budget <= Line; budget++)
            Assert.True(
                TikTokLiveOutput.Fit(budget, combined, profile, live).Length <= budget,
                $"budget {budget} was exceeded");
    }

    [Fact]
    public void No_room_left_means_no_segment()
        => Assert.Equal(string.Empty, TikTokLiveOutput.Fit(0, "profile | live", "profile", "live"));

    #endregion

    #region Counts

    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_500, "1.5K")]
    [InlineData(2_400_000, "2.4M")]
    [InlineData(3_000_000_000, "3B")]
    public void A_compact_count_reads_the_same_as_it_always_did(long value, string expected)
        => Assert.Equal(expected, TikTokLiveOutput.Count(value, compact: true));

    [Fact]
    public void The_chatbox_raises_the_compact_suffix_and_nothing_else()
    {
        string text = TikTokLiveOutput.ChatCount(1_500, compact: true);

        Assert.Equal("1.5" + TextUtilities.TransformToSuperscript("K"), text);
        Assert.StartsWith("1.5", text);
    }

    [Fact]
    public void The_number_a_viewer_is_reading_is_never_raised()
    {
        foreach (long value in new long[] { 0, 42, 999, 1_500, 2_400_000, 3_000_000_000 })
        {
            foreach (bool compact in new[] { true, false })
            {
                string text = TikTokLiveOutput.ChatCount(value, compact);
                string digits = new string(text.Where(c => !char.IsLetter(c)).ToArray());

                Assert.All(digits, c => Assert.False(IsRaised(c), $"{value} came back with a raised '{c}'"));
            }
        }
    }

    [Fact]
    public void A_full_count_has_no_unit_to_raise()
        => Assert.Equal("1500", TikTokLiveOutput.ChatCount(1_500, compact: false));

    [Fact]
    public void Raising_the_suffix_costs_exactly_what_it_did_before()
    {
        // Raised text is a hierarchy change, not a saving. Anything else here would be a miscount.
        Assert.Equal(
            TikTokLiveOutput.Count(1_500, compact: true).Length,
            TikTokLiveOutput.ChatCount(1_500, compact: true).Length);
    }

    #endregion

    #region Templates

    [Fact]
    public void An_empty_token_does_not_strand_a_double_space()
        => Assert.Equal("a b", TikTokLiveOutput.Render("a {gone} b", Tokens(("gone", string.Empty))));

    [Fact]
    public void A_token_is_matched_whatever_case_it_is_written_in()
        => Assert.Equal("hi", TikTokLiveOutput.Render("{USER}", Tokens(("user", "hi"))));

    [Fact]
    public void An_escaped_newline_becomes_a_real_one()
        => Assert.Equal("a\nb", TikTokLiveOutput.Render("a\\nb", Tokens()));

    #endregion

    private static string MaxProfileSummary(TikTokLiveSettings settings)
        => TikTokLiveOutput.Render(
            settings.ProfileTemplate,
            Tokens(("profile", Repeat('n', UserNameLength)), ("followers", "999999999")));

    private static string MaxCommentEvent(TikTokLiveSettings settings)
        => TikTokLiveOutput.Render(
            settings.CommentTemplate,
            Tokens(
                ("user", Repeat('u', TikTokLiveOutput.UserPreviewLength)),
                ("message", Repeat('m', TikTokLiveOutput.CommentPreviewLength))));

    private static string Repeat(char c, int count) => new(c, count);

    private static IReadOnlyDictionary<string, string> Tokens(params (string Key, string Value)[] pairs)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
            tokens[key] = value;

        return tokens;
    }

    private static bool IsRaised(char c)
        => "abcdefghijklmnopqrstuvwxyz0123456789".Any(plain =>
            SuperscriptText.TryMap(plain, out char raised) && raised == c);
}
