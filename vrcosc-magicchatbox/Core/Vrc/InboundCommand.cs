using System;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum InboundTrigger
{
    RisingEdge,
    Level,
}

public enum InboundRisk
{
    Safe,
    Moderate,
    High,
}

public sealed record InboundCommand(
    string Name,
    InboundTrigger Trigger,
    InboundRisk Risk,
    string Description,
    Action<bool> Invoke)
{
    public TimeSpan MinInterval { get; init; } = TimeSpan.FromMilliseconds(400);

    public string Address => AvatarParameter.AddressPrefix + Name;
}
