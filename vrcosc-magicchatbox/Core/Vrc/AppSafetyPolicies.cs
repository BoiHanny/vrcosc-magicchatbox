using MagicChatbox.Vrc;
using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Core.Vrc;

public sealed class AppWorldPolicy : IWorldPolicy
{
    private readonly VrcBridgeSettings _settings;
    private readonly Func<string?> _currentWorld;
    private readonly Func<bool> _isPublicInstance;

    public AppWorldPolicy(VrcBridgeSettings settings, Func<string?> currentWorld, Func<bool> isPublicInstance)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _currentWorld = currentWorld ?? throw new ArgumentNullException(nameof(currentWorld));
        _isPublicInstance = isPublicInstance ?? throw new ArgumentNullException(nameof(isPublicInstance));
    }

    public bool IsCurrentWorldMuted
    {
        get
        {
            try
            {
                if (_settings.MuteInPublicInstances && _isPublicInstance())
                    return true;

                string? world = _currentWorld();
                if (string.IsNullOrWhiteSpace(world))
                    return false;

                return _settings.MutedWorlds.Any(
                    muted => !string.IsNullOrWhiteSpace(muted)
                             && world.Contains(muted, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}

public sealed class AppProfanityPolicy : IProfanityPolicy
{
    private readonly VrcBridgeSettings _settings;

    public AppProfanityPolicy(VrcBridgeSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool Blocks(string text, out string? term)
    {
        term = null;

        if (string.IsNullOrEmpty(text))
            return false;

        foreach (string blocked in _settings.BlockedTerms)
        {
            if (string.IsNullOrWhiteSpace(blocked))
                continue;

            if (text.Contains(blocked, StringComparison.OrdinalIgnoreCase))
            {
                term = blocked;
                return true;
            }
        }

        return false;
    }
}
