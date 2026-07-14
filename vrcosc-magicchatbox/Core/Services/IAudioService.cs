using System.Collections.Generic;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Core.Services;

/// <summary>
/// Audio output device enumeration, TTS voice data, and log directory management.
/// </summary>
public interface IAudioService
{
    bool PopulateOutputDevices();
    List<Voice> ReadTikTokTTSVoices();
    void EnsureLogDirectoryExists(string filePath);
}
