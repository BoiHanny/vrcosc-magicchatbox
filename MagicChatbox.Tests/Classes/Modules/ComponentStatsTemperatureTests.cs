using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Units;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// The global temperature setting the whole app reads: which scales are in play, and which one is
/// on screen right now. Weather borrows the answer from here whenever it has no override of its own.
/// </summary>
public sealed class ComponentStatsTemperatureTests
{
    private static ComponentStatsSettings Only(params TemperatureScale[] scales)
    {
        var settings = new ComponentStatsSettings
        {
            TemperatureCelsius = false,
            TemperatureFahrenheit = false,
            TemperatureKelvin = false,
            TemperatureRankine = false,
            TemperatureReaumur = false,
        };

        foreach (var scale in scales)
        {
            switch (scale)
            {
                case TemperatureScale.Celsius: settings.TemperatureCelsius = true; break;
                case TemperatureScale.Fahrenheit: settings.TemperatureFahrenheit = true; break;
                case TemperatureScale.Kelvin: settings.TemperatureKelvin = true; break;
                case TemperatureScale.Rankine: settings.TemperatureRankine = true; break;
                case TemperatureScale.Reaumur: settings.TemperatureReaumur = true; break;
            }
        }

        return settings;
    }

    [Fact]
    public void OutOfTheBoxItStillSwapsBetweenCelsiusAndFahrenheitAndNothingElse()
    {
        var settings = new ComponentStatsSettings();

        Assert.Equal(new[] { TemperatureScale.Celsius, TemperatureScale.Fahrenheit }, settings.EnabledTemperatureScales);
        Assert.True(settings.TemperatureRotates);
    }

    [Fact]
    public void TheScalesNobodyAskedForStayOffUntilSomebodyDoes()
    {
        var settings = new ComponentStatsSettings();

        Assert.False(settings.TemperatureKelvin);
        Assert.False(settings.TemperatureRankine);
        Assert.False(settings.TemperatureReaumur);
        Assert.Equal(TemperatureCompanion.None, settings.TemperatureCompanionScale);
    }

    [Fact]
    public void TickingASingleScaleParksTheReadoutOnItForGood()
    {
        var settings = Only(TemperatureScale.Kelvin);

        Assert.False(settings.TemperatureRotates);
        Assert.Equal(TemperatureScale.Kelvin, settings.TemperatureScaleAt(0));
        Assert.Equal(TemperatureScale.Kelvin, settings.TemperatureScaleAt(37));
    }

    [Fact]
    public void UntickingEveryScaleFallsBackToCelsiusRatherThanToNothing()
    {
        var settings = Only();

        Assert.Equal(new[] { TemperatureScale.Celsius }, settings.EnabledTemperatureScales);
        Assert.Equal(TemperatureScale.Celsius, settings.TemperatureScaleAt(0));
    }

    [Fact]
    public void TheRotationMovesOnOncePerIntervalAndComesBackAround()
    {
        var settings = Only(TemperatureScale.Celsius, TemperatureScale.Fahrenheit, TemperatureScale.Kelvin);
        settings.TemperatureDisplaySwitchInterval = 5;

        Assert.Equal(TemperatureScale.Celsius, settings.TemperatureScaleAt(0));
        Assert.Equal(TemperatureScale.Celsius, settings.TemperatureScaleAt(4));
        Assert.Equal(TemperatureScale.Fahrenheit, settings.TemperatureScaleAt(5));
        Assert.Equal(TemperatureScale.Kelvin, settings.TemperatureScaleAt(10));
        Assert.Equal(TemperatureScale.Celsius, settings.TemperatureScaleAt(15));
    }

    [Fact]
    public void AnIntervalOfZeroKeepsMovingInsteadOfDividingByIt()
    {
        var settings = Only(TemperatureScale.Celsius, TemperatureScale.Fahrenheit);
        settings.TemperatureDisplaySwitchInterval = 0;

        Assert.Equal(TemperatureScale.Celsius, settings.TemperatureScaleAt(0));
        Assert.Equal(TemperatureScale.Fahrenheit, settings.TemperatureScaleAt(1));
    }

    [Fact]
    public void TheScalesAlwaysComeOutInOneOrderHoweverTheyWereTicked()
    {
        var settings = Only(TemperatureScale.Reaumur, TemperatureScale.Celsius, TemperatureScale.Kelvin);

        Assert.Equal(
            new[] { TemperatureScale.Celsius, TemperatureScale.Kelvin, TemperatureScale.Reaumur },
            settings.EnabledTemperatureScales);
    }

    [Fact]
    public void ASettingsFileThatWasSwappingCarriesOnSwapping()
    {
        var settings = Load("{\"_schemaVersion\":1,\"IsFahrenheit\":false,\"IsTemperatureSwitchEnabled\":true}");

        Assert.Equal(new[] { TemperatureScale.Celsius, TemperatureScale.Fahrenheit }, settings.EnabledTemperatureScales);
    }

    [Fact]
    public void ASettingsFileParkedOnFahrenheitKeepsFahrenheitAndOnlyFahrenheit()
    {
        var settings = Load("{\"_schemaVersion\":1,\"IsFahrenheit\":true,\"IsTemperatureSwitchEnabled\":false}");

        Assert.Equal(new[] { TemperatureScale.Fahrenheit }, settings.EnabledTemperatureScales);
    }

    [Fact]
    public void ASettingsFileParkedOnCelsiusKeepsCelsiusAndOnlyCelsius()
    {
        var settings = Load("{\"_schemaVersion\":1,\"IsFahrenheit\":false,\"IsTemperatureSwitchEnabled\":false}");

        Assert.Equal(new[] { TemperatureScale.Celsius }, settings.EnabledTemperatureScales);
    }

    [Fact]
    public void AFileThatAlreadyKnowsAboutScalesIsLeftAlone()
    {
        // Without the schema guard the dead legacy pair would re-tick Celsius on every single load,
        // and a deliberate choice would never survive a restart.
        var settings = Load(
            "{\"_schemaVersion\":2,\"IsFahrenheit\":false,\"IsTemperatureSwitchEnabled\":true," +
            "\"TemperatureCelsius\":false,\"TemperatureFahrenheit\":false,\"TemperatureKelvin\":true}");

        Assert.Equal(new[] { TemperatureScale.Kelvin }, settings.EnabledTemperatureScales);
    }

    [Fact]
    public void TheOldXmlRouteGetsTheSameReadingOfTheLegacyChoice()
    {
        // That route fills the properties in by hand rather than by deserialising, and it stamps the
        // schema forward on its way out - so if it does not adopt the choice here, nothing ever will.
        var settings = new ComponentStatsSettings { IsFahrenheit = true, IsTemperatureSwitchEnabled = false };

        Assert.True(settings.AdoptLegacySettings());
        Assert.Equal(new[] { TemperatureScale.Fahrenheit }, settings.EnabledTemperatureScales);
    }

    [Fact]
    public void AdoptingTheLegacyChoiceASecondTimeChangesNothing()
    {
        var settings = new ComponentStatsSettings { IsFahrenheit = true, IsTemperatureSwitchEnabled = false };
        settings.AdoptLegacySettings();

        settings.TemperatureKelvin = true;

        Assert.False(settings.AdoptLegacySettings());
        Assert.Equal(new[] { TemperatureScale.Fahrenheit, TemperatureScale.Kelvin }, settings.EnabledTemperatureScales);
    }

    private static ComponentStatsSettings Load(string json)
        => JsonConvert.DeserializeObject<ComponentStatsSettings>(json)!;
}
