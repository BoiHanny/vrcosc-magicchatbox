using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Applies OSC build results to WPF-bound display state.
/// Keeps OscOutputBuilder focused on producing the Chatbox Message.
/// </summary>
public sealed class OscBuildResultPresenter
{
    private readonly OscDisplayState _oscDisplay;
    private readonly IntegrationDisplayState _integrationDisplay;

    public OscBuildResultPresenter(
        OscDisplayState oscDisplay,
        IntegrationDisplayState integrationDisplay)
    {
        _oscDisplay = oscDisplay;
        _integrationDisplay = integrationDisplay;
    }

    public void Present(OscBuildResult result)
    {
        _integrationDisplay.ResetAllOpacity();

        if (result.ExceededLimit)
        {
            _oscDisplay.CharLimit = "Visible";
            foreach (var key in result.TrimmedProviders)
                _integrationDisplay.SetOpacity(key, "0.5");
        }
        else
        {
            _oscDisplay.CharLimit = "Hidden";
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
