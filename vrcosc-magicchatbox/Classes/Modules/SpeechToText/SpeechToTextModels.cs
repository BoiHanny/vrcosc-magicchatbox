using System.Collections.Generic;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules.SpeechToText;

public static class SpeechToTextModels
{
    public const IntelliGPTModel Recommended = IntelliGPTModel.gpt_transcribe;

    public static IReadOnlyList<IntelliGPTModel> Ordered { get; } = new[]
    {
        IntelliGPTModel.gpt_transcribe,
        IntelliGPTModel.gpt_4o_transcribe,
        IntelliGPTModel.gpt_4o_mini_transcribe,
        IntelliGPTModel.gpt_4o_transcribe_diarize,
        IntelliGPTModel.whisper1,
    };

    public static bool IsSupported(IntelliGPTModel model) => Ordered.Contains(model);

    public static IntelliGPTModel Resolve(IntelliGPTModel saved)
        => IsSupported(saved) ? saved : Recommended;
}
