using System.Text.RegularExpressions;

namespace MagicChatbox.Vrc;

/// <summary>Who can get into an instance, in the words VRChat's own UI uses.</summary>
public enum VrcInstanceAccess
{
    Unknown = 0,
    Public = 1,
    FriendsPlus = 2,
    Friends = 3,
    InvitePlus = 4,
    Invite = 5,
    Group = 6,
}

/// <summary>
/// One instance, taken apart: the world, the instance number, who may enter, and where it runs.
/// </summary>
public readonly record struct VrcInstance(
    string WorldId,
    string InstanceId,
    VrcInstanceAccess Access,
    string Region)
{
    public static readonly VrcInstance None = new(string.Empty, string.Empty, VrcInstanceAccess.Unknown, string.Empty);

    public bool IsKnown => WorldId.Length > 0;

    public bool IsPublic => Access == VrcInstanceAccess.Public;

    /// <summary>The name a person would recognise for <see cref="Access"/>.</summary>
    public string AccessName => Access switch
    {
        VrcInstanceAccess.Public => "Public",
        VrcInstanceAccess.FriendsPlus => "Friends+",
        VrcInstanceAccess.Friends => "Friends",
        VrcInstanceAccess.InvitePlus => "Invite+",
        VrcInstanceAccess.Invite => "Invite",
        VrcInstanceAccess.Group => "Group",
        _ => "Unknown",
    };
}

/// <summary>
/// Reads VRChat's instance key out of a log line, and takes it apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>The token grammar is the part that gets this wrong.</b> An instance key is
/// <c>wrld_&lt;id&gt;:&lt;number&gt;</c> followed by any number of <c>~</c> tokens, and those tokens come in
/// two shapes: <c>~region(eu)</c> carries a value and <c>~canRequestInvite</c> does not. A pattern that
/// only accepts the first shape stops matching at the first bare token and silently keeps whatever came
/// before it — so a key carrying <c>~ageGate</c> loses its region, its nonce, and any access token that
/// followed, and the instance reads as Public because the <c>~private</c> token was never seen.
/// </para>
/// <para>
/// <b>Absence of an access token means Public</b>, which is why a truncated key is dangerous rather than
/// merely incomplete: the failure direction is toward the most open reading.
/// </para>
/// </remarks>
public static class VrcInstanceKey
{
    private static readonly Regex JoiningPattern = new(
        @"Joining (wrld_[a-fA-F0-9\-]+:[0-9]+(?:~[A-Za-z]+(?:\([^)]*\))?)*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TokenPattern = new(
        @"~([A-Za-z]+)(?:\(([^)]*)\))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The instance key in this log line, or empty when it carries none.</summary>
    public static string ReadFromJoiningLine(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        Match match = JoiningPattern.Match(line);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>Takes a raw instance key apart. Returns <see cref="VrcInstance.None"/> for anything unparseable.</summary>
    public static VrcInstance Parse(string? instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
            return VrcInstance.None;

        string key = instanceKey.Trim();

        int colon = key.IndexOf(':');
        if (colon <= 0 || colon == key.Length - 1)
            return VrcInstance.None;

        string worldId = key[..colon];
        string rest = key[(colon + 1)..];

        int firstToken = rest.IndexOf('~');
        string instanceId = firstToken < 0 ? rest : rest[..firstToken];

        var access = VrcInstanceAccess.Public;
        string region = string.Empty;
        bool canRequestInvite = false;
        bool sawPrivate = false;

        foreach (Match token in TokenPattern.Matches(rest))
        {
            string name = token.Groups[1].Value;

            switch (name.ToLowerInvariant())
            {
                case "region":
                    region = token.Groups[2].Value;
                    break;
                case "hidden":
                    access = VrcInstanceAccess.FriendsPlus;
                    break;
                case "friends":
                    access = VrcInstanceAccess.Friends;
                    break;
                case "private":
                    sawPrivate = true;
                    break;
                case "group":
                    access = VrcInstanceAccess.Group;
                    break;
                case "canrequestinvite":
                    canRequestInvite = true;
                    break;
            }
        }

        if (sawPrivate)
            access = canRequestInvite ? VrcInstanceAccess.InvitePlus : VrcInstanceAccess.Invite;

        return new VrcInstance(worldId, instanceId, access, region);
    }

    /// <summary>
    /// The world id alone, folded so that two spellings of one world cannot miss each other.
    /// </summary>
    /// <remarks>
    /// Applied to both the live reading and to anything a person saved earlier, or a world added to a
    /// group on one day stops matching itself on another.
    /// </remarks>
    public static string BaseWorldId(string? worldIdOrInstanceKey)
    {
        if (string.IsNullOrWhiteSpace(worldIdOrInstanceKey))
            return string.Empty;

        string text = worldIdOrInstanceKey.Trim();

        int colon = text.IndexOf(':');
        if (colon > 0)
            text = text[..colon];

        return text.ToLowerInvariant();
    }
}
