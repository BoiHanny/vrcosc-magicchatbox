using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class DiscordOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;

    public DiscordOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
    }

    public string SortKey => "Discord";
    public string UiKey => "Discord";
    public int Priority => 45;

    public bool IsEnabledForCurrentMode(bool isVR)
        => _intgr.IntgrDiscord
           && (isVR ? _intgr.IntgrDiscord_VR : _intgr.IntgrDiscord_DESKTOP);

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var discord = _modules.Value.Discord;
        if (discord == null || !discord.IsRunning || !discord.IsAuthenticated) return null;

        // The channel name, the nicknames and the template are all somebody else's text. Telling
        // the module how much room is actually left is what stops it filling the line on its own.
        int budget = context.RemainingCharsIf(string.Empty);
        if (budget <= 0) return null;

        string text = discord.GetOutputString(budget);
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
