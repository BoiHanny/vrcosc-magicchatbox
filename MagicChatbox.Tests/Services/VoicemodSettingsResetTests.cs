using System;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

public class VoicemodSettingsResetTests
{
    [Fact]
    public void Resetting_the_section_keeps_the_client_key()
    {
        // The key is a credential the user pasted in by hand, and re-requesting one from Voicemod
        // is a manual form submission - losing it to a settings reset is not recoverable in-app.
        var settings = new VoicemodSettings
        {
            LocalClientKeyEncrypted = "protected-blob",
            SoundAnnouncementDurationSeconds = 14,
        };
        var provider = new StubSettingsProvider<VoicemodSettings>(settings);
        var reset = new SettingsResetService();

        reset.ResetAll(provider);

        Assert.Equal("protected-blob", settings.LocalClientKeyEncrypted);
        Assert.Equal(8, settings.SoundAnnouncementDurationSeconds);
    }

    [Fact]
    public void Resetting_the_section_restores_the_soundboard_only_default()
    {
        // The soundboard is the feature people come for; voice and mic are opt-in. A reset has to
        // land back on that, not on everything-at-once.
        var settings = new VoicemodSettings
        {
            VoiceControlEnabled = true,
            SoundboardControlEnabled = false,
            MicControlEnabled = true,
        };
        var reset = new SettingsResetService();

        reset.ResetAll(new StubSettingsProvider<VoicemodSettings>(settings));

        Assert.True(settings.SoundboardControlEnabled);
        Assert.False(settings.VoiceControlEnabled);
        Assert.False(settings.MicControlEnabled);
        Assert.True(settings.AnyFeatureEnabled);
    }

    [Fact]
    public void Resetting_the_section_clears_pinned_and_recent_sounds()
    {
        var settings = new VoicemodSettings();
        settings.SetFavoriteSound("airhorn", true);
        settings.RecordSoundUse("airhorn");
        var reset = new SettingsResetService();

        reset.ResetAll(new StubSettingsProvider<VoicemodSettings>(settings));

        Assert.Empty(settings.FavoriteSoundIds);
        Assert.Empty(settings.RecentSoundIds);
    }

    [Fact]
    public void Asking_for_a_full_wipe_still_clears_the_key()
    {
        var settings = new VoicemodSettings { LocalClientKeyEncrypted = "protected-blob" };
        var reset = new SettingsResetService();

        reset.ResetAll(new StubSettingsProvider<VoicemodSettings>(settings), preserveCredentials: false);

        Assert.Equal(string.Empty, settings.LocalClientKeyEncrypted);
    }

    private sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; } = value;
        public event EventHandler? SettingsChanged;
        public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        public void FlushPendingSave() { }
        public void Reload() { }
    }
}
