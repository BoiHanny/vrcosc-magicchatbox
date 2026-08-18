using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

public sealed class OscControllerAdapter : IOscController
{
    private readonly Lazy<OSCController> _osc;

    public OscControllerAdapter(OSCController osc)
    {
        _osc = new Lazy<OSCController>(() => osc);
    }

    public void BuildOSC(bool allowExternalRefresh = true) => _osc.Value.BuildOSC(allowExternalRefresh);
    public bool CreateChat(bool createItem, string? messageText = null) => _osc.Value.CreateChat(createItem, messageText);
}
