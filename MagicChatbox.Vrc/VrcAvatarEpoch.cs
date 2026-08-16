namespace MagicChatbox.Vrc;

/// <summary>
/// An avatar swap, and everything it invalidates.
/// </summary>
/// <remarks>
/// P10 — <c>/avatar/change</c> carries <b>only</b> the avatar id. The parameter set, the eyeheight
/// nodes and every declared type belong to the avatar that just left and none of them are confirmed for
/// the one that arrived. So this is not a notification that a string changed; it is notification that
/// everything under <see cref="VrcAvatarKeys.ParameterKeyPrefix"/> is now unconfirmed and must be
/// removed and re-discovered.
/// <para>
/// v2 has a verified bug here: nothing produced a removal, so <c>avatar.*</c> keys from the previous
/// avatar stayed readable and renderable after a swap. The chatbox went on cheerfully reporting a
/// parameter that no longer existed.
/// </para>
/// </remarks>
/// <param name="AvatarId">The new avatar's id, exactly as it arrived on the wire.</param>
/// <param name="Epoch">The epoch this swap produced. Every wait keyed to a lower one is void.</param>
public readonly record struct VrcAvatarInvalidated(string AvatarId, long Epoch);

/// <summary>The kernel keys VRChat's own avatar addresses project onto.</summary>
/// <remarks>
/// Constants rather than literals scattered across ingress and egress, because the eviction prefix, the
/// projection prefix and whatever the composer reads must be the same string or a swap evicts nothing.
/// </remarks>
public static class VrcAvatarKeys
{
    /// <summary>
    /// What <c>/avatar/parameters/&lt;Name&gt;</c> becomes. <b>Also the eviction prefix</b> on an avatar
    /// change — every key starting with this belongs to the avatar that just left.
    /// </summary>
    public const string ParameterKeyPrefix = "avatar.param.";

    /// <summary>What <c>/avatar/change</c> becomes.</summary>
    public const string AvatarId = "avatar.id";

    /// <summary>What <c>/avatar/eyeheight</c> becomes.</summary>
    public const string EyeHeight = "avatar.eyeheight";

    /// <summary>What <c>/avatar/eyeheightmin</c> becomes.</summary>
    public const string EyeHeightMin = "avatar.eyeheight_min";

    /// <summary>What <c>/avatar/eyeheightmax</c> becomes.</summary>
    public const string EyeHeightMax = "avatar.eyeheight_max";

    /// <summary>
    /// What <c>/avatar/eyeheightscalingallowed</c> becomes — whether this world permits scaling at all.
    /// </summary>
    /// <remarks>
    /// The fifth fixed leaf, and the one that makes a failed <see cref="IVrcEgress.SetEyeHeightAsync"/>
    /// legible. A world's Udon script can set this false, after which VRChat <i>ignores</i> writes to
    /// <c>/avatar/eyeheight</c> — no echo, no error, nothing. Without this key a caller sees only "the
    /// value never came back", which is indistinguishable from VRChat having gone away.
    /// </remarks>
    public const string EyeHeightScalingAllowed = "avatar.eyeheight_scaling_allowed";

    /// <summary>The current avatar's human-readable name. Not available over OSC or OSCQuery.</summary>
    /// <remarks>
    /// The single fact VRChat's per-avatar config file has that the live OSCQuery tree does not, which is
    /// the whole reason that file is read at all. See <c>AvatarConfigReader</c>.
    /// </remarks>
    public const string AvatarName = "avatar.name";

    /// <summary>The config file's content hash, usable as a cheap "did this avatar change" test.</summary>
    public const string AvatarHash = "avatar.hash";

    /// <summary>
    /// The six components of one tracked pose, in the order VRChat sends them.
    /// </summary>
    /// <remarks>
    /// <c>,ffffff</c> — three metres of position, then three degrees of Euler rotation. Six cells rather
    /// than one, because <see cref="SignalValue"/> is a closed scalar union and widening it to carry
    /// vectors would put a variable-length case on every comparison in the store. Six floats is also
    /// what a consumer actually wants: "how high is my head" is one key, not a component of one.
    /// </remarks>
    public static readonly string[] PoseComponents =
        ["position.x", "position.y", "position.z", "rotation.x", "rotation.y", "rotation.z"];

    /// <summary>
    /// Where VRChat's read-only tracking poses land: <c>input.vr.head.position.y</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>input</c> namespace, not <c>avatar</c>, and that is a substantive choice. These are
    /// hardware facts — where the headset and the wrists are — and they stay true across an avatar
    /// change. Filing them under <c>avatar.</c> would mean the swap eviction wiped them, and a headset
    /// does not stop existing because you changed clothes. <see cref="SignalNamespace.Input"/>'s own
    /// documentation says "local input devices we read", which is what these are; they merely arrive by
    /// way of VRChat rather than from the driver.
    /// </para>
    /// <para>
    /// These addresses are in VRChat's OSCQuery tree and <b>absent from its documentation tables</b> —
    /// the wiki lists them only in its errata. That is why they were missed until an explicit read of
    /// the client's advertised tree.
    /// </para>
    /// </remarks>
    public static string TrackingKey(string device, string component) => $"input.vr.{device}.{component}";

    /// <summary>The devices VRChat reports a pose for.</summary>
    public static readonly string[] TrackedDevices = ["head", "leftwrist", "rightwrist"];

    /// <summary>
    /// The session key one of VRChat's fixed non-parameter <c>/avatar</c> addresses projects onto, or null.
    /// </summary>
    /// <remarks>
    /// <c>/avatar/change</c> is deliberately absent. It is text, and text is refused on the observe and
    /// seed paths outright (D7) — the avatar id reaches the store through
    /// <see cref="VrcAvatarIngress"/>'s change projection instead, which is also the only place allowed to
    /// advance the epoch.
    /// </remarks>
    public static string? TryFixedKeyFor(string? address) => address switch
    {
        "/avatar/eyeheight" => EyeHeight,
        "/avatar/eyeheightmin" => EyeHeightMin,
        "/avatar/eyeheightmax" => EyeHeightMax,
        "/avatar/eyeheightscalingallowed" => EyeHeightScalingAllowed,
        _ => null,
    };
}

/// <summary>
/// Which avatar is loaded, as a monotonically increasing number that invalidates everything older.
/// </summary>
/// <remarks>
/// <para>
/// Ported from v2's <c>Features/Transport/Avatar/AvatarEpochService.cs</c>, including the detail that
/// makes it useful: <see cref="AdvanceToAvatar"/> is <b>idempotent for the same id</b>. VRChat re-sends
/// <c>/avatar/change</c> on world loads and on reconnects, and treating each of those as a swap would
/// evict a live parameter set and cancel in-flight writes for no reason.
/// </para>
/// <para>
/// The number exists so that a <i>comparison</i> can answer "is this still about the avatar I was
/// talking about?" — which is the question echo correlation (§12.9, P7) cannot answer any other way,
/// because OSC carries no sender identity, no message id and no correlation field.
/// </para>
/// </remarks>
public sealed class VrcAvatarEpoch
{
    private readonly Lock _gate = new();
    private string _avatarId = string.Empty;
    private long _epoch;

    /// <summary>
    /// Raised after the epoch advances, off the lock, on whichever thread delivered the change — the
    /// OSC receive loop in production.
    /// </summary>
    /// <remarks>
    /// Handlers must return promptly: they run inline on the socket loop. A handler that throws is not
    /// caught here; the receive loop's decoder counts it and keeps receiving.
    /// </remarks>
    public event Action<VrcAvatarInvalidated>? Invalidated;

    /// <summary>The current epoch. Starts at 0 and only ever increases.</summary>
    /// <remarks>
    /// Read without the gate, because ingress reads it once per message at face-tracking rates and a
    /// swap is a once-per-minute event. <see cref="AdvanceToAvatar"/> still takes the gate: the
    /// compare-then-increment against the avatar id is what has to be atomic, not the read.
    /// </remarks>
    public long Current => Interlocked.Read(ref _epoch);

    /// <summary>The avatar id currently loaded, empty before the first <c>/avatar/change</c>.</summary>
    public string CurrentAvatarId
    {
        get
        {
            lock (_gate)
            {
                return _avatarId;
            }
        }
    }

    /// <summary>Reads the epoch to compare against later. Named for what the caller is doing with it.</summary>
    public long Capture() => Current;

    /// <summary>True when <paramref name="epoch"/> still refers to the avatar that is loaded now.</summary>
    public bool IsCurrent(long epoch) => Current == epoch;

    /// <summary>
    /// Records the avatar named by <c>/avatar/change</c>, advancing the epoch only if it is a different
    /// one.
    /// </summary>
    /// <returns>The epoch after the call — unchanged when the id was already current.</returns>
    public long AdvanceToAvatar(string avatarId)
    {
        ArgumentNullException.ThrowIfNull(avatarId);

        long epoch;
        lock (_gate)
        {
            if (string.Equals(_avatarId, avatarId, StringComparison.Ordinal))
            {
                return _epoch;
            }

            _avatarId = avatarId;
            epoch = Interlocked.Increment(ref _epoch);
        }

        // Outside the lock: a subscriber evicting a whole namespace, or completing every outstanding
        // echo wait, must not run with this object's gate held.
        Invalidated?.Invoke(new VrcAvatarInvalidated(avatarId, epoch));
        return epoch;
    }
}
