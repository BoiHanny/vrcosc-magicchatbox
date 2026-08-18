namespace vrcosc_magicchatbox.Services;

public interface IOscController
{
    void BuildOSC(bool allowExternalRefresh = true);
    bool CreateChat(bool createItem, string? messageText = null);
}
