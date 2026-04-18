using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstraction over OSCSender for sending OSC messages to VRChat.
/// Enables constructor injection and testability.
/// </summary>
public interface IOscSender
{
    Task SendOSCMessage(bool fx, int delay = 0);
    void SendOscParam(string address, float value);
    void SendOscParam(string address, int value);
    void SendOscParam(string address, bool value);
    void SendTypingIndicatorAsync();
    Task SentClearMessage(int delay);
    Task ToggleVoice(bool force = false);
}
