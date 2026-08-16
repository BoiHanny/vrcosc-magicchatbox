namespace MagicChatbox.Osc.Query;

/// <summary>
/// The node tree we serve at <c>/</c>. Fixed, hand-written, and versioned as a wire contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not, and must never become, a projection of the descriptor registry (§12.3, §12.4).</b>
/// Mirroring an internal structure onto the wire would mean every descriptor added, renamed or re-ranked
/// silently changes what third-party OSC applications see, and the registry could never be refactored
/// without a compatibility review. Adding a node here is a deliberate protocol change and is reviewed as
/// one — the test suite byte-compares the serialized output against a committed golden file so that
/// changing it is impossible to do by accident.
/// </para>
/// <para>
/// <b>Why the concrete child under <c>/avatar</c> is the entire point.</b> VRCOSC's
/// <c>ConnectionManager.cs:154</c> records: "Register a single child node as VRChat sends everything for
/// some reason but doesn't if you only register the root". Per P2, the requirement to register
/// <i>something</i> under <c>/avatar</c> is confirmed by the official <c>vrchat-community/osc</c> wiki;
/// the narrower claim that the bare root alone fails rests only on that comment and is not attested by
/// any VRChat-authored text. We register the child regardless: it costs one node, and the failure mode
/// if the comment is right and we omit it is <i>total silence</i> — VRChat sends nothing, and there is no
/// error anywhere to diagnose it from.
/// </para>
/// <para>
/// The advertised tree deliberately does <b>not</b> include <c>/avatar/parameters</c>. v2 advertised an
/// empty parameters container and then mutated it, which is what made D15 possible. We never advertise
/// parameters we do not have; enumerating VRChat's parameters is the <i>consumed</i> tree's job, and the
/// two are different documents with different lifetimes (§12.4).
/// </para>
/// </remarks>
public static class OscQueryAdvertisedTree
{
    /// <summary>Builds a fresh copy of the advertised tree.</summary>
    /// <remarks>
    /// A new instance every call rather than a shared static: <see cref="OscQueryNode"/> is mutable, and
    /// a shared root is one careless caller away from being edited underneath the HTTP handler. The
    /// server takes ownership of the instance it is given and never mutates it after publishing.
    /// </remarks>
    public static OscQueryNode Build() => new()
    {
        FullPath = "/",
        Access = (int)OscQueryAccess.NoValue,
        Contents = new Dictionary<string, OscQueryNode>(StringComparer.Ordinal)
        {
            ["avatar"] = new OscQueryNode
            {
                FullPath = "/avatar",
                Access = (int)OscQueryAccess.NoValue,
                Contents = new Dictionary<string, OscQueryNode>(StringComparer.Ordinal)
                {
                    // The one node that makes VRChat talk to us at all. See the type remarks.
                    ["change"] = new OscQueryNode
                    {
                        FullPath = "/avatar/change",
                        Access = (int)OscQueryAccess.Write,
                        OscType = "s",
                        Value = [string.Empty],
                    },
                },
            },
        },
    };
}
