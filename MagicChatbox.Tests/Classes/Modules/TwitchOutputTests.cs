using System;
using System.Net.Http;
using System.Reflection;
using TwitchLib.Api.Core.Exceptions;
using vrcosc_magicchatbox.Classes.Modules;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

public sealed class TwitchOutputTests
{
    private static TwitchSettings Settings() => new() { ChannelName = "channel" };

    private static string Build(TwitchSettings settings, int viewers = 1234, bool isLive = true)
        => TwitchModule.BuildOutputString(settings, "Beat Saber", viewers, 42, "Playing customs all night", isLive);

    [Fact]
    public void LiveIndicatorStaysFullSizeWhileItsNeighboursShrink()
    {
        var settings = Settings();
        Assert.True(settings.UseSmallText);

        string text = Build(settings);

        Assert.Contains("LIVE", text);
        Assert.DoesNotContain("ˡⁱᵛᵉ", text);
        Assert.Contains("ᵖˡᵃʸⁱⁿᵍ", text);
        Assert.Contains("ᵛⁱᵉʷᵉʳˢ", text);
    }

    [Fact]
    public void ValuesAreNeverRaised()
    {
        string text = Build(Settings());

        Assert.Contains("Beat Saber", text);
        Assert.Contains("1234", text);
        Assert.DoesNotContain("¹²³⁴", text);
    }

    [Fact]
    public void ACustomLiveMarkerIsPassedThroughUntouched()
    {
        var settings = Settings();
        settings.LivePrefix = "ON AIR";

        Assert.Contains("ON AIR", Build(settings));
    }

    [Fact]
    public void SmallTextOffLeavesTheLabelsAsTyped()
    {
        var settings = Settings();
        settings.UseSmallText = false;

        string text = Build(settings);

        Assert.Contains("LIVE", text);
        Assert.Contains("playing", text);
        Assert.Contains("viewers", text);
    }

    [Fact]
    public void RaisingTheLabelCostsExactlyWhatTheWordCost()
    {
        var raised = Settings();
        var plain = Settings();
        plain.UseSmallText = false;

        Assert.Equal(Build(plain).Length, Build(raised).Length);
    }

    [Fact]
    public void TemplatesGetTheSameLiveMarkerAsTheDefaultLayout()
    {
        var settings = Settings();
        settings.Template = "{live} {viewers}";

        string text = Build(settings);

        Assert.StartsWith("LIVE", text);
        Assert.Contains("1234", text);
    }

    [Fact]
    public void OfflineFallsBackToTheOfflineMessage()
    {
        var settings = Settings();
        settings.OfflineMessage = "offline";

        Assert.Equal("offline", Build(settings, isLive: false));
    }

    [Fact]
    public void TwitchLibs_expired_token_exception_is_treated_as_unauthorized()
    {
        using var response = new HttpResponseMessage();
        var exception = new TokenExpiredException("expired", response);
        MethodInfo? classifier = typeof(TwitchModule).GetMethod(
            "IsUnauthorized",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(classifier);
        Assert.True((bool)classifier!.Invoke(null, new object[] { exception })!);
    }

    [Fact]
    public void Wrapped_expired_token_messages_are_treated_as_unauthorized()
    {
        var exception = new InvalidOperationException(
            "outer",
            new Exception("Your request was blocked due to an expired token."));
        MethodInfo? classifier = typeof(TwitchModule).GetMethod(
            "IsUnauthorized",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(classifier);
        Assert.True((bool)classifier!.Invoke(null, new object[] { exception })!);
    }
}
