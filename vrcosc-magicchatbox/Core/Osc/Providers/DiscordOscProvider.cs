using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Discord voice channel info → OSC segment.
/// Wraps <see cref="DiscordModule.GetOutputString"/>.
/// </summary>
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

        string text = discord.GetOutputString();
        if (string.IsNullOrWhiteSpace(text)) return null;

        return new OscSegment { Text = text };
    }
}
