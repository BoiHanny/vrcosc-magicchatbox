using System;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Thin orchestrator for OSC message building and chat management.
/// Business logic lives in IOscProvider implementations and ChatStateManager.
/// </summary>
public sealed class OSCController
{
    private readonly ChatStateManager _chatMgr;
    private readonly OscOutputBuilder _oscBuilder;
    private readonly OscDisplayState _oscDisplay;
    private readonly IntegrationDisplayState _integrationDisplay;

    public OSCController(
        ChatStateManager chatMgr,
        OscOutputBuilder oscBuilder,
        OscDisplayState oscDisplay,
        IntegrationDisplayState integrationDisplay)
    {
        _chatMgr = chatMgr;
        _oscBuilder = oscBuilder;
        _oscDisplay = oscDisplay;
        _integrationDisplay = integrationDisplay;
    }

    internal void ClearChat(ChatItem lastsendchat = null) => _chatMgr.ClearChat(lastsendchat);

    public void CreateChat(bool createItem) => _chatMgr.CreateChat(createItem);

    public void BuildOSC()
    {
        try
        {
            var result = _oscBuilder.Build();
            OscOutputBuilder.ApplyToDisplay(result, _oscDisplay, _integrationDisplay);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
