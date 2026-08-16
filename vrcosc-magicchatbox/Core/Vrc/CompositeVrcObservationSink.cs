using MagicChatbox.Vrc;
using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed class CompositeVrcObservationSink : IVrcObservationSink
{
    private readonly IVrcObservationSink[] _sinks;

    public CompositeVrcObservationSink(params IVrcObservationSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        foreach (IVrcObservationSink sink in sinks)
        {
            if (sink is null)
                throw new ArgumentException("A null sink would fault the receive loop.", nameof(sinks));
        }

        _sinks = sinks;
    }

    public void OnObservation(in VrcObservation observation)
    {
        for (int i = 0; i < _sinks.Length; i++)
        {
            try
            {
                _sinks[i].OnObservation(in observation);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }
    }
}
