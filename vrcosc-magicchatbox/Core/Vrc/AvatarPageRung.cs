using System;

namespace vrcosc_magicchatbox.Core.Vrc;

public enum AvatarPageRung
{
    BridgeOff = 0,
    WaitingForVrchat = 1,
    AvatarUnknown = 2,
    AvatarKnown = 3,
}

public static class AvatarPageRungs
{
    public static AvatarPageRung Resolve(bool bridgeEnabled, bool running, bool vrchatFound, bool avatarKnown)
    {
        if (!bridgeEnabled || !running)
            return AvatarPageRung.BridgeOff;

        if (!vrchatFound)
            return AvatarPageRung.WaitingForVrchat;

        return avatarKnown ? AvatarPageRung.AvatarKnown : AvatarPageRung.AvatarUnknown;
    }

    public static string Describe(AvatarPageRung rung) => rung switch
    {
        AvatarPageRung.AvatarKnown => string.Empty,
        AvatarPageRung.AvatarUnknown =>
            "VRChat only announces your avatar when you put one on, so the name arrives when you next switch.",
        AvatarPageRung.WaitingForVrchat =>
            "Waiting for VRChat. Everything below comes from avatars you have worn before.",
        _ => "Switch on the avatar connection under Options, Avatar options, to see what you are wearing and control it from here.",
    };
}
