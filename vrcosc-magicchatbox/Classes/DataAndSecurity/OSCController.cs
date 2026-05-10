using System;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

/// <summary>
/// Thin orchestrator for OSC message building and chat management.
/// Business logic lives in IOscProvider implementations and ChatStateManager.
/// </summary>
public sealed class OSCController
{
    private readonly ChatStateManager _chatMgr;
    private readonly OscOutputBuilder _oscBuilder;
    private readonly OscBuildResultPresenter _oscPresenter;

    public OSCController(
        ChatStateManager chatMgr,
        OscOutputBuilder oscBuilder,
        OscBuildResultPresenter oscPresenter)
    {
        _chatMgr = chatMgr;
        _oscBuilder = oscBuilder;
        _oscPresenter = oscPresenter;
    }

    internal void ClearChat(ChatItem lastsendchat = null) => _chatMgr.ClearChat(lastsendchat);

    public void CreateChat(bool createItem) => _chatMgr.CreateChat(createItem);

    public void BuildOSC()
    {
        try
        {
            var result = _oscBuilder.Build();
            _oscPresenter.Present(result);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
