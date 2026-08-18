using vrcosc_magicchatbox.Core.Units;
using Xunit;

namespace MagicChatbox.Tests.Core.Units;

/// <summary>
/// Every reading in the app arrives in Celsius and leaves in whatever the user picked, so this is
/// the one place the arithmetic lives. It used to be copied into four of them.
/// </summary>
public sealed class TemperatureScaleTests
{
    [Theory]
    [InlineData(TemperatureScale.Celsius, 0.0)]
    [InlineData(TemperatureScale.Fahrenheit, 32.0)]
    [InlineData(TemperatureScale.Kelvin, 273.15)]
    [InlineData(TemperatureScale.Rankine, 491.67)]
    [InlineData(TemperatureScale.Reaumur, 0.0)]
    public void FreezingWaterLandsOnTheNumberEachScaleIsFamousFor(TemperatureScale scale, double expected)
    {
        Assert.Equal(expected, Temperatures.FromCelsius(0, scale), 2);
    }

    [Theory]
    [InlineData(TemperatureScale.Celsius, 100.0)]
    [InlineData(TemperatureScale.Fahrenheit, 212.0)]
    [InlineData(TemperatureScale.Kelvin, 373.15)]
    [InlineData(TemperatureScale.Rankine, 671.67)]
    [InlineData(TemperatureScale.Reaumur, 80.0)]
    public void BoilingWaterLandsOnTheOtherNumberEachScaleIsFamousFor(TemperatureScale scale, double expected)
    {
        Assert.Equal(expected, Temperatures.FromCelsius(100, scale), 2);
    }

    [Fact]
    public void BothAbsoluteScalesReachZeroAtTheSameRealTemperature()
    {
        // The whole point of Kelvin and Rankine: they start where there is nothing left to take away.
        Assert.Equal(0.0, Temperatures.FromCelsius(-273.15, TemperatureScale.Kelvin), 2);
        Assert.Equal(0.0, Temperatures.FromCelsius(-273.15, TemperatureScale.Rankine), 2);
    }

    [Fact]
    public void TheOneTemperatureCelsiusAndFahrenheitAgreeOn()
    {
        Assert.Equal(-40.0, Temperatures.FromCelsius(-40, TemperatureScale.Fahrenheit), 2);
    }

    [Theory]
    [InlineData(TemperatureScale.Celsius, "°C")]
    [InlineData(TemperatureScale.Fahrenheit, "°F")]
    [InlineData(TemperatureScale.Rankine, "°R")]
    [InlineData(TemperatureScale.Reaumur, "°Ré")]
    public void AScaleMadeOfDegreesIsWrittenWithTheDegreeSign(TemperatureScale scale, string expected)
    {
        Assert.Equal(expected, Temperatures.Symbol(scale, degreeSign: true));
    }

    [Fact]
    public void KelvinNeverGetsADegreeSignHoweverPolitelyItIsAsked()
    {
        // A kelvin is not a degree, it is an amount. "°K" has been wrong since 1967 and this is the
        // assertion that keeps it wrong somewhere else.
        Assert.Equal("K", Temperatures.Symbol(TemperatureScale.Kelvin, degreeSign: true));
        Assert.Equal("K", Temperatures.Symbol(TemperatureScale.Kelvin, degreeSign: false));
    }

    [Theory]
    [InlineData(TemperatureScale.Celsius, "C")]
    [InlineData(TemperatureScale.Fahrenheit, "F")]
    [InlineData(TemperatureScale.Rankine, "R")]
    [InlineData(TemperatureScale.Reaumur, "Ré")]
    public void TheBareSymbolDropsTheDegreeForPlacesThatAlreadyShrinkTheUnit(TemperatureScale scale, string expected)
    {
        Assert.Equal(expected, Temperatures.Symbol(scale, degreeSign: false));
    }

    [Fact]
    public void NoCompanionScaleMeansNothingInBrackets()
    {
        Assert.False(Temperatures.TryCompanion(TemperatureCompanion.None, TemperatureScale.Celsius, out _));
    }

    [Fact]
    public void AskingForTheScaleAlreadyOnScreenAddsNothing()
    {
        // Otherwise a rotation that reaches Celsius would print "64°C (64°C)" once every few seconds.
        Assert.False(Temperatures.TryCompanion(TemperatureCompanion.Celsius, TemperatureScale.Celsius, out _));
    }

    [Fact]
    public void ASecondScaleIsHandedBackWhenItDiffersFromTheFirst()
    {
        Assert.True(Temperatures.TryCompanion(TemperatureCompanion.Kelvin, TemperatureScale.Celsius, out var second));
        Assert.Equal(TemperatureScale.Kelvin, second);
    }

    [Theory]
    [InlineData(TemperatureCompanion.Celsius, TemperatureScale.Celsius)]
    [InlineData(TemperatureCompanion.Fahrenheit, TemperatureScale.Fahrenheit)]
    [InlineData(TemperatureCompanion.Kelvin, TemperatureScale.Kelvin)]
    [InlineData(TemperatureCompanion.Rankine, TemperatureScale.Rankine)]
    [InlineData(TemperatureCompanion.Reaumur, TemperatureScale.Reaumur)]
    public void EveryScaleCanBeChosenAsTheCompanionOne(TemperatureCompanion companion, TemperatureScale expected)
    {
        // Réaumur is on screen so that no case here is ever asked for the scale it already shows.
        Assert.True(Temperatures.TryCompanion(companion, TemperatureScale.Reaumur, out var second)
                    || expected == TemperatureScale.Reaumur);

        if (expected != TemperatureScale.Reaumur)
            Assert.Equal(expected, second);
    }
}
