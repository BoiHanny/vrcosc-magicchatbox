using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

public sealed class ProxmoxOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _integrations;

    public ProxmoxOscProvider(Lazy<IModuleHost> modules, ISettingsProvider<IntegrationSettings> integrations)
    {
        _modules = modules;
        _integrations = integrations.Value;
    }

    public string SortKey => "Proxmox";
    public string UiKey => "Proxmox";
    public int Priority => 65;

    public bool IsEnabledForCurrentMode(bool isVRRunning) => _integrations.IntgrProxmox;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        ProxmoxModule? module = _modules.Value.Proxmox;
        if (module == null || string.IsNullOrWhiteSpace(module.StatusLine))
            return null;

        string text = module.StatusLine;
        if (text.Length > context.RemainingCharsIf(string.Empty))
            text = text[..Math.Max(0, context.RemainingCharsIf(string.Empty))].TrimEnd();

        return string.IsNullOrEmpty(text) ? null : new OscSegment { Text = text };
    }
}
