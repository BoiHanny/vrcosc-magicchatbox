using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Manages TTS audio playback lifecycle (fetch, play, cancel).
/// </summary>
public interface ITtsPlaybackService
{
    /// <summary>
    /// Fetches TTS audio from the TikTok API and plays it on the selected output device.
    /// </summary>
    Task PlayTtsAsync(string chat, bool resent = false);

    /// <summary>
    /// Cancels all currently playing TTS audio.
    /// </summary>
    void CancelAllTts();
}
