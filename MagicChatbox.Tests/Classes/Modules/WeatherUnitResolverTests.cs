using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Units;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class WeatherUnitResolverTests
{
    public static IEnumerable<object[]> EveryOverrideAgainstEveryGlobalScale()
    {
        foreach (WeatherUnitOverride unitOverride in new[]
                 {
                     WeatherUnitOverride.UseGlobal,
                     WeatherUnitOverride.Celsius,
                     WeatherUnitOverride.Fahrenheit,
                     WeatherUnitOverride.Kelvin,
                     WeatherUnitOverride.Rankine,
                     WeatherUnitOverride.Reaumur,
                 })
            foreach (TemperatureScale globalScale in Temperatures.All)
                yield return new object[] { unitOverride, globalScale };
    }

    [Theory]
    [MemberData(nameof(EveryOverrideAgainstEveryGlobalScale))]
    public void Wind_on_use_global_always_agrees_with_the_temperature_unit(WeatherUnitOverride unitOverride, TemperatureScale globalScale)
    {
        // The global scale can flip on a clock, so the wind unit is derived from the scale already
        // chosen for this line instead of being resolved a second time.
        TemperatureScale temperature = WeatherUnitResolver.Temperature(unitOverride, globalScale);
        string wind = WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, temperature);

        bool imperial = temperature is TemperatureScale.Fahrenheit or TemperatureScale.Rankine;
        Assert.Equal(imperial ? "mph" : "km/h", wind);
    }

    [Fact]
    public void A_stale_temperature_unit_cannot_leak_into_the_wind_unit()
    {
        // Guards the shape of the call: Wind takes the scale it must match, it does not look the
        // global scale up for itself.
        Assert.Equal("mph", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, TemperatureScale.Fahrenheit));
        Assert.Equal("km/h", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, TemperatureScale.Celsius));
    }

    [Fact]
    public void The_absolute_scales_take_the_wind_unit_of_the_country_that_uses_them()
    {
        // Rankine is Fahrenheit measured from absolute zero, so it belongs to miles per hour.
        // Kelvin is metric, so it does not.
        Assert.Equal("mph", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, TemperatureScale.Rankine));
        Assert.Equal("km/h", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, TemperatureScale.Kelvin));
    }

    [Theory]
    [InlineData(WeatherWindUnitOverride.KilometersPerHour, TemperatureScale.Fahrenheit, "km/h")]
    [InlineData(WeatherWindUnitOverride.MilesPerHour, TemperatureScale.Celsius, "mph")]
    public void An_explicit_wind_override_ignores_the_temperature_unit(WeatherWindUnitOverride windOverride, TemperatureScale temperatureScale, string expected)
    {
        Assert.Equal(expected, WeatherUnitResolver.Wind(windOverride, temperatureScale));
    }

    [Theory]
    [InlineData(WeatherUnitOverride.Celsius, TemperatureScale.Fahrenheit, TemperatureScale.Celsius)]
    [InlineData(WeatherUnitOverride.Fahrenheit, TemperatureScale.Celsius, TemperatureScale.Fahrenheit)]
    [InlineData(WeatherUnitOverride.Kelvin, TemperatureScale.Celsius, TemperatureScale.Kelvin)]
    [InlineData(WeatherUnitOverride.Rankine, TemperatureScale.Celsius, TemperatureScale.Rankine)]
    [InlineData(WeatherUnitOverride.Reaumur, TemperatureScale.Celsius, TemperatureScale.Reaumur)]
    [InlineData(WeatherUnitOverride.UseGlobal, TemperatureScale.Fahrenheit, TemperatureScale.Fahrenheit)]
    [InlineData(WeatherUnitOverride.UseGlobal, TemperatureScale.Kelvin, TemperatureScale.Kelvin)]
    public void An_explicit_temperature_override_wins_over_the_global_scale(WeatherUnitOverride unitOverride, TemperatureScale globalScale, TemperatureScale expected)
    {
        Assert.Equal(expected, WeatherUnitResolver.Temperature(unitOverride, globalScale));
    }
}
