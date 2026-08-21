using System;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Services.Voicemod;

public interface IVoicemodClientKeyProvider
{
    bool TryGetClientKey(out string clientKey);
    bool HasLocalClientKey { get; }
    void SaveLocalClientKey(string clientKey);
    void ClearLocalClientKey();
}

public sealed class VoicemodClientKeyProvider : IVoicemodClientKeyProvider
{
    public const string AssemblyMetadataKey = "VoicemodClientKey";

    private readonly ISettingsProvider<VoicemodSettings> _settingsProvider;
    private readonly IEncryptionService _encryption;

    public VoicemodClientKeyProvider(
        ISettingsProvider<VoicemodSettings> settingsProvider,
        IEncryptionService encryption)
    {
        _settingsProvider = settingsProvider;
        _encryption = encryption;
    }

    public bool HasLocalClientKey => TryGetLocalClientKey(out _);

    public bool TryGetClientKey(out string clientKey)
    {
        if (TryGetLocalClientKey(out clientKey))
        {
            return true;
        }

        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(VoicemodClientKeyProvider).Assembly;
        string? embeddedValue = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, AssemblyMetadataKey, StringComparison.Ordinal))
            ?.Value;

        if (!string.IsNullOrWhiteSpace(embeddedValue))
        {
            clientKey = embeddedValue.Trim();
            return true;
        }

        clientKey = string.Empty;
        return false;
    }

    public void SaveLocalClientKey(string clientKey)
    {
        string normalizedClientKey = clientKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedClientKey))
            throw new ArgumentException("A Voicemod client key is required.", nameof(clientKey));

        string? encryptedClientKey = _encryption.Encrypt(normalizedClientKey);
        if (string.IsNullOrWhiteSpace(encryptedClientKey))
            throw new InvalidOperationException("Windows could not protect the Voicemod client key.");

        _settingsProvider.Value.LocalClientKeyEncrypted = encryptedClientKey;
        _settingsProvider.Save();
    }

    public void ClearLocalClientKey()
    {
        if (string.IsNullOrEmpty(_settingsProvider.Value.LocalClientKeyEncrypted))
            return;

        _settingsProvider.Value.LocalClientKeyEncrypted = string.Empty;
        _settingsProvider.Save();
    }

    private bool TryGetLocalClientKey(out string clientKey)
    {
        string? localClientKey = _encryption.Decrypt(_settingsProvider.Value.LocalClientKeyEncrypted);
        if (!string.IsNullOrWhiteSpace(localClientKey))
        {
            clientKey = localClientKey.Trim();
            return true;
        }

        clientKey = string.Empty;
        return false;
    }
}
