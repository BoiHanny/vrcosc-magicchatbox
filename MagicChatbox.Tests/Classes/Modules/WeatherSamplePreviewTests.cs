using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Units;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// The sample line the Weather section previews with. The formatter is a pure function of the
/// settings plus a snapshot, so the preview needs no network, no location and no consent - which
/// is the whole reason a user can judge a template before the first sync ever happens.
/// </summary>
public sealed class WeatherSamplePreviewTests
{
    private static (WeatherService Service, WeatherSettings Settings) Build()
    {
        // The unit otherwise follows the global Component stats setting, which would make these
        // assertions depend on a second module's default.
        var settings = new WeatherSettings
        {
            ShowWeatherInTime = true,
            WeatherUnitOverride = WeatherUnitOverride.Celsius,
        };

        var service = new WeatherService(
            new StubHttpClientFactory(),
            new StubSettingsProvider<WeatherSettings>(settings),
            new StubSettingsProvider<TimeSettings>(new TimeSettings()),
            new IntegrationDisplayState(),
            new StubSettingsProvider<ComponentStatsSettings>(new ComponentStatsSettings()),
            new ImmediateDispatcher(),
            new FixedClock(),
            new DeniedConsent());

        return (service, settings);
    }

    [Fact]
    public void ThePreviewIsNotEmptyBeforeAnyWeatherHasBeenFetched()
    {
        var (service, _) = Build();

        Assert.NotEmpty(service.BuildSampleWeatherText());
    }

    [Fact]
    public void TheLivePathStaysEmptyUntilRealWeatherArrives()
    {
        var (service, _) = Build();

        // The sample must not leak into the chatbox - it exists for the settings page only.
        Assert.Equal(string.Empty, service.BuildWeatherOnlyText());
    }

    [Fact]
    public void EveryPlaceholderInAUserTemplateIsFilledIn()
    {
        var (service, settings) = Build();
        settings.ShowWeatherCondition = true;
        settings.ShowWeatherHumidity = true;
        settings.ShowWeatherWind = true;
        settings.ShowWeatherFeelsLike = true;
        settings.WeatherTemplate = "{time} {tempWithUnit} {condition} {humidity} {wind} {feels}";

        string line = service.BuildSampleWeatherText();

        Assert.DoesNotContain("{", line);
        Assert.Contains("13:37", line);
        Assert.Contains("21", line);
        Assert.Contains("63", line);
    }

    [Fact]
    public void SwitchingToFahrenheitVisiblyChangesTheReading()
    {
        var (service, settings) = Build();

        settings.WeatherUnitOverride = WeatherUnitOverride.Celsius;
        string celsius = service.BuildSampleWeatherText();

        settings.WeatherUnitOverride = WeatherUnitOverride.Fahrenheit;
        string fahrenheit = service.BuildSampleWeatherText();

        Assert.NotEqual(celsius, fahrenheit);
        Assert.Contains("21", celsius);
        Assert.Contains("71", fahrenheit);
    }

    [Fact]
    public void KelvinIsOnOfferAndArrivesWithoutADegreeSign()
    {
        var (service, settings) = Build();
        settings.WeatherUnitOverride = WeatherUnitOverride.Kelvin;

        string line = service.BuildSampleWeatherText();

        Assert.Contains("295ᵏ", line);
        Assert.DoesNotContain("°", line);
    }

    [Fact]
    public void ASecondScaleCanRideAlongInBracketsInsteadOfWaitingForTheSwap()
    {
        var (service, settings) = Build();
        settings.WeatherCompanionScale = TemperatureCompanion.Kelvin;

        Assert.Contains("21ᶜ ⁽²⁹⁵ᵏ⁾", service.BuildSampleWeatherText());
    }

    [Fact]
    public void TheUnitPlaceholderStaysOneUnitAndTheCompanionTravelsWithTheReading()
    {
        // A template that wants the two apart can have them apart; {unit} would be a poor name for
        // something that sometimes carries a whole second reading.
        var (service, settings) = Build();
        settings.WeatherCompanionScale = TemperatureCompanion.Kelvin;
        settings.WeatherTemplate = "{temp}|{unit}|{tempWithUnit}";

        Assert.Contains("21|ᶜ|21ᶜ ⁽²⁹⁵ᵏ⁾", service.BuildSampleWeatherText());
    }

    [Fact]
    public void TheDecimalBoxIsVisibleInThePreview()
    {
        var (service, settings) = Build();

        settings.WeatherUseDecimal = false;
        Assert.DoesNotContain("21.4", service.BuildSampleWeatherText());

        settings.WeatherUseDecimal = true;
        Assert.Contains("21.4", service.BuildSampleWeatherText());
    }

    [Fact]
    public void TurningTheExtrasOffShortensThePreview()
    {
        var (service, settings) = Build();
        settings.ShowWeatherHumidity = true;
        settings.ShowWeatherWind = true;
        settings.ShowWeatherFeelsLike = true;
        string everything = service.BuildSampleWeatherText();

        settings.ShowWeatherHumidity = false;
        settings.ShowWeatherWind = false;
        settings.ShowWeatherFeelsLike = false;
        string bare = service.BuildSampleWeatherText();

        Assert.True(bare.Length < everything.Length);
    }

    [Fact]
    public void TheTwoLineLayoutActuallyBreaksTheLine()
    {
        var (service, settings) = Build();
        settings.WeatherTemplate = string.Empty;
        settings.ShowWeatherCondition = true;
        settings.ShowWeatherHumidity = true;
        settings.WeatherLayoutMode = WeatherLayoutMode.TwoLines;

        Assert.Contains("\n", service.BuildSampleWeatherText());
    }

    [Fact]
    public void RenamingAConditionShowsUpInThePreview()
    {
        var (service, settings) = Build();
        settings.WeatherTemplate = "{condition}";
        settings.ShowWeatherCondition = true;
        settings.WeatherCustomOverridesEnabled = true;
        settings.WeatherConditionOverrides = "2=|Bit cloudy";

        // Raised, because the condition is the label beside the reading, not the reading itself.
        Assert.Contains("ᵇⁱᵗ ᶜˡᵒᵘᵈʸ", service.BuildSampleWeatherText());
    }

    [Fact]
    public void TheStatsSeparatorMovesTheChatboxLine()
    {
        var (service, settings) = Build();
        settings.WeatherTemplate = string.Empty;
        settings.ShowWeatherCondition = true;
        settings.WeatherStatsSeparator = " ~ ";

        Assert.Contains(" ~ ", service.BuildSampleWeatherText());
    }

    [Fact]
    public void TheClockSeparatorAndTheOrderReachTheDiscordLineOnly()
    {
        // The section says so in its own help text. The chatbox builder keeps the clock and the
        // weather as separate segments and joins them itself, so if this ever stops holding the
        // help text has become a lie and this test is where it gets caught.
        var (service, settings) = Build();
        settings.WeatherTemplate = string.Empty;
        settings.ShowWeatherCondition = true;

        settings.WeatherSeparator = " // ";
        settings.WeatherOrder = WeatherOrder.WeatherFirst;
        string chatbox = service.BuildSampleWeatherText();

        Assert.DoesNotContain(" // ", chatbox);
        Assert.DoesNotContain("13:37", chatbox);

        // The combined form is the Discord one, and it is the only place the separator lands.
        settings.WeatherFallbackMode = WeatherFallbackMode.ShowNA;
        Assert.Contains(" // ", service.BuildTimeWeatherText("13:37"));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class FixedClock : ITimeFormattingService
    {
        public string GetFormattedCurrentTime() => "13:37";
    }

    private sealed class DeniedConsent : IPrivacyConsentService
    {
        public bool IsApproved(PrivacyHook hook) => false;
        public ConsentState GetState(PrivacyHook hook) => ConsentState.Denied;
        public void Approve(PrivacyHook hook) { }
        public void Deny(PrivacyHook hook) { }
        public void Reset(PrivacyHook hook) { }
        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks) => Array.Empty<PrivacyHook>();
        public event EventHandler<ConsentChangedEventArgs> ConsentChanged { add { } remove { } }
    }
}
