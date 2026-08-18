using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class OscBuildResultPresenter
{
    private static readonly IReadOnlyCollection<string> NothingLive = Array.Empty<string>();

    private readonly OscDisplayState _oscDisplay;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly Lazy<IAppState> _appState;

    public OscBuildResultPresenter(
        OscDisplayState oscDisplay,
        IntegrationDisplayState integrationDisplay,
        Lazy<IAppState> appState)
    {
        _oscDisplay = oscDisplay;
        _integrationDisplay = integrationDisplay;
        _appState = appState;
    }

    public void Present(OscBuildResult result)
    {
        _integrationDisplay.ResetAllOpacity();

        var liveKeys = _appState.Value.MasterSwitch
            ? result.IncludedProviders
            : NothingLive;

        if (!SequenceEqualsIgnoreCase(_integrationDisplay.LiveOutputKeys, liveKeys))
            _integrationDisplay.LiveOutputKeys = liveKeys;

        if (result.ExceededLimit)
        {
            if (!SequenceEqualsIgnoreCase(_integrationDisplay.TrimmedOutputKeys, result.TrimmedProviders))
                _integrationDisplay.TrimmedOutputKeys = result.TrimmedProviders;

            foreach (var key in result.TrimmedProviders)
                _integrationDisplay.SetOpacity(key, "0.5");
        }
        else
        {
            if (!SequenceEqualsIgnoreCase(_integrationDisplay.TrimmedOutputKeys, NothingLive))
                _integrationDisplay.TrimmedOutputKeys = NothingLive;
        }

        if (result.Length > OscBuildContext.MaxOscLength)
        {
            _oscDisplay.OscToSent = string.Empty;
            _oscDisplay.OscMsgCount = result.Length;
            _oscDisplay.OscMsgCountUI = $"MAX/{OscBuildContext.MaxOscLength}";
        }
        else
        {
            _oscDisplay.OscToSent = result.Message;
            _oscDisplay.OscMsgCount = result.Length;
            _oscDisplay.OscMsgCountUI = $"{result.Length}/{OscBuildContext.MaxOscLength}";
        }
    }

    private static bool SequenceEqualsIgnoreCase(IReadOnlyCollection<string> current, IReadOnlyCollection<string> incoming)
    {
        if (ReferenceEquals(current, incoming))
            return true;

        if (current.Count != incoming.Count)
            return false;

        return current.SequenceEqual(incoming, StringComparer.OrdinalIgnoreCase);
    }
}
