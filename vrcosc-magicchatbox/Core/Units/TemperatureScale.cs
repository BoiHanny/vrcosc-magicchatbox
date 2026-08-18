using System.ComponentModel;

namespace vrcosc_magicchatbox.Core.Units;

public enum TemperatureScale
{
    [Description("Celsius (°C)")]
    Celsius,

    [Description("Fahrenheit (°F)")]
    Fahrenheit,

    [Description("Kelvin (K)")]
    Kelvin,

    [Description("Rankine (°R)")]
    Rankine,

    [Description("Réaumur (°Ré)")]
    Reaumur,
}

public enum TemperatureCompanion
{
    [Description("Nothing, just the one scale")]
    None,

    [Description("Celsius (°C)")]
    Celsius,

    [Description("Fahrenheit (°F)")]
    Fahrenheit,

    [Description("Kelvin (K)")]
    Kelvin,

    [Description("Rankine (°R)")]
    Rankine,

    [Description("Réaumur (°Ré)")]
    Reaumur,
}

public static class Temperatures
{
    public static readonly TemperatureScale[] All =
    {
        TemperatureScale.Celsius,
        TemperatureScale.Fahrenheit,
        TemperatureScale.Kelvin,
        TemperatureScale.Rankine,
        TemperatureScale.Reaumur,
    };

    public static double FromCelsius(double celsius, TemperatureScale scale)
        => scale switch
        {
            TemperatureScale.Fahrenheit => celsius * 9.0 / 5.0 + 32,
            TemperatureScale.Kelvin => celsius + 273.15,
            TemperatureScale.Rankine => (celsius + 273.15) * 9.0 / 5.0,
            TemperatureScale.Reaumur => celsius * 4.0 / 5.0,
            _ => celsius,
        };

    public static string Symbol(TemperatureScale scale, bool degreeSign)
    {
        if (scale == TemperatureScale.Kelvin)
            return "K";

        string letters = scale switch
        {
            TemperatureScale.Fahrenheit => "F",
            TemperatureScale.Rankine => "R",
            TemperatureScale.Reaumur => "Ré",
            _ => "C",
        };

        return degreeSign ? "°" + letters : letters;
    }

    public static bool TryCompanion(TemperatureCompanion companion, TemperatureScale shown, out TemperatureScale scale)
    {
        scale = companion switch
        {
            TemperatureCompanion.Fahrenheit => TemperatureScale.Fahrenheit,
            TemperatureCompanion.Kelvin => TemperatureScale.Kelvin,
            TemperatureCompanion.Rankine => TemperatureScale.Rankine,
            TemperatureCompanion.Reaumur => TemperatureScale.Reaumur,
            _ => TemperatureScale.Celsius,
        };

        return companion != TemperatureCompanion.None && scale != shown;
    }
}
