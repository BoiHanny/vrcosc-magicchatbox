using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.SpeechToText;

/// <summary>
/// The transcription models offered in the app, in the order they should be shown.
///
/// The list lives in code and is rebuilt every launch, so updating the app updates the choices -
/// nothing about it is kept in a settings file except which one you picked. Only models the audio
/// transcription endpoint accepts belong here: OpenAI's realtime transcription models
/// (gpt-live-transcribe, gpt-realtime-whisper) are for streaming sessions, and this module records a
/// clip and uploads it, so offering them would only produce failures.
/// </summary>
public static class SpeechToTextModels
{
    /// <summary>OpenAI's current recommendation for transcribing recorded speech.</summary>
    public const IntelliGPTModel Recommended = IntelliGPTModel.gpt_transcribe;

    /// <summary>Recommended first, then the models it superseded, then whisper.</summary>
    public static IReadOnlyList<IntelliGPTModel> Ordered { get; } = new[]
    {
        IntelliGPTModel.gpt_transcribe,
        IntelliGPTModel.gpt_4o_transcribe,
        IntelliGPTModel.gpt_4o_mini_transcribe,
        IntelliGPTModel.gpt_4o_transcribe_diarize,
        IntelliGPTModel.whisper1,
    };

    public static bool IsSupported(IntelliGPTModel model) => Ordered.Contains(model);

    /// <summary>
    /// The model to actually use. A saved choice is kept whenever it is still offered, including the
    /// older ones - they work, and silently moving somebody off a model they chose would be worse
    /// than leaving them on it. Anything no longer offered, or a value left over from an enum that
    /// has since changed, falls back to the recommendation rather than failing every transcription.
    /// </summary>
    public static IntelliGPTModel Resolve(IntelliGPTModel saved)
        => IsSupported(saved) ? saved : Recommended;
}
