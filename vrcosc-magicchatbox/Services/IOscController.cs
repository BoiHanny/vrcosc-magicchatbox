namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstraction over OSCController for building and composing OSC messages.
/// Enables constructor injection and testability.
/// </summary>
public interface IOscController
{
    void BuildOSC();
    void CreateChat(bool createItem);
}
