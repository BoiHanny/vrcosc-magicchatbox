using System;
using System.Text;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Voicemod;
using Xunit;

namespace MagicChatbox.Tests.Services.Voicemod;

public sealed class VoicemodClientKeyProviderTests
{
    [Fact]
    public void SaveLocalClientKey_StoresOnlyEncryptedValue_AndUsesItForConnection()
    {
        var settingsProvider = new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings());
        var provider = new VoicemodClientKeyProvider(settingsProvider, new ReversibleEncryptionService());

        provider.SaveLocalClientKey("  client-key-for-test  ");

        Assert.NotEqual("client-key-for-test", settingsProvider.Value.LocalClientKeyEncrypted);
        Assert.Equal(1, settingsProvider.SaveCount);
        Assert.True(provider.HasLocalClientKey);
        Assert.True(provider.TryGetClientKey(out string clientKey));
        Assert.Equal("client-key-for-test", clientKey);
    }

    [Fact]
    public void ClearLocalClientKey_RemovesThePersistedEncryptedValue()
    {
        var settingsProvider = new StubSettingsProvider<VoicemodSettings>(new VoicemodSettings());
        var provider = new VoicemodClientKeyProvider(settingsProvider, new ReversibleEncryptionService());
        provider.SaveLocalClientKey("client-key-for-test");

        provider.ClearLocalClientKey();

        Assert.Equal(string.Empty, settingsProvider.Value.LocalClientKeyEncrypted);
        Assert.False(provider.HasLocalClientKey);
        Assert.Equal(2, settingsProvider.SaveCount);
    }

    private sealed class StubSettingsProvider<T> : ISettingsProvider<T> where T : class, new()
    {
        public T Value { get; }
        public int SaveCount { get; private set; }
        public event EventHandler? SettingsChanged;

        public StubSettingsProvider(T value) => Value = value;

        public void Save()
        {
            SaveCount++;
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FlushPendingSave() { }
        public void Reload() { }
    }

    private sealed class ReversibleEncryptionService : IEncryptionService
    {
        public string? Encrypt(string plainText)
            => string.IsNullOrEmpty(plainText) ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

        public string? Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;

            return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
        }
    }
}
