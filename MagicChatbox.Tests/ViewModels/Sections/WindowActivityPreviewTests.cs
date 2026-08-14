using System;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

public sealed class WindowActivityPreviewTests
{
    private const string App = "'Firefox'";

    [Fact]
    public void TheHeadingAloneIsWhatShowsWhenTheAppIsNotNamed()
    {
        Assert.Equal("On desktop", WindowActivityPreview.Render("On desktop", "ⁱⁿ", App, nameTheApp: false));
    }

    [Fact]
    public void NamingTheAppBringsTheJoiningWordWithIt()
    {
        Assert.Equal("On desktop ⁱⁿ 'Firefox'", WindowActivityPreview.Render("On desktop", "ⁱⁿ", App, nameTheApp: true));
    }

    [Fact]
    public void AnEmptyJoiningWordDoesNotLeaveADoubleSpace()
    {
        // The two branches used to bracket this differently and one of them printed a stray space.
        Assert.Equal("On desktop 'Firefox'", WindowActivityPreview.Render("On desktop", "", App, nameTheApp: true));
    }

    [Fact]
    public void AnEmptyHeadingStillProducesAUsableLine()
    {
        Assert.Equal("'Firefox'", WindowActivityPreview.Render("", "", App, nameTheApp: true));
    }

    [Fact]
    public void TheTitleIsCutToTheLengthTheUserSet()
    {
        string cut = WindowActivityPreview.Title(limitOn: true, configured: 12);

        Assert.True(cut.Length <= 12);
        Assert.NotEqual(WindowActivityPreview.SampleTitle, cut);
    }

    [Fact]
    public void TurningTheLimitOffLeavesTheWholeTitle()
    {
        Assert.Equal(WindowActivityPreview.SampleTitle, WindowActivityPreview.Title(limitOn: false, configured: 12));
    }

    [Fact]
    public void EveryTitleRuleModeReadsAsEnglishRatherThanAnIdentifier()
    {
        // The dropdown renders these descriptions. A mode whose description is its own C# name is a
        // missing attribute, and the user is shown "Exclude" with no idea what it excludes.
        foreach (FilterMode mode in TitleFilterRule.FilterModes)
        {
            string description = mode.GetDescription();

            Assert.NotEqual(mode.ToString(), description);
            Assert.Contains(' ', description);
        }
    }

    [Fact]
    public void TheTitleRuleModesAreDescribedWithoutRepeatingThemselves()
    {
        var descriptions = TitleFilterRule.FilterModes.Select(m => m.GetDescription()).ToList();

        Assert.Equal(descriptions.Count, descriptions.Distinct(StringComparer.Ordinal).Count());
    }
}
