using System;
using System.Globalization;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using Xunit;

namespace MagicChatbox.Tests.Services;

public class TimeFormattingDaylightTests
{
    private sealed class StubSettingsProvider<T> : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = new T();
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    // Both instants are UTC noon, one either side of the European daylight saving period.
    private static readonly DateTimeOffset Winter = new(2026, 12, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Summer = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static (TimeFormattingService Service, TimeSettings Settings) Build(Timezone zone = Timezone.CET)
    {
        var provider = new StubSettingsProvider<TimeSettings>();
        provider.Value.Time24H = true;
        provider.Value.TimeShowTimeZone = true;
        provider.Value.SelectedTimeZone = zone;
        return (new TimeFormattingService(provider), provider.Value);
    }

    [Fact]
    public void WinterKeepsStandardTime()
    {
        var (service, _) = Build();

        Assert.Equal("13:00 (CET+1)", service.GetFormattedTime(Winter));
    }

    [Fact]
    public void SummerMovesToDaylightTime()
    {
        var (service, _) = Build();

        Assert.Equal("14:00 (CEST+2)", service.GetFormattedTime(Summer));
    }

    [Fact]
    public void DaylightOffIsStandardTimeAllYear()
    {
        var (service, settings) = Build();
        settings.UseDaylightSavingTime = false;

        Assert.Equal("13:00 (CET+1)", service.GetFormattedTime(Summer));
        Assert.Equal("13:00 (CET+1)", service.GetFormattedTime(Winter));
    }

    [Fact]
    public void ZoneWithoutDaylightRulesNeverShifts()
    {
        var (service, _) = Build(Timezone.IST);

        Assert.Equal("17:30 (IST+5:30)", service.GetFormattedTime(Winter));
        Assert.Equal("17:30 (IST+5:30)", service.GetFormattedTime(Summer));
    }

    [Fact]
    public void SouthernHemisphereDaylightRunsOverWinterHere()
    {
        var (service, _) = Build(Timezone.NZST);

        Assert.Equal("01:00 (NZDT+13)", service.GetFormattedTime(Winter));
        Assert.Equal("00:00 (NZST+12)", service.GetFormattedTime(Summer));
    }

    // The selected zone must not leak into the display until the custom zone is turned on.
    [Fact]
    public void WithoutACustomZoneTheMachineClockIsReported()
    {
        var (service, settings) = Build(Timezone.JST);
        settings.TimeShowTimeZone = false;

        Assert.Equal(
            Winter.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
            service.GetFormattedTime(Winter));
    }

    [Fact]
    public void TwelveHourFormatKeepsTheZoneSuffix()
    {
        var (service, settings) = Build();
        settings.Time24H = false;

        Assert.Equal("02:00 PM (CEST+2)", service.GetFormattedTime(Summer));
    }
}
