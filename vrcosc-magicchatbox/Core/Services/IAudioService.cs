using System.Collections.Generic;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Core.Services;

public interface IAudioService
{
    bool PopulateOutputDevices();

    /// <summary>Forgets the cached device list, so the next ask goes back to the audio stack.</summary>
    void InvalidateOutputDeviceCache();
    List<Voice> ReadTikTokTTSVoices();
    void EnsureLogDirectoryExists(string filePath);
}
