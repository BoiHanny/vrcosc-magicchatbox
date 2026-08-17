using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Vrc;

public static class InboundCommandRegistry
{
    public const string ControlPrefix = "MCB/Ctrl/";

    public static IReadOnlyList<InboundCommand> Build(
        IAppState appState,
        ITtsPlaybackService tts,
        AppSettings appSettings,
        Func<AfkModule?> afk)
    {
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(tts);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(afk);

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

            new InboundCommand(
                "MCB/Ctrl/Afk",
                InboundTrigger.Level,
                InboundRisk.Safe,
                "Mark yourself away, and stop being away again.",
                value =>
                {
                    AfkModule? module = afk();

                    if (module?.Settings != null)
                        module.Settings.OverrideAfk = value != 0;
                })
            {
                MinInterval = TimeSpan.FromMilliseconds(500),
            },

            new InboundCommand(
                "MCB/Ctrl/Status/Cycle",
                InboundTrigger.Level,
                InboundRisk.Safe,
                "Start or stop cycling through your status messages.",
                value => appSettings.CycleStatus = value != 0)
            {
                MinInterval = TimeSpan.FromMilliseconds(500),
            },
        };
    }
}
