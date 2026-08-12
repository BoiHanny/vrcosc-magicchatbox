namespace vrcosc_magicchatbox.Services;

public interface IOscController
{
    void BuildOSC(bool allowExternalRefresh = true);
    void CreateChat(bool createItem, string? messageText = null);
}
