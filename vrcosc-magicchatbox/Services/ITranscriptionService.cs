using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface ITranscriptionService
{
    bool IsReady { get; }

    Task<string?> TranscribeAsync(
        byte[] audioData,
        string audioFilename,
        string? model = null,
        string? language = null,
        CancellationToken ct = default);
}
