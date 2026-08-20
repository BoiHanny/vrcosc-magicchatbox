using System.Linq;
using System.Text.Json;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Services.Voicemod;
using Xunit;

namespace MagicChatbox.Tests.Services.Voicemod;

public sealed class VoicemodProtocolTests
{
    [Fact]
    public void RegistrationStatus_ParsesTheDocumentedResponse()
    {
        const string json = """
            {
              "action": "registerClient",
              "id": "request-1",
              "payload": {
                "status": {
                  "code": 200,
                  "description": "Authorized"
                }
              }
            }
            """;

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        var status = VoicemodProtocol.ReadRegistrationStatus(envelope!);

        Assert.NotNull(status);
        Assert.Equal(200, status!.Value.Code);
        Assert.Equal("Authorized", status.Value.Description);
    }

    [Fact]
    public void Envelope_NormalizesLegacyIdsAndStringifiedActionObjects()
    {
        const string json = """
            {
              "actionType": "getUserLicense",
              "actionID": "legacy-id",
              "actionObject": "{\"licenseType\":\"pro\"}"
            }
            """;

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        Assert.Equal("getUserLicense", envelope!.Action);
        Assert.Equal("legacy-id", envelope.ActionId);
        Assert.Equal("pro", VoicemodProtocol.ReadLicenseType(envelope));
    }

    [Fact]
    public void Voices_AreDynamicAndKeepAvailabilityMetadata()
    {
        const string json = """
            {
              "actionType": "getVoices",
              "actionObject": {
                "currentVoice": "robot",
                "voices": [
                  {
                    "id": "robot",
                    "friendlyName": "Robot",
                    "enabled": true,
                    "isCustom": false,
                    "favorited": true,
                    "isNew": false,
                    "isPurchased": true,
                    "bitmapChecksum": "abc"
                  },
                  {
                    "id": "pro-only",
                    "friendlyName": "Pro only",
                    "enabled": false
                  }
                ]
              }
            }
            """;

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        var result = VoicemodProtocol.ReadVoices(envelope!);

        Assert.NotNull(result);
        Assert.Equal("robot", result!.Value.CurrentVoiceId);
        Assert.Equal(2, result.Value.Voices.Count);
        Assert.True(result.Value.Voices[0].Favorited);
        Assert.False(result.Value.Voices[1].Enabled);
    }

    [Fact]
    public void Soundboards_ParseCurrentAndLegacySoundPropertyNames()
    {
        const string json = """
            {
              "actionType": "getAllSoundboard",
              "actionObject": {
                "soundboards": [
                  {
                    "id": "board-1",
                    "name": "Favorites",
                    "enabled": true,
                    "sounds": [
                      {
                        "id": "sound-1",
                        "name": "Airhorn",
                        "enabled": true,
                        "playbackMode": "PlayRestart",
                        "muteVoice": true
                      },
                      {
                        "FileName": "legacy-sound",
                        "Name": "Legacy",
                        "Type": "PlayStop",
                        "IsCore": true
                      }
                    ]
                  }
                ]
              }
            }
            """;

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        var boards = VoicemodProtocol.ReadSoundboards(envelope!);

        VoicemodSoundboard board = Assert.Single(boards!);
        Assert.Equal(2, board.Sounds.Count);
        Assert.Equal("sound-1", board.Sounds[0].Id);
        Assert.True(board.Sounds[0].MuteVoice);
        Assert.Equal("legacy-sound", board.Sounds[1].Id);
        Assert.Equal("PlayStop", board.Sounds[1].PlaybackMode);
    }

    [Fact]
    public void CurrentVoice_ParsesTheDynamicParameterDictionary()
    {
        const string json = """
            {
              "actionType": "getCurrentVoice",
              "actionObject": {
                "voiceID": "robot",
                "parameters": {
                  "mix": {
                    "name": "Mix",
                    "default": 0.5,
                    "minValue": 0,
                    "maxValue": 1,
                    "displayNormalized": true,
                    "typeController": 0,
                    "value": 0.75
                  }
                }
              }
            }
            """;

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        var current = VoicemodProtocol.ReadCurrentVoice(envelope!);

        Assert.NotNull(current);
        Assert.Equal("robot", current!.Value.VoiceId);
        VoicemodVoiceParameter parameter = Assert.Single(current.Value.Parameters);
        Assert.Equal("mix", parameter.Key);
        Assert.Equal("Mix", parameter.Name);
        Assert.Equal(0.75, parameter.Value);
        Assert.Equal(0, parameter.Minimum);
        Assert.Equal(1, parameter.Maximum);
    }

    [Fact]
    public void OutboundMessages_AlwaysHaveTheRequiredShape()
    {
        string json = VoicemodProtocol.CreateMessage(
            "loadVoice",
            new { voiceID = "robot" },
            "request-42");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("request-42", root.GetProperty("id").GetString());
        Assert.Equal("loadVoice", root.GetProperty("action").GetString());
        Assert.Equal("robot", root.GetProperty("payload").GetProperty("voiceID").GetString());
        Assert.Equal(3, root.EnumerateObject().Count());
    }
}
