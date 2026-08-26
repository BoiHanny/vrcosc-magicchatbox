using System.Collections.Generic;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.Core.Services;

public interface IAudioService
{
    bool PopulateOutputDevices();

    void InvalidateOutputDeviceCache();
    List<Voice>? ReadTikTokTTSVoices();
    void EnsureLogDirectoryExists(string filePath);
}
