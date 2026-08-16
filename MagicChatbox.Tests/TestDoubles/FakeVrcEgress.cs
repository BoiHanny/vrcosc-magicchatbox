using MagicChatbox.Vocabulary;
using MagicChatbox.Vrc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MagicChatbox.Tests.TestDoubles;

// Stands in for the real egress so the pump can be tested without a socket, VRChat, or a clock that
// depends on either. Only the avatar-parameter path is interesting here; the rest of the interface
// exists to be satisfied.
public sealed class FakeVrcEgress : IVrcEgress
{
    public readonly record struct Write(string Name, VrcParameterValue Value);

    private readonly Lock _gate = new();
    private readonly List<Write> _writes = [];

    public bool Dispatches { get; set; } = true;

    public Exception? ThrowOnWrite { get; set; }

    public TimeSpan WriteDelay { get; set; } = TimeSpan.Zero;

    public IReadOnlyList<Write> Writes
    {
        get { lock (_gate) return _writes.ToArray(); }
    }

    public int CountOf(string name)
    {
        lock (_gate) return _writes.Count(w => string.Equals(w.Name, name, StringComparison.Ordinal));
    }

    public VrcParameterValue? LastValueOf(string name)
    {
        lock (_gate)
        {
            for (int i = _writes.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_writes[i].Name, name, StringComparison.Ordinal))
                    return _writes[i].Value;
            }
        }

        return null;
    }

    public void ClearWrites()
    {
        lock (_gate) _writes.Clear();
    }

    public async ValueTask<EgressResult> SetAvatarParameterAsync(
        string name, VrcParameterValue value, CancellationToken cancellationToken)
    {
        if (ThrowOnWrite != null)
            throw ThrowOnWrite;

        if (WriteDelay > TimeSpan.Zero)
            await Task.Delay(WriteDelay, cancellationToken).ConfigureAwait(false);

        lock (_gate) _writes.Add(new Write(name, value));

        return new EgressResult(Dispatches, Dispatches ? ReasonCode.Ok : ReasonCode.EgressNoEndpoint, Guid.NewGuid());
    }

    public VrcChatboxBudget Budget => VrcChatboxBudget.Empty;

    public ValueTask<EgressResult> SendChatboxAsync(ComposedMessage message, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SetTypingAsync(bool typing, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SetEyeHeightAsync(float metres, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SendInputAsync(VrcInput input, float value, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SendTrackingAsync(VrcTrackingFrame frame, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SetEyesClosedAsync(float amount, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SendEyeGazeAsync(
        VrcEyeGaze gaze, ReadOnlyMemory<float> values, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SetSubsystemAsync(
        string key, VrcParameterValue value, CancellationToken cancellationToken)
        => Ok();

    public ValueTask<EgressResult> SendSubsystemActionAsync(
        VrcAction action, VrcParameterValue? argument, CancellationToken cancellationToken)
        => Ok();

    private static ValueTask<EgressResult> Ok()
        => ValueTask.FromResult(new EgressResult(true, ReasonCode.Ok, Guid.NewGuid()));
}
