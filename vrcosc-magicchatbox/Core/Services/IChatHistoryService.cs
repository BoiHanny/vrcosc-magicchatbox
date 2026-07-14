namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Manages chat message history persistence (LastMessages.json).
/// Load populates ChatStatusDisplayState.LastMessages.
/// Save serializes the current shared state.
/// </summary>
public interface IChatHistoryService
{
    void LoadChatHistory();
    void SaveChatHistory();
}
