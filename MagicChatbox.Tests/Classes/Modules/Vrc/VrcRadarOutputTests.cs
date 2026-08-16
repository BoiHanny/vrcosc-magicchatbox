using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using MagicChatbox.Tests.TestDoubles;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules.Vrc;

/// <summary>
/// Drives the real module, because what the chatbox gets is the template after substitution - and
/// three stock presets used to print their own tokens because nothing ever filled them in.
/// </summary>
public class VrcRadarOutputTests
{
    // ---- doubles -------------------------------------------------------------------------------

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public void Save() { }
        public void FlushPendingSave() { }
        public void Reload() { }
        public event EventHandler SettingsChanged { add { } remove { } }
    }

    private sealed class FakeAppState : IAppState
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public bool MasterSwitch { get; set; } = true;
        public bool IsVRRunning { get; set; }
        public bool BussyBoysMode { get; set; }
        public bool Egg_Dev { get; set; }
        public bool PulsoidAuthConnected { get; set; }
        public PulsoidAuthState PulsoidAuthState { get; set; } = PulsoidAuthState.NoToken;
        public int MainWindowBlurEffect { get; set; }
    }

    /// <summary>Runs everything inline: xUnit has no WPF dispatcher to marshal to.</summary>
    private sealed class InlineDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> func) => func();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public bool CheckAccess() => true;
        public void BeginInvoke(Action action) => action();
        public void Shutdown() { }
    }

    private sealed class ApprovingConsent : IPrivacyConsentService
    {
        public bool IsApproved(PrivacyHook hook) => true;
        public ConsentState GetState(PrivacyHook hook) => ConsentState.Approved;
        public void Approve(PrivacyHook hook) { }
        public void Deny(PrivacyHook hook) { }
        public void Reset(PrivacyHook hook) { }
        public IReadOnlyList<PrivacyHook> GetHooksRequiringConsent(IEnumerable<PrivacyHook> hooks) => Array.Empty<PrivacyHook>();
        public event EventHandler<ConsentChangedEventArgs> ConsentChanged { add { } remove { } }
    }

    // ---- fixture -------------------------------------------------------------------------------

    private static VrcLogModule Radar(VrcLogSettings settings, string world, int players)
    {
        var module = new VrcLogModule(
            new StubSettingsProvider<VrcLogSettings>(settings),
            new IntegrationSettings(),
            new FakeAppState(),
            new FakeOscSender(),
            new InlineDispatcher(),
            new ApprovingConsent());

        module.CurrentWorldName = world;
        module.PlayerCount = players;
        return module;
    }

    private static VrcLogSettings Settings(string template) => new()
    {
        DisplayMode = RadarDisplayMode.AlwaysShow,
        TemplateWorld = template,
        ShowInstanceType = false,
        ShowRegion = false,
    };

    // ---- the tests -----------------------------------------------------------------------------

    [Fact]
    public void No_placeholder_survives_into_the_chatbox()
    {
        // "World Host", "Host Stats" and "Event Host" all shipped tokens nothing replaced.
        foreach ((_, string template) in VrcLogSettings.WorldTemplatePresets)
        {
            var radar = Radar(Settings(template), "The Black Cat", 12);

            string? text = radar.GetOutputString();

            Assert.NotNull(text);
            Assert.DoesNotContain("{", text!);
            Assert.DoesNotContain("}", text!);
        }
    }

    [Fact]
    public void The_session_peak_prints_its_number()
    {
        var radar = Radar(Settings("🌎 {world} | Peak: {peak_session}"), "The Black Cat", 12);
        radar.PeakPlayerCountThisSession = 27;

        Assert.Equal("🌎 The Black Cat | Peak: 27", radar.GetOutputString());
    }

    [Fact]
    public void A_world_name_out_of_the_log_cannot_take_the_line_on_its_own()
    {
        var radar = Radar(Settings("🌎 {world} | 👥 {count}"), new string('w', 400), 12);

        string? text = radar.GetOutputString();

        Assert.NotNull(text);
        Assert.True(text!.Length <= Constants.OscMaxMessageLength, $"the segment came to {text.Length}");
        // The player count is the part a template exists to show, so it is not what gets lost.
        Assert.EndsWith("| 👥 12", text);
    }

    [Fact]
    public void The_radar_takes_only_the_room_it_was_given()
    {
        var radar = Radar(Settings("🌎 {world} | 👥 {count}"), new string('w', 400), 12);

        string? text = radar.GetOutputString(40);

        Assert.NotNull(text);
        Assert.True(text!.Length <= 40, $"the segment came to {text.Length}");
        Assert.Contains(OscGlyphs.Ellipsis, text);
    }

    [Fact]
    public void With_no_room_left_the_radar_says_nothing_rather_than_overshooting()
    {
        var radar = Radar(Settings("🌎 {world} | 👥 {count}"), "The Black Cat", 12);

        Assert.True(string.IsNullOrEmpty(radar.GetOutputString(0)));
    }

    [Fact]
    public void A_duration_that_has_not_started_leaves_no_token_behind()
    {
        // The join time only arrives with a log line, and the token must not print in the meantime.
        var radar = Radar(Settings("⏱️ {session_time}"), "The Black Cat", 12);

        string? text = radar.GetOutputString();

        Assert.NotNull(text);
        Assert.DoesNotContain("{session_time}", text!);
    }
}
