using System;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Soundpad currently-playing song → OSC segment.
/// Wraps <see cref="SoundpadModule.GetPlayingSong"/>.
/// </summary>
public sealed class SoundpadOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;
    private readonly AppSettings _app;

    public SoundpadOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<AppSettings> appProvider)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
        _app = appProvider.Value;
    }

    public string SortKey => "Soundpad";
    public string UiKey => "Soundpad";
    public int Priority => 75;

    public bool IsEnabledForCurrentMode(bool isVR)
        => isVR ? _intgr.IntgrSoundpad_VR : _intgr.IntgrSoundpad_DESKTOP;

    public OscSegment? TryBuild(OscBuildContext context)
    {
        if (!_intgr.IntgrSoundpad) return null;

        string playingSong = _modules.Value.Soundpad?.GetPlayingSong();
        if (string.IsNullOrEmpty(playingSong)) return null;

        string text = _app.PrefixIconSoundpad
            ? $"🎶 '{playingSong}'"
            : $"'{playingSong}'";

        return new OscSegment { Text = text };
    }
}
