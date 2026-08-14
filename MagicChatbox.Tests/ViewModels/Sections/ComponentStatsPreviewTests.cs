using System.Globalization;
using vrcosc_magicchatbox.ViewModels.Sections;
using Xunit;

namespace MagicChatbox.Tests.ViewModels.Sections;

/// <summary>
/// The preview is the only place the component readout explains itself, so what it claims has to be
/// what the writer does - including the dependencies between switches that no label states.
/// </summary>
public sealed class ComponentStatsPreviewTests
{
    private static StatPreviewShape Plain(string name) => new(name, name, false, false, false, false);

    private static string F(double value) => value.ToString("F1", CultureInfo.CurrentCulture);

    private static ComponentStatsPreviewOptions Options(
        StatPreviewShape? gpu = null,
        bool useEmojis = false,
        bool showTemperature = false,
        bool showWattage = false,
        bool showDdr = false,
        bool fahrenheit = false,
        string separator = " ¦ ")
        => new()
        {
            Separator = separator,
            UseEmojis = useEmojis,
            Fahrenheit = fahrenheit,
            ShowGpuTemperature = showTemperature,
            ShowGpuWattage = showWattage,
            ShowDdrVersion = showDdr,
            Cpu = Plain("CPU"),
            Gpu = gpu ?? Plain("GPU"),
            Vram = Plain("VRAM"),
            Ram = Plain("RAM"),
        };

    [Fact]
    public void EveryComponentAppearsInTheOrderTheChatboxWouldSeeThem()
    {
        string line = ComponentStatsPreview.Render(Options());

        Assert.True(line.IndexOf("CPU") < line.IndexOf("GPU"));
        Assert.True(line.IndexOf("GPU") < line.IndexOf("VRAM"));
        // "VRAM:" contains "RAM:", so the standalone one is the last occurrence.
        Assert.True(line.IndexOf("VRAM:") < line.LastIndexOf("RAM:"));
    }

    [Fact]
    public void TheNumberStaysFullSizeAndOnlyTheNameIsRaisedWhenAsked()
    {
        var raised = new StatPreviewShape("CPU", "CPU", RaiseName: true, false, false, false);

        string line = ComponentStatsPreview.Render(Options() with { Cpu = raised });

        Assert.Contains("ᶜᵖᵘ 23.4", line);
        Assert.DoesNotContain("²³", line);
    }

    [Fact]
    public void TheFullSizeNameKeepsTheColonThatStandsInForTheRaise()
    {
        Assert.Contains("CPU: 23.4", ComponentStatsPreview.Render(Options()));
    }

    [Fact]
    public void RoundingDropsTheFractionRatherThanRewritingTheNumber()
    {
        var rounded = new StatPreviewShape("CPU", "CPU", false, false, RoundNumbers: true, false);

        string line = ComponentStatsPreview.Render(Options() with { Cpu = rounded });

        Assert.Contains("CPU: 23", line);
        Assert.DoesNotContain("23.4", line);
    }

    [Fact]
    public void TheCapacityOnlyAppearsWhenTheComponentIsSetToShowIt()
    {
        var withMax = new StatPreviewShape("RAM", "RAM", false, false, false, ShowMax: true);

        Assert.DoesNotContain("18.3/32.0", ComponentStatsPreview.Render(Options()));
        Assert.Contains("18.3/32.0", ComponentStatsPreview.Render(Options() with { Ram = withMax }));
    }

    [Fact]
    public void TheHardwareNameReplacesTheShortOneAndCostsMoreOfTheLine()
    {
        var named = new StatPreviewShape("GPU", "GeForce RTX 4080", false, UseHardwareName: true, false, false);

        string shortLine = ComponentStatsPreview.Render(Options());
        string longLine = ComponentStatsPreview.Render(Options(named));

        Assert.Contains("GeForce RTX 4080", longLine);
        Assert.True(longLine.Length > shortLine.Length);
    }

    [Fact]
    public void RaisedLabelsStopReachingTheTemperatureOnceIconsAreOn()
    {
        // This is the dependency the preview exists to make visible: an icon is placed as-is, so the
        // GPU's raised-label switch has nothing left to act on.
        var raised = new StatPreviewShape("GPU", "GPU", RaiseName: true, false, false, false);

        string words = ComponentStatsPreview.Render(Options(raised, showTemperature: true));
        string icons = ComponentStatsPreview.Render(Options(raised, useEmojis: true, showTemperature: true));

        Assert.Contains("ᵗᵉᵐᵖ", words);
        Assert.DoesNotContain("ᵗᵉᵐᵖ", icons);
        Assert.Contains("♨️", icons);
    }

    [Fact]
    public void TheTemperatureIsConvertedRatherThanRelabelled()
    {
        // The unit is raised because it is a unit; the reading beside it is not. The number is
        // written in the user's own culture, exactly as the module writes it, so it is expected
        // the same way rather than hard-coded to one decimal separator.
        Assert.Contains(F(64.0) + "°ᶜ", ComponentStatsPreview.Render(Options(showTemperature: true)));
        Assert.Contains(F(147.2) + "°ᶠ", ComponentStatsPreview.Render(Options(showTemperature: true, fahrenheit: true)));
    }

    [Fact]
    public void TheMemoryGenerationOnlyRidesAlongWhenAsked()
    {
        Assert.DoesNotContain("ᴰᴰᴿ", ComponentStatsPreview.Render(Options()));
        Assert.Contains("⁽ᴰᴰᴿ⁵⁾", ComponentStatsPreview.Render(Options(showDdr: true)));
    }

    [Fact]
    public void ThePreviewUsesTheSeparatorTheUserTyped()
    {
        Assert.Contains(" / ", ComponentStatsPreview.Render(Options(separator: " / ")));
    }

    [Fact]
    public void AnOverlongSeparatorIsClampedTheSameWayTheWriterClampsIt()
    {
        // Otherwise the preview would promise a divider the chatbox is never going to print.
        string line = ComponentStatsPreview.Render(Options(separator: "----------"));

        Assert.Contains("--------", line);
        Assert.DoesNotContain("---------", line);
    }

    [Fact]
    public void PowerIsReportedWithItsUnitGluedToTheReading()
    {
        Assert.Contains("power 213.0ʷ", ComponentStatsPreview.Render(Options(showWattage: true)));
    }

    [Fact]
    public void NoOptionsAtAllIsStillAWellFormedLineRatherThanACrash()
    {
        Assert.Equal(string.Empty, ComponentStatsPreview.Render(null!));
    }
}
