using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Toast;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Wraps <see cref="OpenAIModule.OpenAIClient"/> chat and moderation endpoints
/// using the official OpenAI .NET SDK (client-per-model pattern).
/// </summary>
public sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly OpenAIModule _openAi;
    private readonly IPrivacyConsentService _consent;

    public OpenAiChatService(OpenAIModule openAi, IPrivacyConsentService consent)
    {
        _openAi = openAi ?? throw new ArgumentNullException(nameof(openAi));
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
    }

    public bool IsClientAvailable => _openAi.IsInitialized;

    public bool CanUseOpenAi => _openAi.IsInitialized && _consent.IsApproved(PrivacyHook.InternetAccess);

    /// <summary>
    /// Surfaces the same permission message other modules show when Internet Access
    /// consent is denied, so a null result doesn't read as a broken OpenAI response.
    /// </summary>
    private static void NotifyConsentDenied()
    {
        App.Services?.GetService<IToastService>()?.Show(
            "🔒 OpenAI",
            "Internet Access permission required for OpenAI features.",
            ToastType.Privacy,
            key: "openai-privacy-denied");
    }

    public async Task<ChatCompletion?> GetChatCompletionAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        ChatCompletionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!_consent.IsApproved(PrivacyHook.InternetAccess))
        {
            NotifyConsentDenied();
            return null;
        }

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
        if (!_consent.IsApproved(PrivacyHook.InternetAccess))
        {
            NotifyConsentDenied();
            return null;
        }

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
