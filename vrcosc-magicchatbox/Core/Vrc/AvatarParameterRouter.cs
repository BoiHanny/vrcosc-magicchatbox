using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed class AvatarParameterRouter : IAvatarParameterSink
{
    private readonly IOscSender _oscSender;
    private readonly Func<AvatarParameterPump?> _pump;
    private readonly Func<bool> _mirrorToLegacy;
    private readonly Dictionary<string, int> _pulseSequence = new(StringComparer.Ordinal);

    public AvatarParameterRouter(
        IOscSender oscSender,
        Func<AvatarParameterPump?> pump,
        Func<bool>? mirrorToLegacy = null)
    {
        _oscSender = oscSender ?? throw new ArgumentNullException(nameof(oscSender));
        _pump = pump ?? throw new ArgumentNullException(nameof(pump));
        _mirrorToLegacy = mirrorToLegacy ?? (() => true);
    }

    public void Set(string name, bool value)
    {
        string address = AvatarParameterAddress.Resolve(name);
        if (address.Length == 0) return;

        AvatarParameterPump? pump = RunningPump();
        pump?.Publish(AvatarParameterAddress.ToName(address), value);

        if (ShouldSendLegacy(pump))
            _oscSender.SendOscParam(address, value);
    }

    public void Set(string name, int value)
    {
        string address = AvatarParameterAddress.Resolve(name);
        if (address.Length == 0) return;

        AvatarParameterPump? pump = RunningPump();
        pump?.Publish(AvatarParameterAddress.ToName(address), value);

        if (ShouldSendLegacy(pump))
            _oscSender.SendOscParam(address, value);
    }

    public void Set(string name, float value)
    {
        string address = AvatarParameterAddress.Resolve(name);
        if (address.Length == 0) return;

        AvatarParameterPump? pump = RunningPump();
        pump?.Publish(AvatarParameterAddress.ToName(address), value);

        if (ShouldSendLegacy(pump))
            _oscSender.SendOscParam(address, value);
    }

    private bool ShouldSendLegacy(AvatarParameterPump? pump)
    {
        if (pump == null)
            return true;

        try
        {
            return _mirrorToLegacy();
        }
        catch
        {
            return true;
        }
    }

    public void Pulse(string name, int milliseconds = 150)
    {
        string address = AvatarParameterAddress.Resolve(name);
        if (address.Length == 0) return;

        int seq;
        lock (_pulseSequence)
        {
            _pulseSequence.TryGetValue(address, out int current);
            seq = current + 1;
            _pulseSequence[address] = seq;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Set(address, true);
                await Task.Delay(Math.Max(1, milliseconds)).ConfigureAwait(false);

                lock (_pulseSequence)
                {
                    if (_pulseSequence.TryGetValue(address, out int latest) && latest != seq)
                        return;
                }

                Set(address, false);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        });
    }

    private AvatarParameterPump? RunningPump()
    {
        try
        {
            AvatarParameterPump? pump = _pump();
            return pump is { IsRunning: true } ? pump : null;
        }
        catch
        {
            return null;
        }
    }
}
