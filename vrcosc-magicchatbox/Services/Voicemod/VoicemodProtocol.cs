using System;
using System.Collections.Generic;
using System.Text.Json;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;

namespace vrcosc_magicchatbox.Services.Voicemod;

public sealed record VoicemodEnvelope(
    string Action,
    string? ActionId,
    string? AppVersion,
    JsonElement ActionObject,
    JsonElement Payload,
    JsonElement Context,
    JsonElement Root);

public static class VoicemodProtocol
{
    public static IReadOnlyList<int> Ports { get; } =
    [
        59129,
        20000,
        39273,
        42152,
        43782,
        46667,
        35679,
        37170,
        38501,
        33952,
        30546,
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    public static string CreateMessage(string action, object? payload = null, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return JsonSerializer.Serialize(
            new
            {
                id = id ?? Guid.NewGuid().ToString("D"),
                action,
                payload = payload ?? new Dictionary<string, object?>(),
            },
            SerializerOptions);
    }

    public static bool TryParseEnvelope(string json, out VoicemodEnvelope? envelope, out string? error)
    {
        envelope = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The message was empty.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The message root was not a JSON object.";
                return false;
            }

            string? action = GetString(root, "actionType")
                ?? GetString(root, "action")
                ?? GetString(root, "type");

            if (string.IsNullOrWhiteSpace(action))
            {
                error = "The message did not contain an action.";
                return false;
            }

            envelope = new VoicemodEnvelope(
                action,
                GetString(root, "actionId") ?? GetString(root, "actionID") ?? GetString(root, "id"),
                GetString(root, "appVersion"),
                CloneNormalizedProperty(root, "actionObject"),
                CloneNormalizedProperty(root, "payload"),
                CloneNormalizedProperty(root, "context"),
                root.Clone());
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static (int Code, string Description)? ReadRegistrationStatus(VoicemodEnvelope envelope)
    {
        if (!TryGetDataProperty(envelope, "status", out JsonElement status)
            || status.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? code = GetInt32(status, "code");
        if (code == null)
            return null;

        return (code.Value, GetString(status, "description") ?? string.Empty);
    }

    public static (IReadOnlyList<VoicemodVoice> Voices, string CurrentVoiceId)? ReadVoices(
        VoicemodEnvelope envelope)
    {
        JsonElement data = SelectDataWithProperty(envelope, "voices");
        if (!TryGetProperty(data, "voices", out JsonElement voicesElement)
            || voicesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var voices = new List<VoicemodVoice>();
        foreach (JsonElement item in voicesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            string? id = GetString(item, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            voices.Add(new VoicemodVoice(
                id,
                GetString(item, "friendlyName") ?? id,
                GetBoolean(item, "enabled") ?? false,
                GetBoolean(item, "isCustom") ?? false,
                GetBoolean(item, "favorited") ?? false,
                GetBoolean(item, "isNew") ?? false,
                GetBoolean(item, "isPurchased") ?? false,
                GetString(item, "bitmapChecksum") ?? string.Empty));
        }

        string currentVoiceId = GetString(data, "currentVoice")
            ?? ReadVoiceId(envelope)
            ?? "nofx";
        return (voices, currentVoiceId);
    }

    public static IReadOnlyList<VoicemodSoundboard>? ReadSoundboards(VoicemodEnvelope envelope)
    {
        JsonElement data = SelectDataWithProperty(envelope, "soundboards");
        if (!TryGetProperty(data, "soundboards", out JsonElement soundboardsElement)
            || soundboardsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var soundboards = new List<VoicemodSoundboard>();
        foreach (JsonElement boardElement in soundboardsElement.EnumerateArray())
        {
            if (boardElement.ValueKind != JsonValueKind.Object)
                continue;

            string? boardId = GetString(boardElement, "id");
            if (string.IsNullOrWhiteSpace(boardId))
                continue;

            var sounds = new List<VoicemodSound>();
            if (TryGetProperty(boardElement, "sounds", out JsonElement soundsElement)
                && soundsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement soundElement in soundsElement.EnumerateArray())
                {
                    if (soundElement.ValueKind != JsonValueKind.Object)
                        continue;

                    string? soundId = GetString(soundElement, "id")
                        ?? GetString(soundElement, "FileName")
                        ?? GetString(soundElement, "fileName");
                    if (string.IsNullOrWhiteSpace(soundId))
                        continue;

                    sounds.Add(new VoicemodSound(
                        soundId,
                        GetString(soundElement, "name")
                            ?? GetString(soundElement, "Name")
                            ?? soundId,
                        GetBoolean(soundElement, "enabled") ?? true,
                        GetBoolean(soundElement, "isCustom") ?? false,
                        GetString(soundElement, "playbackMode")
                            ?? GetString(soundElement, "type")
                            ?? GetString(soundElement, "Type")
                            ?? string.Empty,
                        GetBoolean(soundElement, "loop") ?? false,
                        GetBoolean(soundElement, "muteOtherSounds") ?? false,
                        GetBoolean(soundElement, "muteVoice") ?? false,
                        GetBoolean(soundElement, "stopOtherSounds") ?? false,
                        GetBoolean(soundElement, "showProLogo") ?? false,
                        GetString(soundElement, "bitmapChecksum") ?? string.Empty));
                }
            }

            soundboards.Add(new VoicemodSoundboard(
                boardId,
                GetString(boardElement, "name") ?? boardId,
                GetBoolean(boardElement, "enabled") ?? true,
                GetBoolean(boardElement, "isCustom") ?? false,
                GetBoolean(boardElement, "showProLogo") ?? false,
                sounds));
        }

        return soundboards;
    }

    public static (string VoiceId, IReadOnlyList<VoicemodVoiceParameter> Parameters)? ReadCurrentVoice(
        VoicemodEnvelope envelope)
    {
        string voiceId = ReadVoiceId(envelope) ?? "nofx";
        JsonElement data = SelectDataWithProperty(envelope, "parameters");
        if (!TryGetProperty(data, "parameters", out JsonElement parametersElement))
            return (voiceId, Array.Empty<VoicemodVoiceParameter>());

        var parameters = new List<VoicemodVoiceParameter>();
        if (parametersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in parametersElement.EnumerateObject())
                AddParameter(parameters, property.Name, property.Value);
        }
        else if (parametersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in parametersElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (JsonProperty property in item.EnumerateObject())
                    AddParameter(parameters, property.Name, property.Value);
            }
        }

        return (voiceId, parameters);
    }

    public static (string VoiceId, string ParameterName, double Value)? ReadParameterUpdate(
        VoicemodEnvelope envelope)
    {
        JsonElement data = SelectDataWithProperty(envelope, "parameter");
        if (!TryGetProperty(data, "parameter", out JsonElement parameter)
            || parameter.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? name = GetString(parameter, "name") ?? GetString(parameter, "Name");
        double? value = GetDouble(parameter, "value") ?? GetDouble(parameter, "Value");
        if (string.IsNullOrWhiteSpace(name) || value == null)
            return null;

        return (
            GetString(data, "voiceId") ?? GetString(data, "voiceID") ?? "nofx",
            name,
            value.Value);
    }

    public static bool? ReadBooleanValue(VoicemodEnvelope envelope)
    {
        JsonElement data = SelectDataWithProperty(envelope, "value");
        return GetBoolean(data, "value");
    }

    public static string? ReadVoiceId(VoicemodEnvelope envelope)
    {
        foreach (JsonElement data in EnumerateData(envelope))
        {
            string? value = GetString(data, "voiceID") ?? GetString(data, "voiceId");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    public static string? ReadActiveSoundboardId(VoicemodEnvelope envelope)
    {
        foreach (JsonElement data in EnumerateData(envelope))
        {
            string? value = GetString(data, "profileId");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    public static string? ReadLicenseType(VoicemodEnvelope envelope)
    {
        foreach (JsonElement data in EnumerateData(envelope))
        {
            string? value = GetString(data, "licenseType");
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static void AddParameter(
        ICollection<VoicemodVoiceParameter> parameters,
        string key,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;

        double minimum = GetDouble(value, "minValue") ?? 0d;
        double maximum = GetDouble(value, "maxValue") ?? minimum;
        double current = GetDouble(value, "value") ?? minimum;

        parameters.Add(new VoicemodVoiceParameter(
            key,
            GetString(value, "name") ?? key,
            GetDouble(value, "default") ?? current,
            minimum,
            maximum,
            current,
            GetBoolean(value, "displayNormalized") ?? false,
            GetInt32(value, "typeController") ?? 0));
    }

    private static JsonElement SelectDataWithProperty(VoicemodEnvelope envelope, string propertyName)
    {
        foreach (JsonElement data in EnumerateData(envelope))
        {
            if (TryGetProperty(data, propertyName, out _))
                return data;
        }

        return default;
    }

    private static bool TryGetDataProperty(
        VoicemodEnvelope envelope,
        string propertyName,
        out JsonElement value)
    {
        JsonElement data = SelectDataWithProperty(envelope, propertyName);
        return TryGetProperty(data, propertyName, out value);
    }

    private static IEnumerable<JsonElement> EnumerateData(VoicemodEnvelope envelope)
    {
        if (envelope.ActionObject.ValueKind == JsonValueKind.Object)
            yield return envelope.ActionObject;
        if (envelope.Payload.ValueKind == JsonValueKind.Object)
            yield return envelope.Payload;
        if (envelope.Context.ValueKind == JsonValueKind.Object)
            yield return envelope.Context;
        if (envelope.Root.ValueKind == JsonValueKind.Object)
            yield return envelope.Root;
    }

    private static JsonElement CloneNormalizedProperty(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out JsonElement value))
            return default;

        if (value.ValueKind == JsonValueKind.String)
        {
            string? serialized = value.GetString();
            if (!string.IsNullOrWhiteSpace(serialized))
            {
                try
                {
                    using JsonDocument nested = JsonDocument.Parse(serialized);
                    return nested.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return value.Clone();
                }
            }
        }

        return value.Clone();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
            return null;

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            return numeric != 0;

        if (value.ValueKind == JsonValueKind.String
            && bool.TryParse(value.GetString(), out bool parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            return numeric;

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double numeric))
            return numeric;

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double parsed))
        {
            return parsed;
        }

        return null;
    }
}
