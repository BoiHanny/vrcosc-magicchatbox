using System;
using System.Collections.Generic;
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

        // The build runs whether or not anything is being sent, so the master switch decides this
        // rather than the build does. With it off nothing reaches VRChat, and no integration should
        // be claiming otherwise.
        _integrationDisplay.LiveOutputKeys = _appState.Value.MasterSwitch
            ? result.IncludedProviders
            : NothingLive;

        if (result.ExceededLimit)
        {
            _integrationDisplay.TrimmedOutputKeys = result.TrimmedProviders;

            foreach (var key in result.TrimmedProviders)
                _integrationDisplay.SetOpacity(key, "0.5");
        }
        else
        {
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
}
