using System;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Puts a line into the VRChat chatbox while it is still being typed.
/// </summary>
public interface ILiveTypingService
{
    /// <summary>
    /// Raised when a held line has sat untouched long enough to count as finished.
    /// </summary>
    /// <remarks>
    /// An event rather than a call into the send path: this service knows when someone stopped
    /// typing and nothing else, and giving it the ability to post messages would make every future
    /// change to sending have to reason about it too. Raised off a timer thread.
    /// </remarks>
    event Action? FinalizeRequested;

    /// <summary>
    /// True while an unsent line owns the chatbox. The scan loop leaves the chatbox alone for as
    /// long as this holds, otherwise the next integration tick would type over the person.
    /// </summary>
    bool IsHolding { get; }

    /// <summary>Report the current contents of the chat input.</summary>
    void Show(string text);

    /// <summary>
    /// Give the chatbox back to the integrations. <paramref name="clearChatbox"/> wipes what is
    /// showing; pass false when a real message is about to replace it anyway.
    /// </summary>
    void Release(bool clearChatbox);
}
