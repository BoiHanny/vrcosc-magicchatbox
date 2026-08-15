namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Puts a line into the VRChat chatbox while it is still being typed.
/// </summary>
public interface ILiveTypingService
{
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
