using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Vrc;

public static class InboundCommandRegistry
{
    public const string ControlPrefix = "MCB/Ctrl/";

    public static IReadOnlyList<InboundCommand> Build(
        IAppState appState,
        ITtsPlaybackService tts)
    {
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(tts);

        return new[]
        {
            new InboundCommand(
                "MCB/Ctrl/Tts/Stop",
                InboundTrigger.RisingEdge,
                InboundRisk.Safe,
                "Stop whatever is being read aloud.",
                _ => tts.CancelAllTts())
            {
                MinInterval = TimeSpan.FromMilliseconds(250),
            },

            new InboundCommand(
                "MCB/Ctrl/Panic",
                InboundTrigger.RisingEdge,
                InboundRisk.Safe,
                "Stop everything MagicChatbox is sending, and stop reading aloud.",
                _ =>
                {
                    tts.CancelAllTts();
                    appState.MasterSwitch = false;
                })
            {
                MinInterval = TimeSpan.FromMilliseconds(250),
            },
        };
    }
}
