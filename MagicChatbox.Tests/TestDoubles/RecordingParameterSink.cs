using System.Collections.Generic;
using vrcosc_magicchatbox.Core.Vrc;

namespace MagicChatbox.Tests.TestDoubles;

// Accepts every write and remembers it, so a test can assert what a view model asked for without a
// pump, a socket or VRChat.
public sealed class RecordingParameterSink : IAvatarParameterSink
{
    public readonly record struct Write(string Name, double Value);

    public List<Write> Writes { get; } = [];

    public List<string> Pulses { get; } = [];

    public void Set(string name, bool value) => Writes.Add(new Write(name, value ? 1d : 0d));

    public void Set(string name, int value) => Writes.Add(new Write(name, value));

    public void Set(string name, float value) => Writes.Add(new Write(name, value));

    public void Pulse(string name, int milliseconds = 150) => Pulses.Add(name);
}
