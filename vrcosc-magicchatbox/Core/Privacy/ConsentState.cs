using System.ComponentModel;

namespace vrcosc_magicchatbox.Core.Privacy;

public enum ConsentState
{
    [Description("Not asked yet")]
    Unknown = 0,

    [Description("Allowed")]
    Approved = 1,

    [Description("Blocked")]
    Denied = 2,
}
