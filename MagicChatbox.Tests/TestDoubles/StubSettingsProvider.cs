using System;
using vrcosc_magicchatbox.Core.Configuration;

namespace MagicChatbox.Tests.TestDoubles;

// The same five-line settings provider stub was copied into a handful of test files. This is that
// stub, once, with counters so a test can also assert that something asked for a save.
public sealed class StubSettingsProvider<T>(T value) : ISettingsProvider<T> where T : class, new()
{
    public StubSettingsProvider() : this(new T()) { }

    public T Value { get; } = value;

    public int Saves { get; private set; }
    public int Flushes { get; private set; }
    public int Reloads { get; private set; }

    public void Save() => Saves++;

    public void FlushPendingSave() => Flushes++;

    public void Reload() => Reloads++;

    public event EventHandler SettingsChanged { add { } remove { } }
}
