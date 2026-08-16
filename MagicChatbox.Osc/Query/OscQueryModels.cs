using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagicChatbox.Osc.Query;

/// <summary>OSCQuery's ACCESS field: which directions a node supports.</summary>
/// <remarks>
/// The numbering is the protocol's, not ours. <c>Write</c> means "the peer may write to this node",
/// which is why <c>/avatar/change</c> is advertised as <see cref="Write"/> — VRChat is the one writing.
/// </remarks>
public enum OscQueryAccess
{
    /// <summary>A container. Has children, carries no value of its own.</summary>
    NoValue = 0,

    /// <summary>Readable by a peer.</summary>
    Read = 1,

    /// <summary>Writable by a peer.</summary>
    Write = 2,

    /// <summary>Both.</summary>
    ReadWrite = 3,
}

/// <summary>
/// One node in an OSCQuery tree — ours or VRChat's.
/// </summary>
/// <remarks>
/// <para>
/// v2 modelled this as four types (<c>RootNode</c>, <c>Node&lt;T&gt;</c>, <c>AvatarContents</c>,
/// <c>OscParameterNode</c>) with the <c>/avatar</c> shape baked into the type system. That made the
/// advertised tree convenient and the *consumed* tree — which is a 400-node arbitrary shape produced by
/// somebody else's serializer — awkward, and it is the reason v2's D15 bug had somewhere to hide: the
/// only way to change one branch was to mutate a nested object in place.
/// </para>
/// <para>
/// One recursive node type serializes to the same JSON, describes both trees, and makes "replace a
/// branch" mean "build a new tree", which is the fix D15 asks for.
/// </para>
/// </remarks>
public sealed class OscQueryNode
{
    /// <summary>Human-readable label. Omitted from the wire when null.</summary>
    [JsonPropertyOrder(-4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("DESCRIPTION")]
    public string? Description { get; set; }

    /// <summary>The absolute OSC address of this node.</summary>
    [JsonPropertyOrder(-3)]
    [JsonPropertyName("FULL_PATH")]
    public string FullPath { get; set; } = "/";

    /// <summary>See <see cref="OscQueryAccess"/>. Serialized as the protocol's integer.</summary>
    [JsonPropertyOrder(-2)]
    [JsonPropertyName("ACCESS")]
    public int Access { get; set; }

    /// <summary>Child nodes by name. Omitted from the wire when null.</summary>
    [JsonPropertyOrder(-1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("CONTENTS")]
    public Dictionary<string, OscQueryNode>? Contents { get; set; }

    /// <summary>The OSC type-tag string for this node, e.g. <c>"s"</c> or <c>"f"</c>.</summary>
    /// <remarks>
    /// This is a <i>declaration</i>, used to enumerate parameters before the first message arrives
    /// (P1). It never decodes anything: the wire's own type tag does that, on every message.
    /// </remarks>
    [JsonPropertyOrder(0)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("TYPE")]
    public string? OscType { get; set; }

    /// <summary>The node's current value, as a single-element array. Omitted when null.</summary>
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("VALUE")]
    public object[]? Value { get; set; }
}

/// <summary>The <c>?HOST_INFO</c> document: where to send OSC, and what we support.</summary>
/// <remarks>
/// This is the half of the handshake that tells VRChat our <i>UDP</i> port. Our mDNS advertisement gets
/// VRChat to ask; this answer is what it sends to.
/// </remarks>
public sealed class OscQueryHostInfo
{
    /// <summary>The service instance name, matching what we advertise over mDNS.</summary>
    [JsonPropertyName("NAME")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The address our OSC receiver is bound to.</summary>
    [JsonPropertyName("OSC_IP")]
    [JsonConverter(typeof(JsonIPAddressConverter))]
    public IPAddress OscIp { get; set; } = IPAddress.Loopback;

    /// <summary>The UDP port our OSC receiver is bound to. Negotiated, never assumed.</summary>
    [JsonPropertyName("OSC_PORT")]
    public int OscPort { get; set; }

    /// <summary>Always UDP for VRChat.</summary>
    [JsonPropertyName("OSC_TRANSPORT")]
    [JsonConverter(typeof(JsonStringEnumConverter<OscTransport>))]
    public OscTransport OscTransport { get; set; } = OscTransport.UDP;

    /// <summary>Which optional OSCQuery features we implement.</summary>
    [JsonPropertyName("EXTENSIONS")]
    public OscQueryExtensions Extensions { get; set; } = new();
}

/// <summary>OSCQuery's transport enumeration. Serialized by name, so the casing is the wire's.</summary>
public enum OscTransport
{
    /// <summary>OSC over TCP. VRChat does not use it; present because the field is an enumeration.</summary>
    TCP,

    /// <summary>OSC over UDP. What VRChat speaks.</summary>
    UDP,
}

/// <summary>The OSCQuery extension flags advertised in <c>?HOST_INFO</c>.</summary>
public sealed class OscQueryExtensions
{
    /// <summary>ACCESS fields are served.</summary>
    [JsonPropertyName("ACCESS")]
    public bool Access { get; set; } = true;

    /// <summary>CLIPMODE fields are served.</summary>
    [JsonPropertyName("CLIPMODE")]
    public bool ClipMode { get; set; } = true;

    /// <summary>RANGE fields are served.</summary>
    [JsonPropertyName("RANGE")]
    public bool Range { get; set; } = true;

    /// <summary>TYPE fields are served.</summary>
    [JsonPropertyName("TYPE")]
    public bool Type { get; set; } = true;

    /// <summary>VALUE fields are served.</summary>
    [JsonPropertyName("VALUE")]
    public bool Value { get; set; } = true;
}

/// <summary>Reads and writes <see cref="IPAddress"/> as a plain string, which is what the protocol uses.</summary>
internal sealed class JsonIPAddressConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string for {nameof(IPAddress)}, got {reader.TokenType}.");
        }

        var text = reader.GetString();
        return IPAddress.TryParse(text, out var address)
            ? address
            : throw new JsonException($"'{text}' is not an IP address.");
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// The one serializer configuration for both documents we serve and every document we consume.
/// </summary>
/// <remarks>
/// Source-generated: the HTTP handler serializes on an arbitrary thread while a live VRChat session is
/// running, and reflection-based serialization there is both slower and a trimming hazard.
/// <para>
/// <c>WriteIndented</c> matches v2 and VRChat's own output, and it is what the committed golden file
/// for the advertised tree is compared against. The golden file is the wire contract (§12.3): if this
/// option changes, that is a protocol change and the golden file changes with it, under review.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(OscQueryNode))]
[JsonSerializable(typeof(OscQueryHostInfo))]
[JsonSerializable(typeof(OscQueryExtensions))]
[JsonSerializable(typeof(JsonElement))]
public partial class OscQueryJson : JsonSerializerContext
{
    /// <summary>Serializes a node tree exactly as it is served.</summary>
    public static string Serialize(OscQueryNode node) => JsonSerializer.Serialize(node, Default.OscQueryNode);

    /// <summary>Serializes a host-info document exactly as it is served.</summary>
    public static string Serialize(OscQueryHostInfo hostInfo) => JsonSerializer.Serialize(hostInfo, Default.OscQueryHostInfo);

    /// <summary>Serializes a node tree to UTF-8, skipping the intermediate string on the serve path.</summary>
    public static byte[] SerializeUtf8(OscQueryNode node) => JsonSerializer.SerializeToUtf8Bytes(node, Default.OscQueryNode);

    /// <summary>Serializes a host-info document to UTF-8.</summary>
    public static byte[] SerializeUtf8(OscQueryHostInfo hostInfo) => JsonSerializer.SerializeToUtf8Bytes(hostInfo, Default.OscQueryHostInfo);

    /// <summary>Parses a peer's node tree. Returns null rather than throwing on malformed input.</summary>
    public static OscQueryNode? TryParseNode(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize(utf8, Default.OscQueryNode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses a peer's <c>?HOST_INFO</c>. Returns null rather than throwing on malformed input.</summary>
    public static OscQueryHostInfo? TryParseHostInfo(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize(utf8, Default.OscQueryHostInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses a peer's node tree from text.</summary>
    public static OscQueryNode? TryParseNode(string json) => TryParseNode(Encoding.UTF8.GetBytes(json));

    /// <summary>Parses a peer's <c>?HOST_INFO</c> from text.</summary>
    public static OscQueryHostInfo? TryParseHostInfo(string json) =>
        TryParseHostInfo(Encoding.UTF8.GetBytes(json));
}
