using System;
using vrcosc_magicchatbox.Core.Osc;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity;

public sealed class OSCController
{
    private readonly ChatStateManager _chatMgr;
    private readonly OscOutputBuilder _oscBuilder;
    private readonly OscBuildResultPresenter _oscPresenter;
    private readonly System.Threading.Lock _buildGate = new();

    public OSCController(
        ChatStateManager chatMgr,
        OscOutputBuilder oscBuilder,
        OscBuildResultPresenter oscPresenter)
    {
        _chatMgr = chatMgr;
        _oscBuilder = oscBuilder;
        _oscPresenter = oscPresenter;
    }

    internal void ClearChat(ChatItem? lastsendchat = null) => _chatMgr.ClearChat(lastsendchat);

    public bool CreateChat(bool createItem, string? messageText = null) => _chatMgr.CreateChat(createItem, messageText);

    public OscBuildResult? Build(bool allowExternalRefresh = true)
    {
        try
        {
            lock (_buildGate)
            {
                return _oscBuilder.Build(allowExternalRefresh);
            }
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public void Present(OscBuildResult? result)
    {
        if (result == null)
            return;

        try
        {
            _oscPresenter.Present(result);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void BuildOSC(bool allowExternalRefresh = true) => Present(Build(allowExternalRefresh));
}
