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
/// DPAPI cannot be made to refuse on a healthy machine, so the protect step is overridden through
/// the seam the settings object exposes for exactly that. Everything else is the production code.
/// </summary>
internal sealed class UnprotectablePulsoidSettings : PulsoidModuleSettings
{
    protected override bool TryProtectToken(string plaintext, out string ciphertext)
    {
        ciphertext = string.Empty;
        return false;
    }
}

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
        Assert.False(reader.Value.StoredTokenUnreadable);
        Assert.False(reader.Value.TokenEncryptionFailed);
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
        var settings = new PulsoidModuleSettings { AccessTokenOAuthEncrypted = UndecryptableCipher };

        Assert.True(settings.StoredTokenUnreadable);
        // Nothing failed to *encrypt* here; conflating the two is what disabled heart rate for a
        // whole session on a perfectly good token.
        Assert.False(settings.TokenEncryptionFailed);
        // The bad blob is preserved rather than being blanked out: it may decrypt elsewhere,
        // and overwriting it is how a good token gets silently destroyed.
        Assert.Equal(UndecryptableCipher, settings.AccessTokenOAuthEncrypted);
        Assert.Equal(string.Empty, settings.AccessTokenOAuth);
    }

    /// <summary>A DPAPI blob from another account: valid base64, refuses to unprotect here.</summary>
    private static string UndecryptableCipher => Convert.ToBase64String(new byte[] { 1, 0, 0, 0, 9, 9, 9, 9 });

    [Fact]
    public void ClearStoredToken_AfterAFailedDecrypt_WipesTheCiphertextAndTheFailureFlag()
    {
        // The state Disconnect has to cope with: plaintext already empty, ciphertext still there.
        var settings = new PulsoidModuleSettings { AccessTokenOAuthEncrypted = UndecryptableCipher };
        Assert.True(settings.StoredTokenUnreadable);

        settings.ClearStoredToken();

        Assert.Equal(string.Empty, settings.AccessTokenOAuth);
        Assert.Equal(string.Empty, settings.AccessTokenOAuthEncrypted);
        Assert.False(settings.StoredTokenUnreadable);
        Assert.False(settings.TokenEncryptionFailed);
    }

    [Fact]
    public void AssigningEmpty_AfterAFailedDecrypt_IsNotSwallowedByThePlaintextGuard()
    {
        // The exact bug: the setter compared the incoming "" with the (already empty) plaintext,
        // returned early, and left the blob on disk forever — TokenProtectionFailed is not
        // serialized, so the next launch re-derived the lockout from the surviving ciphertext and
        // the user could never clear it from the UI.
        var settings = new PulsoidModuleSettings { AccessTokenOAuthEncrypted = UndecryptableCipher };

        settings.AccessTokenOAuth = string.Empty;

        Assert.Equal(string.Empty, settings.AccessTokenOAuthEncrypted);
        Assert.False(settings.StoredTokenUnreadable);
    }

    [Fact]
    public void ClearStoredToken_AfterAFailedDecrypt_RemovesTheBlobFromDiskToo()
    {
        // End to end: an undecryptable blob is on disk, the user presses Disconnect, and the next
        // launch must come up as "not connected" rather than re-deriving the lockout.
        var seed = new PulsoidModuleSettings { AccessTokenOAuthEncrypted = UndecryptableCipher };
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(seed));

        var provider = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        Assert.True(provider.Value.StoredTokenUnreadable);

        provider.Value.ClearStoredToken();
        provider.FlushPendingSave();
        provider.Dispose();

        var afterDisconnect = new JsonSettingsProvider<PulsoidModuleSettings>(_env);
        Assert.Equal(string.Empty, afterDisconnect.Value.AccessTokenOAuthEncrypted);
        Assert.Equal(string.Empty, afterDisconnect.Value.AccessTokenOAuth);
        Assert.False(afterDisconnect.Value.StoredTokenUnreadable);
        afterDisconnect.Dispose();
    }

    // ---- (a2) an encrypt failure is a storage problem, not a lost credential -------------------

    [Fact]
    public void EncryptFailure_KeepsTheWorkingPlaintextAndIsNotReportedAsUnreadable()
    {
        var settings = new UnprotectablePulsoidSettings { AccessTokenOAuth = "works-right-now" };

        Assert.Equal("works-right-now", settings.AccessTokenOAuth);
        Assert.True(settings.TokenEncryptionFailed);
        // Nothing is unreadable: there is a perfectly usable token in memory.
        Assert.False(settings.StoredTokenUnreadable);
    }

    [Fact]
    public void EncryptFailure_DoesNotLeaveASupersededTokenToResurrectOnTheNextLaunch()
    {
        var settings = new UnprotectablePulsoidSettings();

        // Pretend a previous session stored a different credential successfully. Any ciphertext
        // that does not decrypt to the token we now hold is superseded, and leaving it means the
        // next launch signs the user back in as the old token with nothing to show for it.
        settings.AccessTokenOAuthEncrypted = UndecryptableCipher;
        settings.AccessTokenOAuth = "the-new-token";

        Assert.Equal("the-new-token", settings.AccessTokenOAuth);
        Assert.True(settings.TokenEncryptionFailed);
        Assert.Equal(string.Empty, settings.AccessTokenOAuthEncrypted);
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

    // The transient-failure regression is covered by
    // PulsoidModuleAuthRestoreTests.Startup_WhenPulsoidIsUnreachable_*, which drives the real
    // PulsoidModule against a stubbed Pulsoid. The version that used to live here copied the
    // module's decision table into the test body with a constant `validation`, so two of its three
    // arms were unreachable and the assertion held by construction — it stayed green with the bug
    // reinstated, which is worse than having no test at all.

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
