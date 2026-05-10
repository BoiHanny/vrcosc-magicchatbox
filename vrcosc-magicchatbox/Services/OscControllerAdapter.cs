using System;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Adapter that delegates to the DI-resolved OSCController instance.
/// Registered as a singleton in DI.
/// </summary>
public sealed class OscControllerAdapter : IOscController
{
    private readonly Lazy<OSCController> _osc;

    public OscControllerAdapter(OSCController osc)
    {
        // Wrap in Lazy to break potential circular init
        _osc = new Lazy<OSCController>(() => osc);
    }

    public void BuildOSC(bool allowExternalRefresh = true) => _osc.Value.BuildOSC(allowExternalRefresh);
    public void CreateChat(bool createItem, string? messageText = null) => _osc.Value.CreateChat(createItem, messageText);
}
