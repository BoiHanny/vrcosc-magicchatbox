using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Services;

public sealed class SettingsResetService : ISettingsResetService
{
    private static readonly HashSet<string> MetadataPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(VersionedSettings.AppVersion),
        nameof(VersionedSettings.SchemaVersion),
        nameof(VersionedSettings.MigratedAt)
    };

    private static readonly HashSet<string> CredentialPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AccessToken",
        "AccessTokenEncrypted",
        "AccessTokenOAuth",
        "AccessTokenOAuthEncrypted",
        "RefreshToken",
        "RefreshTokenEncrypted",
        "ClientId",
        "ClientIdEncrypted",
        "VoiceClientId",
        "VoiceClientIdEncrypted",
        "OrganizationID",
        "OrganizationIDEncrypted",
        "TokenExpiresAtUtcTicks",
        "HasRpcScope"
    };

    public int ResetAll<T>(ISettingsProvider<T> provider, bool preserveCredentials = true) where T : class, new()
    {
        var propertyNames = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => p.Name)
            .ToArray();

        return ResetProperties(provider, propertyNames, preserveCredentials);
    }

    public int ResetProperties<T>(
        ISettingsProvider<T> provider,
        IEnumerable<string> propertyNames,
        bool preserveCredentials = true)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(propertyNames);

        var current = provider.Value;
        var defaults = new T();
        int resetCount = 0;

        foreach (var propertyName in propertyNames.Distinct(StringComparer.Ordinal))
        {
            var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                Logging.WriteInfo($"[SettingsReset] {typeof(T).Name}.{propertyName}: property not found.");
                continue;
            }

            if (!CanResetProperty(property, preserveCredentials))
                continue;

            try
            {
                var defaultValue = property.GetValue(defaults);
                property.SetValue(current, defaultValue);
                resetCount++;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"[SettingsReset] Failed to reset {typeof(T).Name}.{property.Name}: {ex.Message}");
            }
        }

        provider.Save();
        Logging.WriteInfo($"[SettingsReset] Reset {resetCount} setting(s) on {typeof(T).Name}.");
        return resetCount;
    }

    private static bool CanResetProperty(PropertyInfo property, bool preserveCredentials)
    {
        if (!property.CanRead || !property.CanWrite)
            return false;

        if (MetadataPropertyNames.Contains(property.Name))
            return false;

        if (property.GetIndexParameters().Length > 0)
            return false;

        if (IsJsonIgnoredNonCredential(property))
            return false;

        return !preserveCredentials || !CredentialPropertyNames.Contains(property.Name);
    }

    private static bool IsJsonIgnoredNonCredential(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonIgnoreAttribute>() != null
            && !CredentialPropertyNames.Contains(property.Name);
    }
}
