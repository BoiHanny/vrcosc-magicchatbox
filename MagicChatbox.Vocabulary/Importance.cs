namespace MagicChatbox.Vocabulary;

/// <summary>
/// What a value <i>means</i>, not where an author happened to put it.
/// </summary>
/// <remarks>
/// This is the drop order when a composition will not fit VRChat's 144-character chatbox budget. v2 derived
/// priority from position in a layer list (<c>TimelineEngine.cs:636-654</c>), so reordering the UI
/// silently changed which fact survived truncation.
/// <para>
/// Here rather than in the kernel because <c>ModulePublication</c> names it, and the assembly a module
/// author compiles against may reference only this one. See <see cref="Temperament"/> for the argument.
/// </para>
/// </remarks>
public enum Importance : byte
{
    /// <summary>Dropped first. An emoji, a separator, a flourish.</summary>
    Decorative = 0,

    /// <summary>The normal case: content the user chose to show.</summary>
    Foreground = 1,

    /// <summary>Dropped last. A warning, a heart-rate alarm, a "recording" indicator.</summary>
    Critical = 2,
}
