namespace MagicChatbox.Kernel;

/// <summary>
/// Who may write a key, and by what path.
/// </summary>
/// <remarks>
/// This and <see cref="DescriptorSource"/> are the two facets that stayed in the kernel when
/// <c>Temperament</c>, <c>Unit</c> and <c>Importance</c> moved down into <c>MagicChatbox.Vocabulary</c>
/// for the module SDK's sake. They are the descriptor layer's own policy vocabulary — one is about the
/// write path and the other about precedence between declarers — and no signature a module author sees
/// names either, so moving them would have widened the SDK's surface for nobody.
/// </remarks>
public enum WriteSafety : byte
{
    /// <summary>
    /// Only the observation path may write this cell; <c>Mutate</c> is rejected. The cell reflects
    /// external truth, and asserting it locally would make the store claim something nobody confirmed.
    /// </summary>
    ObservedOnly,

    /// <summary>Anyone with a grant may write it.</summary>
    Writable,

    /// <summary>Assistant, Rule and Module writes are rejected; User, System and Restore may write.</summary>
    /// <remarks>
    /// The line through the middle is whether a person is present to be asked. A rule is on the rejected
    /// side despite being authored by the user, because it fires unattended long after it was written;
    /// a restore is on the permitted side despite being automatic, because it is that same person's saved
    /// state coming back.
    /// </remarks>
    Privileged,
}

/// <summary>
/// Which layer declared a descriptor. A flat rank, and an <c>int</c> comparison decides conflicts.
/// </summary>
/// <remarks>
/// The difference between "last writer wins by accident of startup order" and a defined outcome. v2
/// spent 610 lines on a registry with a precedence table, layer diagnostics and a conflict-monitor
/// hosted service, and still only <i>logged</i> conflicts — which is why v2 has keys in production
/// whose declared type disagrees with their received type.
/// <para>
/// <see cref="OscQuery"/> outranks <see cref="AvatarConfig"/>: the config file is what was uploaded,
/// the OSCQuery tree is what the running client actually has.
/// </para>
/// </remarks>
public enum DescriptorSource : byte
{
    /// <summary>The kernel's fallback for a key nobody described.</summary>
    Default = 0,

    /// <summary>A module declaring its own keys. Deliberately the lowest real rank.</summary>
    Module = 1,

    /// <summary>Heuristic classification of an avatar parameter by name.</summary>
    Classification = 2,

    /// <summary>The avatar's uploaded OSC config JSON.</summary>
    AvatarConfig = 3,

    /// <summary>The running client's advertised OSCQuery tree.</summary>
    OscQuery = 4,

    /// <summary>The user said so. Nothing outranks a person.</summary>
    User = 5,
}
