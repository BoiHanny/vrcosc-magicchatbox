using System;
using System.Collections.Generic;
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
    public void TheConnectGreeting_IsRealTrafficRatherThanAMalformedMessage()
    {
        // Captured verbatim from Voicemod 3.16.70. It carries no action of any kind, and it is the
        // first thing a fresh socket receives, so rejecting it loses the app version it hands over.
        const string json =
            """{"appVersion":"3.16.70","msg":"Pending authentication","server":"Kestrel"}""";

        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));

        Assert.Equal(VoicemodProtocol.ServerNoticeAction, envelope!.Action);
        Assert.Equal("3.16.70", envelope.AppVersion);
        Assert.Equal("Pending authentication", VoicemodProtocol.ReadServerNotice(envelope));
    }

    [Fact]
    public void AMessageWithNeitherAnActionNorANotice_IsStillRejected()
    {
        Assert.False(VoicemodProtocol.TryParseEnvelope(
            """{"appVersion":"3.16.70"}""",
            out _,
            out string? error));

        Assert.Equal("The message did not contain an action.", error);
    }

    [Fact]
    public void TheFlatSoundCatalog_IsReadFromListOfMemes()
    {
        VoicemodEnvelope envelope = Parse("""
            {
              "actionType": "getMemes",
              "actionObject": {
                "listOfMemes": [
                  { "Name": "Air horn", "FileName": "airhorn", "Type": "PlayRestart", "enabled": true },
                  { "Name": "Locked", "FileName": "locked", "Type": "PlayRestart", "enabled": false }
                ]
              }
            }
            """);

        IReadOnlyList<VoicemodSound>? sounds = VoicemodProtocol.ReadMemes(envelope);

        Assert.NotNull(sounds);
        Assert.Equal(2, sounds!.Count);
        Assert.Equal("airhorn", sounds[0].Id);
        Assert.Equal("Air horn", sounds[0].Name);
        Assert.True(sounds[0].Enabled);
        Assert.False(sounds[1].Enabled);
        Assert.Equal("Locked (unavailable)", sounds[1].DisplayName);
    }

    [Fact]
    public void ABitmapReply_IsAttributedToTheVoiceOrSoundThatAskedForIt()
    {
        var voice = VoicemodProtocol.ReadBitmap(Parse("""
            { "actionType": "getBitmap", "actionObject": { "voiceID": "robot", "result": "AAAA" } }
            """));
        Assert.NotNull(voice);
        Assert.Equal(("voice", "robot", "AAAA"), voice!.Value);

        var sound = VoicemodProtocol.ReadBitmap(Parse("""
            { "actionType": "getBitmap", "actionObject": { "memeId": "airhorn", "result": "BBBB" } }
            """));
        Assert.NotNull(sound);
        Assert.Equal(("sound", "airhorn", "BBBB"), sound!.Value);

        Assert.Null(VoicemodProtocol.ReadBitmap(Parse("""
            { "actionType": "getBitmap", "actionObject": { "voiceID": "robot" } }
            """)));
    }

    [Fact]
    public void ABitmapReplyCarriesNamedRenditions_NotASingleValue()
    {
        // Captured from Voicemod 3.16.70. The published reference shows result as one value; the
        // real server sends an object of named renditions, so reading it as a string found nothing
        // and every sound silently came back without artwork.
        var bitmap = VoicemodProtocol.ReadBitmap(Parse("""
            {
              "actionType": "getBitmap",
              "actionID": "4ba195d0-4ffa-4ae2-ac4e-e0446e37ae4f",
              "actionId": "4ba195d0-4ffa-4ae2-ac4e-e0446e37ae4f",
              "actionObject": {
                "memeId": "8ccaeee0-8873-4ff7-8e89-3a1d523f10b0",
                "result": { "default": "AAAA", "selected": "BBBB" }
              }
            }
            """));

        Assert.NotNull(bitmap);
        Assert.Equal(("sound", "8ccaeee0-8873-4ff7-8e89-3a1d523f10b0", "AAAA"), bitmap!.Value);
    }

    [Fact]
    public void ABitmapReplyFallsBackToWhicheverRenditionArrived()
    {
        var bitmap = VoicemodProtocol.ReadBitmap(Parse("""
            {
              "actionType": "getBitmap",
              "actionObject": { "voiceID": "robot", "result": { "somethingNew": "CCCC" } }
            }
            """));

        Assert.NotNull(bitmap);
        Assert.Equal(("voice", "robot", "CCCC"), bitmap!.Value);
    }

    [Theory]
    [InlineData(90, 90)]
    [InlineData(86400, 86400)]
    [InlineData(3600000, 3600)]
    public void RemainingTime_IsReadAsSecondsUntilTheNumberIsClearlyMilliseconds(
        double raw,
        double expectedSeconds)
    {
        // The docs give this field in seconds in one place and milliseconds in another, so the
        // reader has to pick by magnitude rather than trust either.
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            VoicemodProtocol.NormalizeRemainingTime(raw));
    }

    [Fact]
    public void TheSignedInAccount_IsReadFromEitherCasingOfTheField()
    {
        Assert.Equal("user-1", VoicemodProtocol.ReadUserId(Parse("""
            { "actionType": "getUser", "actionObject": { "userId": "user-1" } }
            """)));

        Assert.Equal("user-2", VoicemodProtocol.ReadUserId(Parse("""
            { "actionType": "getUser", "actionObject": { "userID": "user-2" } }
            """)));
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
    private static VoicemodEnvelope Parse(string json)
    {
        Assert.True(VoicemodProtocol.TryParseEnvelope(json, out VoicemodEnvelope? envelope, out _));
        return envelope!;
    }
}