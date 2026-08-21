using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class VoicemodOscProvider : IOscProvider
{
    private const string VoiceIcon = "🎙️";

    private const int VoiceIconCost = 3;

    private const int QuoteCost = 2;

    private const string NoEffectVoiceId = "nofx";

    private readonly IntegrationSettings _integrationSettings;
    private readonly VoicemodSettings _settings;
    private readonly VoicemodDisplayState _display;

    public VoicemodOscProvider(
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<VoicemodSettings> settingsProvider,
        VoicemodDisplayState display)
    {
        _integrationSettings = integrationSettingsProvider.Value;
        _settings = settingsProvider.Value;
        _display = display;
    }

    public string SortKey => "Voicemod";
    public string UiKey => "Voicemod";
    public int Priority => 80;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _integrationSettings.IntgrVoicemod
           && HasAnyOutput
           && (isVR ? _integrationSettings.IntgrVoicemod_VR : _integrationSettings.IntgrVoicemod_DESKTOP);

    private bool AnnouncesSounds => _settings.AnnounceSoundboardToChat && _settings.SoundboardControlEnabled;

    private bool AnnouncesVoice => _settings.AnnounceVoiceToChat && _settings.VoiceControlEnabled;

    private bool HasAnyOutput => AnnouncesSounds || AnnouncesVoice;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!HasAnyOutput)
            return null;

        int budget = context.RemainingCharsIf(string.Empty);

        if (AnnouncesSounds
            && TransientWindow.ShouldShow(
                onlyOnChange: true,
                _display.LastSoundPlaybackStartedUtc,
                DateTime.UtcNow,
                _settings.SoundAnnouncementDurationSeconds))
        {
            string sound = BuildSegment(_display.LastPlayedSoundName, budget);
            if (!string.IsNullOrEmpty(sound))
                return new OscSegment { Text = sound };
        }

        if (!AnnouncesVoice)
            return null;

        string voice = BuildVoiceSegment(
            _display.IsConnected ? _display.CurrentVoiceId : null,
            _display.CurrentVoiceName,
            budget);

        return string.IsNullOrEmpty(voice) ? null : new OscSegment { Text = voice };
    }

    public static string BuildSegment(string? soundName, int budget)
        => SoundpadOscProvider.BuildSegment(soundName, withIcon: true, budget);

    public static string BuildVoiceSegment(string? voiceId, string? voiceName, int budget)
    {
        if (budget <= 0)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(voiceId)
            || string.Equals(voiceId, NoEffectVoiceId, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string clean = SegmentWriter.Tidy(voiceName);
        if (clean.Length == 0)
            return string.Empty;

        return SegmentWriter.Fit(
            budget,
            () => ComposeVoice(clean, withIcon: true),
            () => ComposeVoice(SegmentWriter.Truncate(clean, VoiceRoom(budget, withIcon: true)), withIcon: true),
            () => ComposeVoice(SegmentWriter.Truncate(clean, VoiceRoom(budget, withIcon: false)), withIcon: false));
    }

    private static int VoiceRoom(int budget, bool withIcon)
        => budget - QuoteCost - (withIcon ? VoiceIconCost : 0);

    private static string ComposeVoice(string? voiceName, bool withIcon)
        => string.IsNullOrEmpty(voiceName)
            ? string.Empty
            : new SegmentWriter()
                .Field(OscText.Raw(withIcon ? VoiceIcon : null), OscText.Value($"'{voiceName}'"))
                .Text;
}
