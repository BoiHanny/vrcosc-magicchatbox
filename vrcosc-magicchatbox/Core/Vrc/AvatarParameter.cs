namespace vrcosc_magicchatbox.Core.Vrc;

public enum AvatarParameterKind
{
    Bool,
    Int,
    Float,
    Pulse,
}

public enum AvatarParameterFlow
{
    AppToAvatar,
    AvatarToApp,
    Bidirectional,
}

public enum AvatarParameterTier
{
    Legacy,
    Compatibility,
    Synced,
    Local,
    Control,
}

public sealed record AvatarParameter(
    string Name,
    AvatarParameterKind Kind,
    AvatarParameterFlow Flow,
    AvatarParameterTier Tier,
    string Range,
    string Source,
    string Gate,
    string Notes = "")
{
    public const string AddressPrefix = "/avatar/parameters/";

    public string Address => AddressPrefix + Name;
}
