using System.Text;
using MagicChatbox.Osc;
using MagicChatbox.Vocabulary;

namespace MagicChatbox.Vrc;

/// <summary>The one implementation of <see cref="IVrcEgress"/>. See that interface for why.</summary>
internal sealed class VrcEgress : IVrcEgress
{
    private readonly IOscSender _sender;
    private readonly IWorldPolicy _world;
    private readonly IProfanityPolicy _profanity;
    private readonly IChatboxCadence _cadence;
    private readonly IEgressJournal _journal;
    private readonly VrcEchoTracker? _echo;
    private readonly object _budgetGate = new();

    private VrcChatboxBudget _budget = VrcChatboxBudget.Empty;

    public VrcEgress(
        IOscSender sender,
        IWorldPolicy world,
        IProfanityPolicy profanity,
        IChatboxCadence cadence,
        IEgressJournal? journal = null,
        VrcEchoTracker? echo = null)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _profanity = profanity ?? throw new ArgumentNullException(nameof(profanity));
        _cadence = cadence ?? throw new ArgumentNullException(nameof(cadence));
        _journal = journal ?? NullEgressJournal.Instance;
        _echo = echo;
    }

    public VrcChatboxBudget Budget
    {
        get { lock (_budgetGate) { return _budget; } }
    }

    public async ValueTask<EgressResult> SendChatboxAsync(ComposedMessage message, CancellationToken cancellationToken)
    {
        const string surface = "chatbox.send";
        var correlation = Correlation.For(surface);
        var text = message.Text ?? string.Empty;

        // SAFETY, always first, always in this order.
        if (_world.IsCurrentWorldMuted)
        {
            return Blocked(correlation, surface, ReasonCode.EgressWorldMuted, null);
        }

        if (_profanity.Blocks(text, out var term))
        {
            return Blocked(correlation, surface, ReasonCode.EgressProfanityBlocked, term);
        }

        // Belt and braces: the composer has already fitted this. If it has not, the bug is there, and
        // this stops a malformed line reaching the wire while making the failure visible in the ledger.
        var length = VrcChatboxLimits.Measure(text);
        if (length > VrcChatboxLimits.MaxCharacters)
        {
            return Blocked(
                correlation, surface, ReasonCode.EgressBudgetExceeded,
                $"{length} characters exceeds {VrcChatboxLimits.MaxCharacters}");
        }

        if (!_cadence.TryAcquire())
        {
            return Blocked(correlation, surface, ReasonCode.RateLimited, null);
        }

        // P5 — ",sTT": text, sendImmediately = true (bypasses the in-game keyboard),
        // playNotificationSfx = false. The third argument is sent EXPLICITLY rather than omitted.
        // Its documented default is true, so relying on the default is how a future VRChat change turns
        // every chatbox update into a notification chime for everyone in the room. VRCOSC sends it
        // explicitly for the same reason (ChatBoxManager.cs:369).
        var dispatched = await _sender
            .SendAsync(OscMessage.Create("/chatbox/input", text, true, false), cancellationToken)
            .ConfigureAwait(false);

        if (!dispatched)
        {
            return Blocked(correlation, surface, ReasonCode.EgressNoEndpoint, null);
        }

        lock (_budgetGate)
        {
            _budget = new VrcChatboxBudget(length, VrcChatboxLimits.MaxCharacters);
        }

        _journal.Dispatched(correlation, surface, text);
        return new EgressResult(true, ReasonCode.Ok, correlation.OperationId);
    }

    public ValueTask<EgressResult> SetTypingAsync(bool typing, CancellationToken cancellationToken) =>
        SendSimpleAsync(
            "chatbox.typing",
            OscMessage.Create("/chatbox/typing", OscArg.Bool(typing)),
            respectWorldMute: true,
            cancellationToken);

    public ValueTask<EgressResult> SetAvatarParameterAsync(
        string name, VrcParameterValue value, CancellationToken cancellationToken)
    {
        const string surface = "avatar.parameter.set";
        var correlation = Correlation.For(surface);

        if (string.IsNullOrWhiteSpace(name))
        {
            return Reject(correlation, surface, ReasonCode.EgressUnsupportedValue, "empty parameter name");
        }

        // The name is interpolated into an address below, so this is the one place on this interface
        // where a caller's string becomes part of a destination. See the helper for what that can do.
        if (FindIllegalAddressChar(name) is { } illegal)
        {
            return Reject(
                correlation, surface, ReasonCode.EgressUnsupportedValue,
                $"parameter name contains '{illegal}', which is not legal in an OSC address");
        }

        // D4's sibling on the egress side: a non-finite float must never reach the wire. Inbound, NaN
        // defeats epsilon dedupe; outbound, it is simply meaningless to VRChat.
        if (!value.IsFinite())
        {
            return Reject(correlation, surface, ReasonCode.NonFiniteValue, name);
        }

        var arg = value.Kind switch
        {
            VrcParameterKind.Bool => OscArg.Bool(value.AsBool()),
            VrcParameterKind.Int => OscArg.Int32(value.AsInt()),
            VrcParameterKind.Float => OscArg.Float32(value.AsFloat()),
            _ => throw new InvalidOperationException($"Unhandled {nameof(VrcParameterKind)} '{value.Kind}'."),
        };

        // P7: registered BEFORE dispatch, because VRChat's echo can be in the receive buffer before the
        // await on the socket send has finished unwinding. Registering afterwards is a race whose only
        // symptom is an occasional inexplicable timeout on a healthy connection.
        //
        // A name the key grammar rejects gets no registration and still gets sent: VRChat's namespace is
        // wider than the kernel's, and refusing to write a parameter we merely cannot model as a cell
        // would be the wrong half of the problem to solve.
        VrcEchoWait? pending = null;
        if (_echo is not null && SignalKey.TryIntern(VrcAvatarKeys.ParameterKeyPrefix + name, out var key))
        {
            pending = _echo.Register(correlation.OperationId, key, ToSignalValue(value));
        }

        // Not Send(): this is the one non-chatbox surface that registers an echo, so it needs the full
        // overload rather than the no-world-gate shorthand.
        return SendSimpleAsync(
            surface,
            OscMessage.Create($"/avatar/parameters/{name}", arg),
            respectWorldMute: false,
            cancellationToken,
            correlation,
            pending);
    }

    public ValueTask<EgressResult> SetEyeHeightAsync(float metres, CancellationToken cancellationToken)
    {
        const string surface = "avatar.eyeheight.set";
        var correlation = Correlation.For(surface);

        if (!float.IsFinite(metres))
        {
            return Reject(correlation, surface, ReasonCode.NonFiniteValue, null);
        }

        return Send(
            surface, correlation,
            OscMessage.Create("/avatar/eyeheight", OscArg.Float32(metres)),
            cancellationToken);
    }

    public ValueTask<EgressResult> SendInputAsync(VrcInput input, float value, CancellationToken cancellationToken)
    {
        const string surface = "input.send";
        var correlation = Correlation.For(surface);

        if (!float.IsFinite(value))
        {
            return Reject(correlation, surface, ReasonCode.NonFiniteValue, null);
        }

        // A button is a bool on the wire, not a float. VRChat's coercion table would accept 0.0/1.0 for
        // one-argument messages, so sending a float works — but it also makes `Jump = 0.5` expressible,
        // and a half-pressed jump is not a thing. The type tag now says which of the two shapes this is.
        var argument = VrcInputs.KindOf(input) == VrcInputKind.Button
            ? OscArg.Bool(value != 0f)
            : OscArg.Float32(value);

        return Send(surface, correlation, OscMessage.Create($"/input/{input}", argument), cancellationToken);
    }

    public ValueTask<EgressResult> SetSubsystemAsync(
        string key, VrcParameterValue value, CancellationToken cancellationToken)
    {
        const string surface = "vrc.subsystem.set";
        var correlation = Correlation.For(surface);

        if (!VrcSubsystems.TryByKey(key, out var target) || target.Access == VrcAccess.Read)
        {
            // Either not an address we model, or one VRChat only reports. Both are the caller naming
            // something that cannot be written, and both deserve the same answer rather than a
            // dispatch into nothing.
            return Reject(correlation, surface, ReasonCode.ObservedOnly, key);
        }

        if (!TryCoerce(value, target.Kind, out var argument))
        {
            return Reject(correlation, surface, ReasonCode.KindMismatch, key);
        }

        return Send(surface, correlation, OscMessage.Create(target.Address, argument), cancellationToken);
    }

    public ValueTask<EgressResult> SendSubsystemActionAsync(
        VrcAction action, VrcParameterValue? argument, CancellationToken cancellationToken)
    {
        const string surface = "vrc.subsystem.action";
        var correlation = Correlation.For(surface);
        var target = VrcSubsystems.For(action);

        // A bare trigger is a true, matching how VRChat documents its own button addresses.
        var value = argument ?? VrcParameterValue.Bool(true);
        if (!TryCoerce(value, target.Kind, out var arg))
        {
            return Reject(correlation, surface, ReasonCode.KindMismatch, action.ToString());
        }

        return Send(surface, correlation, OscMessage.Create(target.Address, arg), cancellationToken);
    }

    /// <summary>Puts a value on the wire as the kind VRChat declared for that address, or refuses.</summary>
    /// <remarks>
    /// The conversion itself already lives on <see cref="VrcParameterValue"/>, which is a numeric union
    /// with the three accessors — so this only has to pick the one the address declared. Sending the
    /// declared type rather than relying on VRChat's own coercion table means a caller with the wrong
    /// idea gets the value it asked for, not a silent reinterpretation.
    /// </remarks>
    private static bool TryCoerce(VrcParameterValue value, SignalKind kind, out OscArg argument)
    {
        if (!value.IsFinite())
        {
            argument = default;
            return false;
        }

        switch (kind)
        {
            case SignalKind.Bool:
                argument = OscArg.Bool(value.AsBool());
                return true;
            case SignalKind.Int:
                argument = OscArg.Int32(value.AsInt());
                return true;
            case SignalKind.Float:
                argument = OscArg.Float32(value.AsFloat());
                return true;
            default:
                // Text addresses — the dolly's import and export paths — are unreachable through
                // VrcParameterValue, which is a numeric union by design.
                argument = default;
                return false;
        }
    }

    public async ValueTask<EgressResult> SendTrackingAsync(
        VrcTrackingFrame frame, CancellationToken cancellationToken)
    {
        const string surface = "tracking.send";
        var correlation = Correlation.For(surface);

        if (frame.Position is null && frame.Rotation is null)
        {
            // Not the same as "send nothing": a caller that meant to move a tracker and built an empty
            // frame would otherwise get a cheerful Ok for a datagram that was never composed.
            return Blocked(correlation, surface, ReasonCode.EgressUnsupportedValue, "frame carries neither position nor rotation");
        }

        if (frame.Position is { } p && !p.IsFinite())
        {
            return Blocked(correlation, surface, ReasonCode.NonFiniteValue, "position");
        }

        if (frame.Rotation is { } r && !r.IsFinite())
        {
            return Blocked(correlation, surface, ReasonCode.NonFiniteValue, "rotation");
        }

        // The wire spells the head slot "head" and the numbered ones bare, so neither is the enum's own
        // name. Deriving the segment here rather than from ToString() is what keeps a member rename from
        // silently writing to /tracking/trackers/Tracker3/.
        var slot = frame.Slot == VrcTrackerSlot.Head ? "head" : ((int)frame.Slot).ToString();
        var stem = $"/tracking/trackers/{slot}/";

        // Position first: VRChat aligns the tracking space from the head's position, and sending the
        // rotation of a point it has not placed yet is a frame of yaw applied to the wrong origin.
        if (frame.Position is { } position)
        {
            var sent = await Send(surface, correlation, Vector(stem + "position", position), cancellationToken).ConfigureAwait(false);

            if (!sent.Dispatched)
            {
                return sent;
            }
        }

        if (frame.Rotation is { } rotation)
        {
            return await Send(surface, correlation, Vector(stem + "rotation", rotation), cancellationToken).ConfigureAwait(false);
        }

        return new EgressResult(true, ReasonCode.Ok, correlation.OperationId);

        static OscMessage Vector(string address, VrcVector3 v) =>
            OscMessage.Create(address, OscArg.Float32(v.X), OscArg.Float32(v.Y), OscArg.Float32(v.Z));
    }

    public ValueTask<EgressResult> SetEyesClosedAsync(float amount, CancellationToken cancellationToken)
    {
        const string surface = "tracking.eye.eyelid";
        var correlation = Correlation.For(surface);

        if (!float.IsFinite(amount))
        {
            return Reject(correlation, surface, ReasonCode.NonFiniteValue, null);
        }

        // 0..1 is the documented domain and the only bound VRChat states on this surface. Refused
        // rather than clamped: a sender computing 1.4 has a scaling bug, and clamping hides it behind
        // eyelids that look almost right.
        if (amount is < 0f or > 1f)
        {
            return Reject(correlation, surface, ReasonCode.EgressUnsupportedValue, $"{amount} is outside 0..1");
        }

        return Send(
            surface, correlation,
            OscMessage.Create("/tracking/eye/EyesClosedAmount", OscArg.Float32(amount)),
            cancellationToken);
    }

    public ValueTask<EgressResult> SendEyeGazeAsync(
        VrcEyeGaze gaze, ReadOnlyMemory<float> values, CancellationToken cancellationToken)
    {
        const string surface = "tracking.eye.gaze";
        var correlation = Correlation.For(surface);

        if (!Enum.IsDefined(gaze))
        {
            return Reject(correlation, surface, ReasonCode.EgressUnsupportedValue, gaze.ToString());
        }

        // The arity is protocol: VRChat reads a fixed count per address, so the wrong number is not a
        // smaller frame, it is a different message.
        var arity = VrcEyeGazes.ArityOf(gaze);
        if (values.Length != arity)
        {
            return Reject(
                correlation, surface, ReasonCode.EgressUnsupportedValue,
                $"{gaze} takes {arity} floats, got {values.Length}");
        }

        var span = values.Span;
        var args = new OscArg[arity];
        for (var i = 0; i < arity; i++)
        {
            if (!float.IsFinite(span[i]))
            {
                return Reject(correlation, surface, ReasonCode.NonFiniteValue, $"{gaze}[{i}]");
            }

            args[i] = OscArg.Float32(span[i]);
        }

        return Send(surface, correlation, OscMessage.Create(VrcEyeGazes.AddressOf(gaze), args), cancellationToken);
    }

    /// <summary>
    /// Send on a surface that has no world gate and no cadence — everything except the chatbox.
    /// </summary>
    /// <remarks>
    /// Exists so that <c>respectWorldMute: false</c> is written once rather than at seven call sites,
    /// where it reads as a decision each time instead of as the default it is. The chatbox is the only
    /// surface the world blacklist applies to, and it calls the full pipeline directly.
    /// </remarks>
    private ValueTask<EgressResult> Send(
        string surface, Correlation correlation, OscMessage message, CancellationToken cancellationToken) =>
        SendSimpleAsync(surface, message, respectWorldMute: false, cancellationToken, correlation);

    /// <summary>Refuse before the wire, journalling the reason.</summary>
    private ValueTask<EgressResult> Reject(
        Correlation correlation, string surface, ReasonCode reason, string? detail) =>
        ValueTask.FromResult(Blocked(correlation, surface, reason, detail));

    /// <summary>
    /// The first character of <paramref name="name"/> that must not go into an OSC address, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is an egress-fence check, not tidiness.</b> The parameter name is interpolated straight
    /// into <c>/avatar/parameters/{name}</c>, and OSC 1.0 reserves <c>? * [ ] { }</c> as <i>pattern</i>
    /// characters: an address containing them is a wildcard, and a receiver that honours patterns treats
    /// <c>/avatar/parameters/*</c> as every parameter at once. One badly-named parameter would then
    /// write one value over the whole avatar. The rest — space, <c>#</c>, <c>,</c> and the control
    /// characters — are simply not legal in an address, and produce a packet whose behaviour is
    /// whatever the receiver's parser happens to do.
    /// </para>
    /// <para>
    /// <b><c>/</c> is deliberately allowed.</b> It is a separator rather than a metacharacter, and
    /// VRChat avatar authors really do use it — <c>Go/JSRF/ReadyToGrind</c> is a name from this
    /// codebase's own notes. Refusing it would break a large fraction of real avatars to close a hole
    /// that a separator cannot open, because the prefix is fixed and nothing can climb back out of it.
    /// </para>
    /// </remarks>
    private static char? FindIllegalAddressChar(string name)
    {
        foreach (var c in name)
        {
            if (c is ' ' or '#' or '*' or ',' or '?' or '[' or ']' or '{' or '}' || char.IsControl(c))
            {
                return c;
            }
        }

        return null;
    }

    private static SignalValue ToSignalValue(VrcParameterValue value) => value.Kind switch
    {
        VrcParameterKind.Bool => SignalValue.Bool(value.AsBool()),
        VrcParameterKind.Int => SignalValue.Int(value.AsInt()),
        VrcParameterKind.Float => SignalValue.Float(value.AsFloat()),
        _ => throw new InvalidOperationException($"Unhandled {nameof(VrcParameterKind)} '{value.Kind}'."),
    };

    private async ValueTask<EgressResult> SendSimpleAsync(
        string surface,
        OscMessage message,
        bool respectWorldMute,
        CancellationToken cancellationToken,
        Correlation? existing = null,
        VrcEchoWait? pending = null)
    {
        var correlation = existing ?? Correlation.For(surface);

        if (respectWorldMute && _world.IsCurrentWorldMuted)
        {
            return Blocked(correlation, surface, ReasonCode.EgressWorldMuted, null);
        }

        var dispatched = await _sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!dispatched)
        {
            // Nothing left the socket, so nothing can echo. Leaving the registration to expire would
            // report a two-second timeout for a failure we knew about immediately.
            if (pending is not null)
            {
                _echo?.Cancel(pending.Pending.OperationId);
            }

            return Blocked(correlation, surface, ReasonCode.EgressNoEndpoint, null);
        }

        _journal.Dispatched(correlation, surface, message.Address);
        return new EgressResult(true, ReasonCode.Ok, correlation.OperationId);
    }

    private EgressResult Blocked(Correlation correlation, string surface, ReasonCode reason, string? detail)
    {
        _journal.Blocked(correlation, surface, reason, detail);
        return new EgressResult(false, reason, correlation.OperationId, detail);
    }
}
