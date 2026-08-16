using MagicChatbox.Osc;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>One fact VRChat just told us, already in the kernel's vocabulary.</summary>
/// <param name="Key">The projected key — <c>avatar.param.hue</c>, <c>avatar.id</c>, <c>avatar.eyeheight</c>.</param>
/// <param name="Value">The value, taken from the wire's own type tag (P1). Never from a schema.</param>
/// <param name="AvatarEpoch">
/// Which avatar this belongs to. A consumer that batches or queues observations needs this to drop the
/// ones that arrived just before a swap it has already processed.
/// </param>
public readonly record struct VrcObservation(SignalKey Key, SignalValue Value, long AvatarEpoch);

/// <summary>
/// Somewhere to hand observations to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface exists because <c>Vrc</c> cannot name the kernel.</b> §12.1 routes ingress into
/// <c>SignalStore.Observe</c> through an <c>OscKernelBridge</c> that lives in <c>Core</c> — the only
/// assembly permitted to reference both sides. So the seam is declared here, in the vocabulary both
/// halves already share, and <c>Core</c> supplies the implementation that forwards to the store's
/// source port.
/// </para>
/// <para>
/// <b>It is called once per message, not once per bundle.</b> §12.1 asks for one <c>Observe</c> call per
/// bundle so the span setup amortizes across 10–50 messages — but <c>IOscMessageSink</c> carries no
/// bundle delimiters, so that boundary is not visible from here at all. Batching therefore belongs to
/// the implementation of this interface (or to a buffering decorator in <c>Core</c>), and the
/// per-message shape is what this seam can honestly offer.
/// </para>
/// <para>
/// Implementations run inline on the OSC receive loop. Return promptly, do not block, and do not
/// allocate per call — the caller does neither.
/// </para>
/// </remarks>
public interface IVrcObservationSink
{
    /// <summary>Receives one observation. <c>in</c> so nothing is copied and nothing is boxed.</summary>
    void OnObservation(in VrcObservation observation);
}

/// <summary>Discards every observation. Useful when only the epoch and the counters matter.</summary>
public sealed class NullVrcObservationSink : IVrcObservationSink
{
    /// <summary>The shared instance; the type holds no state.</summary>
    public static readonly NullVrcObservationSink Instance = new();

    private NullVrcObservationSink() { }

    /// <inheritdoc />
    public void OnObservation(in VrcObservation observation) { }
}

/// <summary>
/// Live counters for the ingress projection. Machine-readable, because "8,000 messages were addresses we
/// do not model" belongs on the Sources screen, not in a log file nobody opens.
/// </summary>
/// <remarks>
/// Every counter here replaces a log line that would otherwise be written at face-tracking rates. An
/// address we ignore is not an error and must never cost a formatted string.
/// </remarks>
public sealed class VrcIngressCounters
{
    private long _parameters;
    private long _avatarChanges;
    private long _eyeHeights;
    private long _poses;
    private long _subsystems;
    private long _ignored;
    private long _unmappable;
    private long _malformed;

    /// <summary>Avatar parameters projected onto a key and published.</summary>
    public long Parameters => Volatile.Read(ref _parameters);

    /// <summary>Well-formed <c>/avatar/change</c> messages, whether or not the id was new.</summary>
    public long AvatarChanges => Volatile.Read(ref _avatarChanges);

    /// <summary>Eyeheight messages, including the min and max bounds.</summary>
    public long EyeHeights => Volatile.Read(ref _eyeHeights);

    /// <summary>Tracked poses accepted, counted per message rather than per component.</summary>
    public long Poses => Volatile.Read(ref _poses);

    /// <summary>Camera and dolly readings accepted.</summary>
    public long Subsystems => Volatile.Read(ref _subsystems);

    /// <summary>Addresses outside the families we model. Not an error — VRChat sends plenty.</summary>
    public long Ignored => Volatile.Read(ref _ignored);

    /// <summary>
    /// Addresses we recognised but could not project: a parameter name the key grammar rejects, or one
    /// long enough to overrun the key budget. A sustained non-zero count is a real avatar the user owns
    /// that we silently cannot see.
    /// </summary>
    public long Unmappable => Volatile.Read(ref _unmappable);

    /// <summary>Recognised addresses carrying no argument, or an argument of the wrong type.</summary>
    public long Malformed => Volatile.Read(ref _malformed);

    internal void CountParameter() => Interlocked.Increment(ref _parameters);

    internal void CountAvatarChange() => Interlocked.Increment(ref _avatarChanges);

    internal void CountEyeHeight() => Interlocked.Increment(ref _eyeHeights);

    internal void CountPose() => Interlocked.Increment(ref _poses);

    internal void CountSubsystem() => Interlocked.Increment(ref _subsystems);

    internal void CountIgnored() => Interlocked.Increment(ref _ignored);

    internal void CountUnmappable() => Interlocked.Increment(ref _unmappable);

    internal void CountMalformed() => Interlocked.Increment(ref _malformed);
}

/// <summary>
/// Turns VRChat's avatar addresses into kernel keys, on the receive loop, without allocating.
/// </summary>
/// <remarks>
/// <para>
/// The four families it recognises are §12.1's table: <c>/avatar/parameters/&lt;Name&gt;</c>,
/// <c>/avatar/change</c>, <c>/avatar/eyeheight</c> and the eyeheight bounds. Everything else is counted
/// and dropped — <c>/tracking/*</c> and <c>/input/*</c> are egress-only surfaces (D11) and modelling
/// them as cells would create keys that can be written and never confirmed.
/// </para>
/// <para>
/// <b>Nothing on this path allocates.</b> The address arrives as UTF-8 bytes pointing into the receive
/// buffer, the key is assembled in a stack buffer, and <see cref="SignalKey.InternUtf8"/> turns it into
/// an interned key with one dictionary probe. v2's one structural allocation here — a fresh address
/// <c>string</c> per uncached packet, <c>Features/Transport/OSC/Synapse.cs:191-200</c> — has no
/// equivalent. The single exception is <c>/avatar/change</c>, which materializes the avatar id string;
/// that happens once per avatar, not once per message.
/// </para>
/// <para>
/// <b>Per P1 the wire is authoritative for the value.</b> The type tag decides the
/// <see cref="SignalKind"/>; no schema is consulted, and none is needed to accept a value. Reconciling
/// against a descriptor is the store's job, downstream, with its own named rejection.
/// </para>
/// </remarks>
public sealed class VrcAvatarIngress : IOscMessageSink
{
    // Comfortably past VRChat's longest real parameter name and bounded so the stack buffer is a
    // constant. A name that overruns it is counted as unmappable rather than truncated: a truncated key
    // is a DIFFERENT key, and two parameters colliding on one cell is worse than one parameter missing.
    private const int MaxKeyUtf8Bytes = 256;

    // Sized for one avatar, not for a library. The largest real avatar measured declares 702 parameters,
    // so this is roughly 3x headroom; overrunning it degrades to allocation, never to incorrectness.
    private const int ParameterTableCapacity = 2048;

    private readonly IVrcObservationSink _observations;
    private readonly VrcAvatarEpoch _epoch;
    private readonly VrcEchoTracker? _echo;

    /// <summary>
    /// The intern table for <c>avatar.param.*</c> keys — this path's own, and replaced on every swap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not <see cref="SignalKeyInternTable.Shared"/>, and that is the entire point.</b> The shared table
    /// is capped at 4,096 entries and never evicts, by design — its own comment budgets "roughly 250 real
    /// keys". Avatar parameters break that budget badly: a real library of 154 avatars holds 6,362 distinct
    /// ones, so routing them through the shared table fills it after a median of ~82 swaps. Past the cap
    /// interning stays correct but allocates a fresh lowered string on every call, at face-tracking rates,
    /// and because nothing is ever evicted the table ends up full of avatars the user took off hours ago
    /// while the one they are wearing misses every time.
    /// </para>
    /// <para>
    /// Replacing the instance per avatar keeps the working set at one avatar's parameters. Safe because
    /// <see cref="SignalKey"/> equality is ordinal on the text, not reference identity on the instance — a
    /// key interned by this table equals the same key interned anywhere else, so descriptors registered
    /// before a swap still match cells written after one.
    /// </para>
    /// <para>
    /// Written and read only from the OSC receive loop, so a plain field assignment is sufficient.
    /// </para>
    /// </remarks>
    private SignalKeyInternTable _parameterKeys = new(ParameterTableCapacity);

    /// <param name="observations">Where projected observations go. <c>Core</c> supplies the kernel bridge.</param>
    /// <param name="avatarEpoch">Advanced by <c>/avatar/change</c>; read into every observation.</param>
    /// <param name="echo">
    /// Optional. When present, every avatar-parameter observation is offered to it, which is how a
    /// dispatched write learns that VRChat confirmed it (§12.9).
    /// </param>
    public VrcAvatarIngress(
        IVrcObservationSink observations,
        VrcAvatarEpoch avatarEpoch,
        VrcEchoTracker? echo = null)
    {
        _observations = observations ?? throw new ArgumentNullException(nameof(observations));
        _epoch = avatarEpoch ?? throw new ArgumentNullException(nameof(avatarEpoch));
        _echo = echo;
    }

    /// <summary>What the projection has seen. Never resets.</summary>
    public VrcIngressCounters Counters { get; } = new();

    /// <summary>
    /// The current avatar's key table, for the miss-rate alarm. Resets on every swap, by design.
    /// </summary>
    /// <remarks>
    /// Worth surfacing rather than hiding: a sustained miss rate here means parameter names are churning
    /// faster than they are being reused, which at face-tracking rates is the difference between zero
    /// allocations and thousands a second.
    /// </remarks>
    public SignalKeyInternStats ParameterKeyStats => _parameterKeys.Stats;

    private static ReadOnlySpan<byte> ParameterAddressPrefix => "/avatar/parameters/"u8;

    private static ReadOnlySpan<byte> ChangeAddress => "/avatar/change"u8;

    private static ReadOnlySpan<byte> EyeHeightAddress => "/avatar/eyeheight"u8;

    private static ReadOnlySpan<byte> EyeHeightMinAddress => "/avatar/eyeheightmin"u8;

    private static ReadOnlySpan<byte> EyeHeightMaxAddress => "/avatar/eyeheightmax"u8;

    private static ReadOnlySpan<byte> EyeHeightScalingAllowedAddress => "/avatar/eyeheightscalingallowed"u8;

    private static ReadOnlySpan<byte> TrackingPosePrefix => "/tracking/vrsystem/"u8;

    private static ReadOnlySpan<byte> TrackingPoseSuffix => "/pose"u8;

    private static ReadOnlySpan<byte> CameraPoseAddress => "/usercamera/Pose"u8;

    private static ReadOnlySpan<byte> CameraPrefix => "/usercamera/"u8;

    private static ReadOnlySpan<byte> DollyPrefix => "/dolly/"u8;

    private static ReadOnlySpan<byte> ParameterKeyPrefix => "avatar.param."u8;

    /// <summary>
    /// Projects one decoded message.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly, not as a public method, because the parameter type belongs to
    /// <c>MagicChatbox.Osc</c> and <c>Vrc</c>'s public surface names no <c>Osc</c> type
    /// (<c>VrcPublicSurface_LeaksNoOscType</c>). A host wires this up through <see cref="VrcTransport"/>,
    /// which owns the receiver, rather than by naming the sink interface itself.
    /// </remarks>
    void IOscMessageSink.OnMessage(scoped ref OscReader message) => Project(ref message);

    /// <summary>
    /// The projection itself, reachable without an <c>Osc</c> type in the signature so tests and future
    /// callers inside this assembly can drive it.
    /// </summary>
    internal void Project(scoped ref OscReader message)
    {
        var address = message.Address;

        if (address.StartsWith(ParameterAddressPrefix))
        {
            ProjectParameter(address[ParameterAddressPrefix.Length..], ref message);
            return;
        }

        if (address.SequenceEqual(ChangeAddress))
        {
            ProjectAvatarChange(ref message);
            return;
        }

        if (address.SequenceEqual(EyeHeightAddress))
        {
            ProjectEyeHeight(VrcAvatarKeys.EyeHeight, ref message);
            return;
        }

        if (address.SequenceEqual(EyeHeightMinAddress))
        {
            ProjectEyeHeight(VrcAvatarKeys.EyeHeightMin, ref message);
            return;
        }

        if (address.SequenceEqual(EyeHeightMaxAddress))
        {
            ProjectEyeHeight(VrcAvatarKeys.EyeHeightMax, ref message);
            return;
        }

        if (address.SequenceEqual(EyeHeightScalingAllowedAddress))
        {
            ProjectScalingAllowed(ref message);
            return;
        }

        if (address.StartsWith(TrackingPosePrefix) && address.EndsWith(TrackingPoseSuffix))
        {
            var device = address[TrackingPosePrefix.Length..^TrackingPoseSuffix.Length];
            ProjectPose(device, ref message);
            return;
        }

        if (address.SequenceEqual(CameraPoseAddress))
        {
            ProjectPose("camera"u8, ref message);
            return;
        }

        if (address.StartsWith(CameraPrefix) || address.StartsWith(DollyPrefix))
        {
            ProjectSubsystem(address, ref message);
            return;
        }

        // /tracking/*, /input/*, and whatever VRChat adds next. Counted, never logged: at face-tracking
        // rates a log line per ignored message is the application's dominant cost.
        Counters.CountIgnored();
    }

    private void ProjectParameter(ReadOnlySpan<byte> name, scoped ref OscReader message)
    {
        if (name.IsEmpty || ParameterKeyPrefix.Length + name.Length > MaxKeyUtf8Bytes)
        {
            Counters.CountUnmappable();
            return;
        }

        if (!message.TryReadNext(out var argument))
        {
            Counters.CountMalformed();
            return;
        }

        SignalValue value;
        switch (argument.TypeTag)
        {
            case 'T':
            case 'F':
                value = SignalValue.Bool(argument.AsBool());
                break;

            case 'i':
                value = SignalValue.Int(argument.AsInt32());
                break;

            // Non-finite floats are forwarded rather than dropped. D4 puts that rejection at the store,
            // with a named ReasonCode, because "the avatar sent us a NaN" is a fact the user should be
            // able to see rather than a message that quietly disappeared two assemblies earlier.
            case 'f':
                value = SignalValue.Float(argument.AsFloat32());
                break;

            default:
                // VRChat's avatar parameters are exactly T/F/i/f. Anything else is a schema we do not
                // have, not a value we can classify.
                Counters.CountMalformed();
                return;
        }

        Span<byte> keyBytes = stackalloc byte[MaxKeyUtf8Bytes];
        ParameterKeyPrefix.CopyTo(keyBytes);
        name.CopyTo(keyBytes[ParameterKeyPrefix.Length..]);

        if (!_parameterKeys.TryInternUtf8(keyBytes[..(ParameterKeyPrefix.Length + name.Length)], out var key))
        {
            // A parameter name the key grammar rejects — a slash, a space, anything non-ASCII. Rejected,
            // never repaired: v2 repaired keys and the repaired form silently diverged from the form the
            // descriptor was registered under, so the composer read an empty cell forever.
            Counters.CountUnmappable();
            return;
        }

        Counters.CountParameter();
        Publish(key, value);

        // After publishing, not before: the cell is the source of truth (Q8 — only Observe writes an
        // avatar parameter), and the ledger's CommandCompleted should follow the change it describes.
        _echo?.TryConfirm(key, value);
    }

    private void ProjectAvatarChange(scoped ref OscReader message)
    {
        if (!message.TryReadNext(out var argument) || argument.TypeTag != 's')
        {
            Counters.CountMalformed();
            return;
        }

        // The one allocation on this path, once per avatar rather than once per message.
        var avatarId = argument.AsString();
        Counters.CountAvatarChange();

        if (!SignalValue.TryText(avatarId, out var value))
        {
            Counters.CountUnmappable();
            return;
        }

        // P10, in order: the epoch advances FIRST, which invalidates every outstanding echo wait and
        // tells Core to evict avatar.param.* — before avatar.id names the avatar those evictions are
        // making room for. Reversing this leaves a window where avatar.id is new and the parameters are
        // still the old avatar's.
        var previousEpoch = _epoch.Current;
        var epoch = _epoch.AdvanceToAvatar(avatarId);

        if (epoch != previousEpoch)
        {
            // The departing avatar's parameter names are now dead weight, so the whole table goes rather
            // than being added to. See the field's remarks for why this matters more than it looks.
            _parameterKeys = new SignalKeyInternTable(ParameterTableCapacity);
        }

        if (SignalKey.TryIntern(VrcAvatarKeys.AvatarId, out var key))
        {
            _observations.OnObservation(new VrcObservation(key, value, epoch));
        }
    }

    private void ProjectEyeHeight(string keyText, scoped ref OscReader message)
    {
        if (!message.TryReadNext(out var argument) || argument.TypeTag != 'f')
        {
            Counters.CountMalformed();
            return;
        }

        if (!SignalKey.TryIntern(keyText, out var key))
        {
            Counters.CountUnmappable();
            return;
        }

        Counters.CountEyeHeight();

        // P8: /avatar/eyeheight echoes TWICE — the requested value, then the Udon-enforced one the world
        // may have clamped it to. Both are published, in order, and the second is the truth. This is why
        // SetEyeHeightAsync deliberately does not correlate: verifying after the first echo would record
        // a value that is about to change.
        Publish(key, SignalValue.Float(argument.AsFloat32()));
    }

    /// <summary>
    /// Projects <c>/avatar/eyeheightscalingallowed</c>, the world's permission to scale at all.
    /// </summary>
    /// <remarks>
    /// A discrete bool that changes on world load, not on avatar load — which is why it is a session key
    /// rather than an <c>avatar.param.*</c> one, and survives a swap.
    /// </remarks>
    private void ProjectScalingAllowed(scoped ref OscReader message)
    {
        if (!message.TryReadNext(out var argument) || argument.TypeTag is not ('T' or 'F'))
        {
            Counters.CountMalformed();
            return;
        }

        if (!SignalKey.TryIntern(VrcAvatarKeys.EyeHeightScalingAllowed, out var key))
        {
            Counters.CountUnmappable();
            return;
        }

        Counters.CountEyeHeight();
        Publish(key, SignalValue.Bool(argument.AsBool()));
    }

    /// <summary>
    /// Projects one <c>/tracking/vrsystem/{device}/pose</c> — six floats into six cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>All six or none.</b> The components are read into a buffer first and published only once the
    /// whole message has parsed. A pose whose Z arrived and whose rotation did not is not a partial
    /// pose, it is a wrong one, and half-updating three of six cells would leave a reader mixing two
    /// different instants with nothing to detect it by.
    /// </para>
    /// <para>
    /// Only the three devices VRChat advertises are accepted. The address carries the device name, so
    /// matching on a prefix alone would mint <c>input.vr.&lt;anything&gt;.position.x</c> keys from a
    /// malformed or future address — unbounded key growth on a path that runs at headset frame rate.
    /// </para>
    /// </remarks>
    private void ProjectPose(ReadOnlySpan<byte> device, scoped ref OscReader message)
    {
        var name = ResolveDevice(device);
        if (name is null)
        {
            Counters.CountIgnored();
            return;
        }

        Span<float> components = stackalloc float[VrcAvatarKeys.PoseComponents.Length];
        for (var i = 0; i < components.Length; i++)
        {
            if (!message.TryReadNext(out var argument) || argument.TypeTag != 'f')
            {
                Counters.CountMalformed();
                return;
            }

            components[i] = argument.AsFloat32();
        }

        for (var i = 0; i < components.Length; i++)
        {
            if (SignalKey.TryIntern(VrcAvatarKeys.TrackingKey(name, VrcAvatarKeys.PoseComponents[i]), out var key))
            {
                Publish(key, SignalValue.Float(components[i]));
            }
        }

        Counters.CountPose();
    }

    private static string? ResolveDevice(ReadOnlySpan<byte> device)
    {
        if (device.SequenceEqual("head"u8)) return "head";
        if (device.SequenceEqual("leftwrist"u8)) return "leftwrist";
        if (device.SequenceEqual("rightwrist"u8)) return "rightwrist";
        if (device.SequenceEqual("camera"u8)) return "camera";
        return null;
    }

    /// <summary>
    /// Projects one readable <c>/usercamera</c> or <c>/dolly</c> address.
    /// </summary>
    /// <remarks>
    /// Looked up in <see cref="VrcSubsystems"/> rather than pattern-matched, so an address VRChat
    /// advertises but this build does not model is counted as ignored instead of minting a key. The
    /// write-only actions — Capture, Close, the dolly imports — are in that table with a null key and
    /// therefore land here and are correctly ignored: VRChat never echoes them, so a cell claiming their
    /// value would be asserting something nobody confirmed.
    /// </remarks>
    private void ProjectSubsystem(ReadOnlySpan<byte> address, scoped ref OscReader message)
    {
        Span<char> chars = stackalloc char[MaxKeyUtf8Bytes];
        var written = System.Text.Encoding.UTF8.GetChars(address, chars);
        var text = new string(chars[..written]);

        if (!VrcSubsystems.TryByAddress(text, out var descriptor) || descriptor.Key is null)
        {
            Counters.CountIgnored();
            return;
        }

        if (!message.TryReadNext(out var argument))
        {
            Counters.CountMalformed();
            return;
        }

        SignalValue value;
        switch (argument.TypeTag)
        {
            case 'T':
            case 'F':
                value = SignalValue.Bool(argument.AsBool());
                break;
            case 'i':
                value = SignalValue.Int(argument.AsInt32());
                break;
            case 'f':
                value = SignalValue.Float(argument.AsFloat32());
                break;
            default:
                Counters.CountMalformed();
                return;
        }

        if (!SignalKey.TryIntern(descriptor.Key, out var key))
        {
            Counters.CountUnmappable();
            return;
        }

        Counters.CountSubsystem();
        Publish(key, value);
    }

    private void Publish(SignalKey key, SignalValue value) =>
        _observations.OnObservation(new VrcObservation(key, value, _epoch.Current));
}
