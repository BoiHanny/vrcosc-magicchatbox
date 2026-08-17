using System;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum BridgeFirstRunOutcome
{
    Nothing = 0,
    EnabledForNewInstall = 1,
    NeedsIntroduction = 2,
}

public static class VrcBridgeFirstRun
{
    public static BridgeFirstRunOutcome Decide(VrcBridgeSettings settings, bool hadSettingsFile)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.BridgeIntroSeen)
            return BridgeFirstRunOutcome.Nothing;

        if (!hadSettingsFile)
            return BridgeFirstRunOutcome.EnabledForNewInstall;

        return settings.EnableBridge ? BridgeFirstRunOutcome.Nothing : BridgeFirstRunOutcome.NeedsIntroduction;
    }

    public static BridgeFirstRunOutcome Apply(VrcBridgeSettings settings, bool hadSettingsFile)
    {
        ArgumentNullException.ThrowIfNull(settings);

        BridgeFirstRunOutcome outcome = Decide(settings, hadSettingsFile);

        switch (outcome)
        {
            case BridgeFirstRunOutcome.EnabledForNewInstall:
                settings.EnableBridge = true;
                settings.EnableParameterInput = true;
                settings.BridgeIntroSeen = true;
                break;

            case BridgeFirstRunOutcome.Nothing:
                settings.BridgeIntroSeen = true;
                break;
        }

        return outcome;
    }
}
