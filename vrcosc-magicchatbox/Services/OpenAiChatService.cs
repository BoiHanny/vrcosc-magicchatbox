using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Wraps <see cref="OpenAIModule.OpenAIClient"/> chat and moderation endpoints
/// using the official OpenAI .NET SDK (client-per-model pattern).
/// </summary>
public sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly OpenAIModule _openAi;

    public OpenAiChatService(OpenAIModule openAi)
    {
        _openAi = openAi ?? throw new ArgumentNullException(nameof(openAi));
    }

    public bool IsClientAvailable => _openAi.IsInitialized;

    public async Task<ChatCompletion?> GetChatCompletionAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatCompletionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!IsClientAvailable)
            throw new InvalidOperationException("OpenAI client is not initialized. Configure your API key first.");

        try
        {
            var chatClient = _openAi.OpenAIClient!.GetChatClient(model);
            var result = await chatClient.CompleteChatAsync(messages, options, ct).ConfigureAwait(false);
            return result.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
            throw;
        }
    }

    public async Task<ModerationResult?> ClassifyTextAsync(string text, string model, CancellationToken ct = default)
    {
        if (!IsClientAvailable)
            throw new InvalidOperationException("OpenAI client is not initialized. Configure your API key first.");

        try
        {
            var moderationClient = _openAi.OpenAIClient!.GetModerationClient(model);
            var result = await moderationClient.ClassifyTextAsync(text, ct).ConfigureAwait(false);
            return result.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logging.WriteException(ex, MSGBox: false);
            throw;
        }
    }
}
