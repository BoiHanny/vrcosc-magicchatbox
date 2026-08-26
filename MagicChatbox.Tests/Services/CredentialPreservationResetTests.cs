using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using Xunit;

namespace MagicChatbox.Tests.Services;

// Every credential here is stored as a plaintext/encrypted pair whose setters write through to each
// other, so resetting one half is enough to destroy the half being protected. These cover each
// settings class that holds a token the user cannot recover from inside the app.
public class CredentialPreservationResetTests
{
    [Fact]
    public void Resetting_openai_keeps_the_key_and_the_organisation()
    {
        var settings = new OpenAISettings
        {
            AccessToken = "sk-live-key",
            OrganizationID = "org-live",
        };

        new SettingsResetService().ResetAll(new StubSettingsProvider<OpenAISettings>(settings));

        Assert.Equal("sk-live-key", settings.AccessToken);
        Assert.Equal("org-live", settings.OrganizationID);
        Assert.NotEmpty(settings.AccessTokenEncrypted);
    }

    [Fact]
    public void Resetting_twitch_keeps_both_halves_of_each_pair()
    {
        var settings = new TwitchSettings
        {
            ClientId = "twitch-client",
            AccessToken = "twitch-token",
        };
        var encryptedClientId = settings.ClientIdEncrypted;
        var encryptedToken = settings.AccessTokenEncrypted;

        new SettingsResetService().ResetAll(new StubSettingsProvider<TwitchSettings>(settings));

        Assert.Equal("twitch-client", settings.ClientId);
        Assert.Equal("twitch-token", settings.AccessToken);
        Assert.Equal(encryptedClientId, settings.ClientIdEncrypted);
        Assert.Equal(encryptedToken, settings.AccessTokenEncrypted);
    }

    [Fact]
    public void Resetting_spotify_keeps_the_refresh_token()
    {
        // The refresh token is the one that matters: losing it forces the whole OAuth flow again.
        var settings = new SpotifySettings
        {
            AccessToken = "spotify-access",
            RefreshToken = "spotify-refresh",
        };

        new SettingsResetService().ResetAll(new StubSettingsProvider<SpotifySettings>(settings));

        Assert.Equal("spotify-access", settings.AccessToken);
        Assert.Equal("spotify-refresh", settings.RefreshToken);
    }

    [Fact]
    public void Resetting_discord_keeps_the_tokens_and_the_voice_client_id()
    {
        var settings = new DiscordSettings
        {
            AccessToken = "discord-access",
            RefreshToken = "discord-refresh",
            VoiceClientId = "123456789",
        };

        new SettingsResetService().ResetAll(new StubSettingsProvider<DiscordSettings>(settings));

        Assert.Equal("discord-access", settings.AccessToken);
        Assert.Equal("discord-refresh", settings.RefreshToken);
        Assert.Equal("123456789", settings.VoiceClientId);
    }

    [Fact]
    public void Resetting_pulsoid_keeps_the_oauth_token()
    {
        var settings = new PulsoidModuleSettings { AccessTokenOAuth = "pulsoid-oauth" };

        new SettingsResetService().ResetAll(new StubSettingsProvider<PulsoidModuleSettings>(settings));

        Assert.Equal("pulsoid-oauth", settings.AccessTokenOAuth);
    }

    [Fact]
    public void Asking_for_a_full_wipe_clears_the_credentials_too()
    {
        var settings = new TwitchSettings
        {
            ClientId = "twitch-client",
            AccessToken = "twitch-token",
        };

        new SettingsResetService().ResetAll(
            new StubSettingsProvider<TwitchSettings>(settings),
            preserveCredentials: false);

        Assert.Empty(settings.ClientId);
        Assert.Empty(settings.AccessToken);
        Assert.Empty(settings.ClientIdEncrypted);
        Assert.Empty(settings.AccessTokenEncrypted);
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
