using System.Net;
using System.Text.Json;

namespace MagicChatbox.Osc.Query;

/// <summary>One enumerated node from a peer's tree: a path, its declared type, and its current value.</summary>
/// <param name="Path">The full OSC address, e.g. <c>/avatar/parameters/VRCFaceBlendH</c>.</param>
/// <param name="OscType">The peer's declared type tag, or null when it declared none.</param>
/// <param name="Value">The seed value, normalized to bool / int / float / string.</param>
/// <param name="Access">The peer's ACCESS field.</param>
public readonly record struct OscQueryEntry(string Path, string? OscType, object? Value, OscQueryAccess Access);

/// <summary>What a peer's tree told us: which avatar is loaded, and what parameters it has.</summary>
/// <param name="AvatarId">The value of <c>/avatar/change</c>, empty when the peer did not report one.</param>
/// <param name="Parameters">Every leaf under <c>/avatar/parameters</c>, flattened by full path.</param>
/// <param name="AvatarLeaves">
/// The non-parameter leaves directly under <c>/avatar</c>. A live client advertises exactly five —
/// <c>change</c>, <c>eyeheight</c>, <c>eyeheightmin</c>, <c>eyeheightmax</c> and
/// <c>eyeheightscalingallowed</c> — and they are kept separate from <paramref name="Parameters"/> because
/// they describe the session and the world rather than the worn avatar, and so outlive an avatar change.
/// Defaults to empty so a caller that only wants parameters need not name it.
/// </param>
public readonly record struct OscQuerySnapshot(
    string AvatarId,
    IReadOnlyList<OscQueryEntry> Parameters,
    IReadOnlyList<OscQueryEntry>? AvatarLeaves = null)
{
    /// <summary>The non-parameter <c>/avatar</c> leaves, never null.</summary>
    public IReadOnlyList<OscQueryEntry> AvatarLeaves { get; init; } = AvatarLeaves ?? [];
}

/// <summary>
/// Reads the other side of the handshake: a peer's <c>?HOST_INFO</c> and its node tree.
/// </summary>
/// <remarks>
/// <para>
/// This is the <i>consumed</i> tree, and §12.4 is emphatic that it is not the same object as the one we
/// advertise: dynamic where ours is fixed, 40-400 nodes where ours is two, rebuilt on every avatar
/// change where ours changes only when we change the protocol. Nothing here writes to the advertised
/// tree, and nothing in the advertised tree is derived from this.
/// </para>
/// <para>
/// Per P1, what comes back is used to <b>enumerate</b> — to know which parameters exist and seed a value
/// before the first message arrives — and never to decode. Decoding reads the wire's own type tag, on
/// every message, always.
/// </para>
/// </remarks>
public sealed class OscQueryClient
{
    /// <summary>
    /// A ceiling on a peer's response. An avatar with 400 parameters serves well under 200 KB; two
    /// megabytes is generous and still bounds what a hostile or broken peer on the LAN can make us hold.
    /// </summary>
    public const int MaxResponseBytes = 2 * 1024 * 1024;

    private readonly HttpClient _http;

    /// <param name="http">Injected so tests can point it at the embedded server, and so the timeout is the caller's decision.</param>
    public OscQueryClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>Creates a client with the short timeout a loopback handshake deserves.</summary>
    public static OscQueryClient CreateDefault() => new(new HttpClient { Timeout = TimeSpan.FromSeconds(5) });

    /// <summary>Fetches a peer's <c>?HOST_INFO</c>. Null when it is unreachable or unparseable.</summary>
    /// <remarks>
    /// The interesting field is <c>OSC_PORT</c>: that is where the peer wants us to <i>send</i>, and it is
    /// the whole reason we do not hard-code 9000.
    /// </remarks>
    public async Task<OscQueryHostInfo?> TryFetchHostInfoAsync(IPEndPoint httpEndpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpEndpoint);

        var utf8 = await TryFetchAsync($"http://{httpEndpoint}/?HOST_INFO", cancellationToken).ConfigureAwait(false);
        return utf8 is null ? null : OscQueryJson.TryParseHostInfo(utf8);
    }

    /// <summary>Fetches and flattens a peer's node tree. Null when it is unreachable or unparseable.</summary>
    public async Task<OscQuerySnapshot?> TryFetchSnapshotAsync(IPEndPoint httpEndpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpEndpoint);

        var utf8 = await TryFetchAsync($"http://{httpEndpoint}/", cancellationToken).ConfigureAwait(false);
        if (utf8 is null)
        {
            return null;
        }

        var root = OscQueryJson.TryParseNode(utf8);
        return root is null ? null : Flatten(root);
    }

    /// <summary>Flattens a parsed tree into the avatar id and a flat parameter list.</summary>
    /// <remarks>Separated from the fetch so the shape rules are testable against a literal document.</remarks>
    public static OscQuerySnapshot Flatten(OscQueryNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var avatar = Child(root, "avatar");
        var avatarId = NormalizeValue(Child(avatar, "change")?.Value?.FirstOrDefault()) as string ?? string.Empty;

        var parameters = new List<OscQueryEntry>();
        var parametersNode = Child(avatar, "parameters");
        if (parametersNode?.Contents is { } contents)
        {
            FlattenInto(contents, parameters);
        }

        // The fixed leaves, collected separately. "parameters" is excluded by name rather than by
        // structure because it is the one child of /avatar that is a container.
        var leaves = new List<OscQueryEntry>();
        if (avatar?.Contents is { } avatarChildren)
        {
            foreach (var (name, node) in avatarChildren)
            {
                if (string.Equals(name, "parameters", StringComparison.Ordinal) || node.OscType is null)
                {
                    continue;
                }

                leaves.Add(new OscQueryEntry(
                    NormalizePath(node.FullPath, name),
                    node.OscType,
                    NormalizeValue(node.Value?.FirstOrDefault()),
                    (OscQueryAccess)node.Access));
            }
        }

        return new OscQuerySnapshot(avatarId, parameters, leaves);
    }

    private static void FlattenInto(Dictionary<string, OscQueryNode> nodes, List<OscQueryEntry> output)
    {
        foreach (var (name, node) in nodes)
        {
            // A container recurses; a leaf is a parameter. VRChat nests parameters under group nodes, so
            // treating "has CONTENTS" as "is not a leaf" is what produces the flat address list.
            if (node.Contents is { Count: > 0 } children)
            {
                FlattenInto(children, output);
                continue;
            }

            output.Add(new OscQueryEntry(
                NormalizePath(node.FullPath, name),
                node.OscType,
                NormalizeValue(node.Value?.FirstOrDefault()),
                (OscQueryAccess)node.Access));
        }
    }

    private static OscQueryNode? Child(OscQueryNode? node, string name) =>
        node?.Contents is { } contents && contents.TryGetValue(name, out var child) ? child : null;

    private static string NormalizePath(string? fullPath, string fallbackName)
    {
        var candidate = string.IsNullOrWhiteSpace(fullPath) ? fallbackName : fullPath;
        return candidate.StartsWith('/') ? candidate : "/" + candidate;
    }

    /// <summary>
    /// Turns a deserialized JSON value into one of the four types VRChat actually uses.
    /// </summary>
    /// <remarks>
    /// Integer before floating point, because JSON has one number type and VRChat has two: reading
    /// <c>1</c> as a float would declare an Int parameter to be a Float, and the mismatch would then show
    /// up as a coercion counter rather than as the schema bug it is.
    /// </remarks>
    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetDouble(out var d) => (float)d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private async Task<byte[]?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var bounded = new MemoryStream();
            var buffer = new byte[8192];

            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (bounded.Length + read > MaxResponseBytes)
                {
                    // A peer streaming past the cap without declaring a Content-Length. Truncated JSON is
                    // not a document, so there is nothing to salvage; the caller's backoff owns the retry.
                    return null;
                }

                bounded.Write(buffer, 0, read);
            }

            return bounded.ToArray();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation. Ours is a real cancellation and
            // propagates; a peer that went away is just unreachable.
            return null;
        }
    }
}
