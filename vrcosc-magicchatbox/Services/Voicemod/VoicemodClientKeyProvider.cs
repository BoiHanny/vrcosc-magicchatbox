using System;
using System.Linq;
using System.Reflection;

namespace vrcosc_magicchatbox.Services.Voicemod;

public interface IVoicemodClientKeyProvider
{
    bool TryGetClientKey(out string clientKey);
}

public sealed class VoicemodClientKeyProvider : IVoicemodClientKeyProvider
{
    public const string EnvironmentVariableName = "MAGICCHATBOX_VOICEMOD_CLIENT_KEY";
    public const string AssemblyMetadataKey = "VoicemodClientKey";

    public bool TryGetClientKey(out string clientKey)
    {
        string? environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            clientKey = environmentValue.Trim();
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
}
