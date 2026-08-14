using System;
using System.IO;
using Newtonsoft.Json;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using Xunit;

namespace MagicChatbox.Tests.Classes.Modules;

/// <summary>
/// Regression cover for "Pulsoid authentication is lost across app restarts".
/// The token always survived; what did not was the state describing it, and a transient
/// validation failure was indistinguishable from a revoked token.
/// </summary>
public sealed class PulsoidAuthPersistenceTests : IDisposable
{
    private sealed class TempEnvironment : IEnvironmentService
    {
        public TempEnvironment(string root) => DataPath = root;
        public string DataPath { get; }
        public string LogPath => Path.Combine(DataPath, "logs");
        public string VrcPath => DataPath;
        public void SetCustomProfile(int profileNumber) => throw new NotSupportedException();
    }

    private readonly string _dir;
    private readonly TempEnvironment _env;

    private string SettingsFile => Path.Combine(_dir, $"{nameof(PulsoidModuleSettings)}.json");

    public PulsoidAuthPersistenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "MagicChatboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _env = new TempEnvironment(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    // ---- (a) the stored token survives a real save/load cycle ----------------------------------

    [Fact]
    public void AccessToken_SurvivesSaveAndReload_ThroughTheSettingsProvider()
    {
        const string token = "3f0c8f1a-1111-2222-3333-9b6d4e5f6a70";

        var writer = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        writer.Value.AccessTokenOAuth = token;
        writer.FlushPendingSave();
        writer.Dispose();

        // Nothing readable on disk, but a fresh provider (a "restart") gets the token back.
        string onDisk = File.ReadAllText(SettingsFile);
        Assert.DoesNotContain(token, onDisk);

        var reader = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        Assert.Equal(token, reader.Value.AccessTokenOAuth);
        Assert.False(reader.Value.TokenProtectionFailed);
        reader.Dispose();
    }

    [Fact]
    public void AccessToken_IsWrittenImmediately_NotLeftToTheDebounce()
    {
        const string token = "flush-me-now-4a2b";

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.AccessTokenOAuth = token;

        // FlushPendingSave is what PulsoidModule.SaveSettings calls; no waiting on a 2s timer.
        provider.FlushPendingSave();

        var roundTripped = JsonConvert.DeserializeObject<PulsoidModuleSettings>(File.ReadAllText(SettingsFile));
        Assert.NotNull(roundTripped);
        Assert.Equal(token, roundTripped!.AccessTokenOAuth);
        provider.Dispose();
    }

    [Fact]
    public void ClearingAccessToken_WipesBothHalvesOnDisk()
    {
        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.AccessTokenOAuth = "to-be-removed";
        provider.FlushPendingSave();

        provider.Value.AccessTokenOAuth = string.Empty;
        provider.FlushPendingSave();

        var roundTripped = JsonConvert.DeserializeObject<PulsoidModuleSettings>(File.ReadAllText(SettingsFile));
        Assert.NotNull(roundTripped);
        Assert.Equal(string.Empty, roundTripped!.AccessTokenOAuth);
        Assert.Equal(string.Empty, roundTripped.AccessTokenOAuthEncrypted);
        provider.Dispose();
    }

    [Fact]
    public void UndecryptableStoredToken_KeepsTheCiphertextAndReportsTheFailure()
    {
        // A DPAPI blob from another account looks like this: valid base64, refuses to unprotect.
        string junkCipher = Convert.ToBase64String(new byte[] { 1, 0, 0, 0, 9, 9, 9, 9 });
        var settings = new PulsoidModuleSettings { AccessTokenOAuthEncrypted = junkCipher };

        Assert.True(settings.TokenProtectionFailed);
        // The bad blob is preserved rather than being blanked out: it may decrypt elsewhere,
        // and overwriting it is how a good token gets silently destroyed.
        Assert.Equal(junkCipher, settings.AccessTokenOAuthEncrypted);
        Assert.Equal(string.Empty, settings.AccessTokenOAuth);
    }

    // ---- (b) a transient failure never becomes a terminal unauthenticated state ----------------

    [Theory]
    [InlineData(PulsoidAuthState.Authenticated, true)]
    [InlineData(PulsoidAuthState.Unverified, true)]
    [InlineData(PulsoidAuthState.Unreachable, true)]
    [InlineData(PulsoidAuthState.Rejected, false)]
    [InlineData(PulsoidAuthState.Unreadable, false)]
    [InlineData(PulsoidAuthState.NoToken, false)]
    public void AuthConnected_IsDerivedFromAuthState_AndSurvivesBeingUnreachable(PulsoidAuthState state, bool expectedConnected)
    {
        var display = new PulsoidDisplayState { AuthState = state };

        Assert.Equal(expectedConnected, display.AuthConnected);
    }

    [Fact]
    public void AuthStatusText_DistinguishesNoTokenFromRejectedFromUnreachable()
    {
        var display = new PulsoidDisplayState();

        display.AuthState = PulsoidAuthState.NoToken;
        string noToken = display.AuthStatusText;

        display.AuthState = PulsoidAuthState.Rejected;
        string rejected = display.AuthStatusText;

        display.AuthState = PulsoidAuthState.Unreachable;
        string unreachable = display.AuthStatusText;

        Assert.NotEqual(noToken, rejected);
        Assert.NotEqual(rejected, unreachable);
        Assert.NotEqual(noToken, unreachable);
        Assert.Contains("Not connected", noToken);
        Assert.Contains("rejected", rejected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reach Pulsoid", unreachable, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthConnected_RaisesChangeNotificationForTheDerivedProperties()
    {
        var display = new PulsoidDisplayState();
        var seen = new System.Collections.Generic.List<string?>();
        display.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

        display.AuthState = PulsoidAuthState.Authenticated;

        Assert.Contains(nameof(PulsoidDisplayState.AuthState), seen);
        Assert.Contains(nameof(PulsoidDisplayState.AuthConnected), seen);
        Assert.Contains(nameof(PulsoidDisplayState.AuthStatusText), seen);
    }

    [Fact]
    public void TransientValidationFailure_NeitherClearsTheTokenNorSignsTheUserOut()
    {
        const string token = "still-perfectly-good-token";

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        provider.Value.AccessTokenOAuth = token;
        provider.FlushPendingSave();

        // Restart: the module seeds an optimistic signed-in state from the stored credential.
        var display = new PulsoidDisplayState();
        var reloaded = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        display.AuthState = string.IsNullOrWhiteSpace(reloaded.Value.AccessTokenOAuth)
            ? PulsoidAuthState.NoToken
            : PulsoidAuthState.Unverified;
        Assert.True(display.AuthConnected);

        // Pulsoid is unreachable (offline / timeout / 429 / 5xx) — the outcome the validator
        // reports for every one of those.
        var validation = PulsoidTokenValidation.Unknown;
        if (validation == PulsoidTokenValidation.Invalid)
            display.AuthState = PulsoidAuthState.Rejected;
        else if (validation == PulsoidTokenValidation.Unknown)
            display.AuthState = PulsoidAuthState.Unreachable;
        else
            display.AuthState = PulsoidAuthState.Authenticated;

        Assert.Equal(PulsoidAuthState.Unreachable, display.AuthState);
        Assert.True(display.AuthConnected);
        Assert.Equal(token, reloaded.Value.AccessTokenOAuth);

        // And the credential is still on disk for the next launch.
        reloaded.Dispose();
        provider.Dispose();
        var afterOutage = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        Assert.Equal(token, afterOutage.Value.AccessTokenOAuth);
        afterOutage.Dispose();
    }

    [Fact]
    public void PulsoidAuthState_IsOnlyEverReachedFromTheDerivedBoolean()
    {
        // The legacy boolean setter still has to agree with the enum, since existing bindings
        // and IAppState callers go through it.
        var display = new PulsoidDisplayState { AuthState = PulsoidAuthState.Rejected };

        display.AuthConnected = true;
        Assert.Equal(PulsoidAuthState.Authenticated, display.AuthState);

        display.AuthConnected = false;
        Assert.Equal(PulsoidAuthState.NoToken, display.AuthState);
    }
}
