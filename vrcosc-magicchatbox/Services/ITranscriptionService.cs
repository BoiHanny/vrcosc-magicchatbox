using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstracts audio-to-text transcription so modules don't depend on the OpenAI client directly.
/// Zero-disk-I/O: accepts raw WAV bytes in memory.
/// </summary>
public interface ITranscriptionService
{
    bool IsReady { get; }

    /// <summary>
    /// Transcribes audio from an in-memory WAV byte array. No temp files are created.
    /// </summary>
    Task<string?> TranscribeAsync(
        byte[] audioData,
        string audioFilename,
        string? model = null,
        string? language = null,
        CancellationToken ct = default);
}
