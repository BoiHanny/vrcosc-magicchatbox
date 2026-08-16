using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Services;

namespace MagicChatbox.Tests.TestDoubles;

// One IOscSender stand-in for every test that needs one. Three near-identical copies used to live in
// three test files, and only one of them recorded anything - so a test that wanted to assert on an
// avatar parameter had to grow a fourth. This records both chatbox text and parameters, and does
// nothing surprising when a test only needs the interface satisfied.
public sealed class FakeOscSender : IOscSender
{
    public readonly record struct SentParameter(string Address, object Value);

    private readonly Lock _gate = new();
    private readonly List<string> _sent = [];
    private readonly List<SentParameter> _parameters = [];

    public int Clears { get; private set; }
    public bool AnySoundRequested { get; private set; }
    public int TypingStarts { get; private set; }
    public int TypingStops { get; private set; }
    public int VoiceToggles { get; private set; }

    public IReadOnlyList<string> Sent
    {
        get { lock (_gate) return _sent.ToArray(); }
    }

    public IReadOnlyList<SentParameter> Parameters
    {
        get { lock (_gate) return _parameters.ToArray(); }
    }

    public IReadOnlyList<object> ValuesFor(string address)
    {
        lock (_gate)
        {
            return _parameters
                .Where(p => string.Equals(p.Address, address, StringComparison.Ordinal))
                .Select(p => p.Value)
                .ToArray();
        }
    }

    public object? LastValueFor(string address)
    {
        lock (_gate)
        {
            for (int i = _parameters.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_parameters[i].Address, address, StringComparison.Ordinal))
                    return _parameters[i].Value;
            }
        }

        return null;
    }

    public bool WasSent(string address) => LastValueFor(address) != null;

    public void Clear()
    {
        lock (_gate)
        {
            _sent.Clear();
            _parameters.Clear();
            Clears = 0;
            AnySoundRequested = false;
            TypingStarts = 0;
            TypingStops = 0;
            VoiceToggles = 0;
        }
    }

    public Task<bool> SendOSCMessage(bool fx, int delay = 0, bool force = false, string? explicitText = null)
    {
        lock (_gate)
        {
            if (fx) AnySoundRequested = true;
            _sent.Add(explicitText ?? string.Empty);
        }

        return Task.FromResult(true);
    }

    public void SendOscParam(string address, float value) => Record(address, value);

    public void SendOscParam(string address, int value) => Record(address, value);

    public void SendOscParam(string address, bool value) => Record(address, value);

    public void SendTypingIndicatorAsync()
    {
        lock (_gate) TypingStarts++;
    }

    public void StopTypingIndicator()
    {
        lock (_gate) TypingStops++;
    }

    public Task SentClearMessage(int delay)
    {
        lock (_gate) Clears++;
        return Task.CompletedTask;
    }

    public Task ToggleVoice(bool force = false)
    {
        lock (_gate) VoiceToggles++;
        return Task.CompletedTask;
    }

    private void Record(string address, object value)
    {
        lock (_gate) _parameters.Add(new SentParameter(address, value));
    }
}
