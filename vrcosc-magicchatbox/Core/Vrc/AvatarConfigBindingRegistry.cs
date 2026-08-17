using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed record AvatarConfigOption(string Parameter, string Description);

public static class AvatarConfigBindingRegistry
{
    public const string Sending = AvatarConfigBinding.Prefix + "Sending";
    public const string HeartRate = AvatarConfigBinding.Prefix + "HeartRate";
    public const string Media = AvatarConfigBinding.Prefix + "Media";
    public const string WindowActivity = AvatarConfigBinding.Prefix + "WindowActivity";
    public const string Status = AvatarConfigBinding.Prefix + "Status";

    public static readonly IReadOnlyList<AvatarConfigOption> Options = new AvatarConfigOption[]
    {
        new(Sending, "Hold this off to stop MagicChatbox sending anything at all while you wear this avatar."),
        new(HeartRate, "Hold this off to keep your heart rate off the chatbox while you wear this avatar."),
        new(Media, "Hold this off to keep what you are listening to off the chatbox while you wear this avatar."),
        new(WindowActivity, "Hold this off to keep the app you have open off the chatbox while you wear this avatar."),
        new(Status, "Hold this off to keep your personal status off the chatbox while you wear this avatar."),
    };

    public static IReadOnlyList<AvatarConfigBinding> Build(
        IAppState appState,
        IntegrationSettings integrations)
    {
        ArgumentNullException.ThrowIfNull(appState);
        ArgumentNullException.ThrowIfNull(integrations);

        var applies = new Dictionary<string, Action<bool>>(StringComparer.Ordinal)
        {
            [Sending] = on => appState.MasterSwitch = on,
            [HeartRate] = on => integrations.IntgrHeartRate = on,
            [Media] = on => integrations.IntgrScanMediaLink = on,
            [WindowActivity] = on => integrations.IntgrScanWindowActivity = on,
            [Status] = on => integrations.IntgrStatus = on,
        };

        return Options
            .Where(o => applies.ContainsKey(o.Parameter))
            .Select(o => new AvatarConfigBinding(
                o.Parameter, o.Description, ConfigDirection.OffOnly, applies[o.Parameter]))
            .ToList();
    }
}
