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
        "HasRpcScope",
        "LocalClientKey",
        "LocalClientKeyEncrypted"
    };

    private const string EncryptedSuffix = "Encrypted";

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
        var written = new HashSet<string>(StringComparer.Ordinal);
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

            var target = ResolveWriteTarget<T>(property);
            if (!written.Add(target.Name))
                continue;

            try
            {
                var defaultValue = target.GetValue(defaults);
                target.SetValue(current, defaultValue);
                resetCount++;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"[SettingsReset] Failed to reset {typeof(T).Name}.{target.Name}: {ex.Message}");
            }
        }

        // Flush rather than save: the writes above armed the debounced auto-save, and letting that
        // fire afterwards would persist whatever a listener wrote back while reacting to the reset.
        provider.FlushPendingSave();
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

        return !preserveCredentials || !IsPreservedCredential(property.Name);
    }

    private static bool IsJsonIgnoredNonCredential(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonIgnoreAttribute>() != null
            && !IsPreservedCredential(property.Name);
    }

    // Half of an encrypted pair cannot be protected on its own: the two halves share one stored value
    // and each setter rewrites the other, so clearing either one also clears the one being protected.
    private static bool IsPreservedCredential(string propertyName)
        => CredentialPropertyNames.Contains(propertyName)
            || CredentialPropertyNames.Contains(TwinPropertyName(propertyName));

    private static string TwinPropertyName(string propertyName)
        => propertyName.EndsWith(EncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? propertyName[..^EncryptedSuffix.Length]
            : propertyName + EncryptedSuffix;

    // For the same reason, an encrypted half is reset through its plaintext twin: writing the
    // encrypted half directly blanks the plaintext one instead of restoring its default.
    private static PropertyInfo ResolveWriteTarget<T>(PropertyInfo property)
    {
        if (!property.Name.EndsWith(EncryptedSuffix, StringComparison.Ordinal))
            return property;

        var plaintext = typeof(T).GetProperty(
            property.Name[..^EncryptedSuffix.Length],
            BindingFlags.Public | BindingFlags.Instance);

        return plaintext is { CanRead: true, CanWrite: true } ? plaintext : property;
    }
}
