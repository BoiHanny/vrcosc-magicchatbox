using System.Linq;
using System.Text.RegularExpressions;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The TikTok templates are edited between streams, which is exactly when there is no live data to
/// render them against. These pin the stand-in table and the way the two halves are joined.
/// </summary>
public sealed class TikTokSamplePreviewTests
{
    private static TikTokLiveSettings Settings() => new();

    [Fact]
    public void Every_token_the_stock_templates_use_has_a_stand_in_behind_it()
    {
        var settings = Settings();
        var tokens = TikTokLiveSectionViewModel.BuildSampleTokens(settings);

        string[] templates =
        [
            settings.ProfileTemplate,
            settings.ProfileFollowerChangeTemplate,
            settings.SummaryTemplate,
            settings.FollowTemplate,
            settings.CommentTemplate,
            settings.GiftTemplate,
            settings.LikeTemplate,
            settings.ViewerMilestoneTemplate
        ];

        var missing = templates
            .SelectMany(t => Regex.Matches(t, @"\{(?<name>[a-z_]+)\}").Select(m => m.Groups["name"].Value))
            .Distinct()
            .Where(name => !tokens.ContainsKey(name))
            .ToList();

        Assert.True(missing.Count == 0, "no sample value for: " + string.Join(", ", missing));
    }

    [Fact]
    public void A_rendered_template_leaves_no_placeholder_behind()
    {
        var settings = Settings();
        string line = TikTokLiveOutput.Render(settings.ProfileTemplate, TikTokLiveSectionViewModel.BuildSampleTokens(settings));

        Assert.DoesNotContain("{", line);
        Assert.Contains(TikTokLiveSectionViewModel.SampleProfileName, line);
    }

    [Fact]
    public void Shortening_the_counts_changes_what_the_preview_shows()
    {
        var plain = Settings();
        plain.CompactViewerCount = false;

        var compact = Settings();
        compact.CompactViewerCount = true;

        // The checkbox beside this promises 12300 -> 12.3ᵏ: the number full size, the unit raised.
        Assert.Equal("12300", TikTokLiveSectionViewModel.BuildSampleTokens(plain)["followers"]);
        Assert.Equal("12.3ᵏ", TikTokLiveSectionViewModel.BuildSampleTokens(compact)["followers"]);
    }

    [Fact]
    public void The_users_own_name_replaces_the_stand_in_once_they_type_one()
    {
        var settings = Settings();
        settings.ProfileUserName = "@realname";

        Assert.Equal("realname", TikTokLiveSectionViewModel.BuildSampleTokens(settings)["profile"]);
    }

    [Fact]
    public void The_live_host_falls_back_to_the_profile_name()
    {
        var settings = Settings();
        settings.ProfileUserName = "someone";

        Assert.Equal("someone", TikTokLiveSectionViewModel.BuildSampleTokens(settings)["host"]);
    }

    [Fact]
    public void The_order_setting_decides_which_half_comes_first()
    {
        var settings = Settings();
        settings.CombinedOutputSeparator = " | ";

        settings.OutputOrder = TikTokOutputOrder.ProfileThenLive;
        Assert.Equal("profile | live", TikTokLiveSectionViewModel.CombineSample(settings, "profile", "live"));

        settings.OutputOrder = TikTokOutputOrder.LiveThenProfile;
        Assert.Equal("live | profile", TikTokLiveSectionViewModel.CombineSample(settings, "profile", "live"));
    }

    [Fact]
    public void An_empty_half_never_leaves_the_separator_stranded()
    {
        var settings = Settings();
        settings.CombinedOutputSeparator = " | ";

        Assert.Equal("profile", TikTokLiveSectionViewModel.CombineSample(settings, "profile", string.Empty));
        Assert.Equal("live", TikTokLiveSectionViewModel.CombineSample(settings, "   ", "live"));
    }

    [Fact]
    public void A_separator_of_backslash_n_really_breaks_the_line()
    {
        var settings = Settings();
        settings.CombinedOutputSeparator = "\\n";

        Assert.Equal("profile\nlive", TikTokLiveSectionViewModel.CombineSample(settings, "profile", "live"));
    }
}
