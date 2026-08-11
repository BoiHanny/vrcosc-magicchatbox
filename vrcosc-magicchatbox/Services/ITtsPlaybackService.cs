using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface ITtsPlaybackService
{
    Task PlayTtsAsync(string chat, bool resent = false);

    void CancelAllTts();
}
