using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.Core.Osc.Providers;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Core.Osc;

public sealed class VoicemodOscProviderTests
{
    [Fact]
    public void ARecentlyPlayedSound_IsIncludedUsingTheSoundpadChatFormat()
    {
        var display = new VoicemodDisplayState();
        display.RecordSoundPlayback("Air horn");
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings
            {
                IntgrVoicemod = true,
                IntgrVoicemod_DESKTOP = true,
            }),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings()),
            display);

        OscSegment? segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.Equal("🎶 'Air horn'", segment.Text);
    }

    [Fact]
    public void TheRouteChipsControlWhichModeReceivesTheSound()
    {
        var settings = new IntegrationSettings
        {
            IntgrVoicemod = true,
            IntgrVoicemod_DESKTOP = true,
            IntgrVoicemod_VR = false,
        };
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(settings),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings()),
            new VoicemodDisplayState());

        Assert.True(provider.IsEnabledForCurrentMode(isVR: false));
        Assert.False(provider.IsEnabledForCurrentMode(isVR: true));
    }

    [Fact]
    public void AnExpiredOrClearedPlayback_IsNotShown()
    {
        var display = new VoicemodDisplayState();
        display.RecordSoundPlayback("Air horn");
        display.ClearSoundPlayback();
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings { IntgrVoicemod = true }),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings()),
            display);

        Assert.Null(provider.TryBuild(Context()));
    }

    [Fact]
    public void ADisabledAnnouncementPreference_HidesTheSoundWithoutDisablingVoicemodControls()
    {
        var display = new VoicemodDisplayState();
        display.RecordSoundPlayback("Air horn");
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings { IntgrVoicemod = true }),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings
            {
                AnnounceSoundboardToChat = false,
            }),
            display);

        Assert.False(provider.IsEnabledForCurrentMode(isVR: false));
        Assert.Null(provider.TryBuild(Context()));
    }

    [Fact]
    public void TheActiveVoice_IsShownOnlyWhenTheVoiceAnnouncementIsSwitchedOn()
    {
        var display = new VoicemodDisplayState { ConnectionState = VoicemodConnectionState.Connected };
        display.ReplaceVoices(
            [new VoicemodVoice("robot", "Robot", true, false, false, false, false, string.Empty)],
            "robot");

        var settings = new VoicemodSettings { AnnounceVoiceToChat = false, VoiceControlEnabled = true };
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings
            {
                IntgrVoicemod = true,
                IntgrVoicemod_DESKTOP = true,
            }),
            new StubSettingsProvider<VoicemodSettings>(settings),
            display);

        Assert.Null(provider.TryBuild(Context()));

        settings.AnnounceVoiceToChat = true;
        OscSegment? segment = provider.TryBuild(Context());

        Assert.NotNull(segment);
        Assert.Equal("🎙️ 'Robot'", segment.Text);
    }

    [Fact]
    public void NoEffect_IsNotWorthAChatboxLine()
    {
        var display = new VoicemodDisplayState { ConnectionState = VoicemodConnectionState.Connected };
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings
            {
                IntgrVoicemod = true,
                IntgrVoicemod_DESKTOP = true,
            }),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings { AnnounceVoiceToChat = true, VoiceControlEnabled = true }),
            display);

        Assert.Null(provider.TryBuild(Context()));
    }

    [Fact]
    public void SwitchingTheFeatureOff_SilencesItsChatboxOutputToo()
    {
        var display = new VoicemodDisplayState();
        display.RecordSoundPlayback("Air horn");

        var settings = new VoicemodSettings { SoundboardControlEnabled = false };
        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings
            {
                IntgrVoicemod = true,
                IntgrVoicemod_DESKTOP = true,
            }),
            new StubSettingsProvider<VoicemodSettings>(settings),
            display);

        Assert.False(provider.IsEnabledForCurrentMode(isVR: false));
        Assert.Null(provider.TryBuild(Context()));
    }

    [Fact]
    public void ADisconnectedVoicemod_NeverReportsAVoice()
    {
        var display = new VoicemodDisplayState { ConnectionState = VoicemodConnectionState.Reconnecting };
        display.ReplaceVoices(
            [new VoicemodVoice("robot", "Robot", true, false, false, false, false, string.Empty)],
            "robot");

        var provider = new VoicemodOscProvider(
            new StubSettingsProvider<IntegrationSettings>(new IntegrationSettings
            {
                IntgrVoicemod = true,
                IntgrVoicemod_DESKTOP = true,
            }),
            new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings { AnnounceVoiceToChat = true, VoiceControlEnabled = true }),
            display);

        Assert.Null(provider.TryBuild(Context()));
    }

    private static OscBuildContext Context() => new()
    {
        Separator = " ┆ ",
        Prefix = string.Empty,
        Suffix = string.Empty,
    };

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public event EventHandler? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        public void FlushPendingSave() { }
        public void Reload() { }
    }
}
