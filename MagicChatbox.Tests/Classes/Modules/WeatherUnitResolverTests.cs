using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public class WeatherUnitResolverTests
{
    public static IEnumerable<object[]> EveryOverrideAgainstEveryGlobalUnit()
    {
        foreach (var unitOverride in new[] { WeatherUnitOverride.UseGlobal, WeatherUnitOverride.Celsius, WeatherUnitOverride.Fahrenheit })
            foreach (string globalUnit in new[] { "C", "F" })
                yield return new object[] { unitOverride, globalUnit };
    }

    [Theory]
    [MemberData(nameof(EveryOverrideAgainstEveryGlobalUnit))]
    public void Wind_on_use_global_always_agrees_with_the_temperature_unit(WeatherUnitOverride unitOverride, string globalUnit)
    {
        // The global unit can flip on a clock, so the wind unit is derived from the temperature
        // unit already chosen for this line instead of being resolved a second time.
        string temperature = WeatherUnitResolver.Temperature(unitOverride, globalUnit);
        string wind = WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, temperature);

        Assert.Equal(temperature == "F" ? "mph" : "km/h", wind);
    }

    [Fact]
    public void A_stale_temperature_unit_cannot_leak_into_the_wind_unit()
    {
        // Guards the shape of the call: Wind takes the unit it must match, it does not look the
        // global unit up for itself.
        Assert.Equal("mph", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, "F"));
        Assert.Equal("km/h", WeatherUnitResolver.Wind(WeatherWindUnitOverride.UseGlobal, "C"));
    }

    [Theory]
    [InlineData(WeatherWindUnitOverride.KilometersPerHour, "F", "km/h")]
    [InlineData(WeatherWindUnitOverride.MilesPerHour, "C", "mph")]
    public void An_explicit_wind_override_ignores_the_temperature_unit(WeatherWindUnitOverride windOverride, string temperatureUnit, string expected)
    {
        Assert.Equal(expected, WeatherUnitResolver.Wind(windOverride, temperatureUnit));
    }

    [Theory]
    [InlineData(WeatherUnitOverride.Celsius, "F", "C")]
    [InlineData(WeatherUnitOverride.Fahrenheit, "C", "F")]
    [InlineData(WeatherUnitOverride.UseGlobal, "F", "F")]
    [InlineData(WeatherUnitOverride.UseGlobal, "C", "C")]
    public void An_explicit_temperature_override_wins_over_the_global_unit(WeatherUnitOverride unitOverride, string globalUnit, string expected)
    {
        Assert.Equal(expected, WeatherUnitResolver.Temperature(unitOverride, globalUnit));
    }
}
