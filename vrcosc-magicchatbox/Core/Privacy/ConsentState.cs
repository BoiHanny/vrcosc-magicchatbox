using System.ComponentModel;

namespace vrcosc_magicchatbox.Core.Privacy;

/// <summary>
/// Whether the user has answered for one hook yet, and what they said.
/// </summary>
/// <remarks>
/// The descriptions are what the permission rows show. "Unknown" in particular read as an error
/// rather than as the ordinary state of a question nobody has been asked yet.
/// </remarks>
public enum ConsentState
{
    [Description("Not asked yet")]
    Unknown = 0,

    [Description("Allowed")]
    Approved = 1,

    [Description("Blocked")]
    Denied = 2,
}
