using OpenAI.Chat;
using OpenAI.Moderations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface IOpenAiChatService
{
    Task<ChatCompletion?> GetChatCompletionAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatCompletionOptions? options = null,
        CancellationToken ct = default);

    Task<ModerationResult?> ClassifyTextAsync(string text, string model, CancellationToken ct = default);

    bool IsClientAvailable { get; }

    bool CanUseOpenAi { get; }
}
