using OpenAI.Chat;
using OpenAI.Moderations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Abstracts OpenAI chat completion and moderation API calls.
/// Uses the official OpenAI .NET SDK (client-per-model pattern).
/// </summary>
public interface IOpenAiChatService
{
    /// <summary>
    /// Sends a chat completion request using the specified model.
    /// </summary>
    Task<ChatCompletion?> GetChatCompletionAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatCompletionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a moderation check on the given text using the specified model.
    /// </summary>
    Task<ModerationResult?> ClassifyTextAsync(string text, string model, CancellationToken ct = default);

    /// <summary>
    /// Whether the OpenAI client is initialized and ready to accept requests.
    /// </summary>
    bool IsClientAvailable { get; }
}
