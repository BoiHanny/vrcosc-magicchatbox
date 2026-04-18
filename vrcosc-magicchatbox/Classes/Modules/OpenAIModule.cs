using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Wraps the OpenAI SDK client, handles authentication, and implements speech-to-text transcription for the app.
/// </summary>
public class OpenAIModule : ITranscriptionService
{
    private readonly ISettingsProvider<OpenAISettings> _settingsProvider;
    private OpenAISettings Settings => _settingsProvider.Value;
    private void SaveSettings() => _settingsProvider.Save();

    private readonly OpenAIDisplayState _openAI;

    public OpenAIModule(ISettingsProvider<OpenAISettings> settingsProvider, OpenAIDisplayState openAI)
    {
        _settingsProvider = settingsProvider;
        _openAI = openAI;
    }

    private string CreateCustomOpenAIAccessErrorTxt(Exception ex)
    {
        if (ex.Message.Contains("Incorrect API"))
        {
            return "Invalid API key";
        }
        else if (ex.Message.Contains("No such organization"))
        {
            return "Invalid organization ID";
        }
        else if (ex.Message.Contains("500"))
        {
            return "Internal server error, try again later";
        }
        else if (ex.Message.Contains("503"))
        {
            return "Service unavailable, try again later";
        }
        else
        {
            return ex.Message;
        }
    }


    private void ReportTestConnectionError(Exception ex)
    {

        Logging.WriteException(ex, MSGBox: false);
        Settings.AccessTokenEncrypted = string.Empty;
        Settings.OrganizationIDEncrypted = string.Empty;
        Settings.AccessToken = string.Empty;
        Settings.OrganizationID = string.Empty;
        SaveSettings();
        _openAI.Connected = false;
        _openAI.AccessError = true;
        _openAI.AccessErrorTxt = CreateCustomOpenAIAccessErrorTxt(ex);
        OpenAIClient = null;
    }


    private async Task TestConnection()
    {
        try
        {
            var chatClient = OpenAIClient!.GetChatClient("gpt-4o-mini");
            var result = await chatClient.CompleteChatAsync(
                [new UserChatMessage("say: OK")],
                new ChatCompletionOptions { MaxOutputTokenCount = 1 });

            AuthChecked = result?.Value != null;

            if (!AuthChecked)
            {
                ReportTestConnectionError(new Exception("OpenAI connection test failed"));
            }
        }
        catch (Exception ex)
        {
            AuthChecked = false;
            ReportTestConnectionError(ex);
        }
    }

    /// <summary>
    /// Creates and tests an <see cref="OpenAIClient"/> with the supplied credentials, updating connection state on success or failure.
    /// </summary>
    public async Task InitializeClient(string apiKey, string organizationID)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return;
        }

        if (string.IsNullOrEmpty(organizationID))
        {
            return;
        }

        var options = new OpenAIClientOptions { OrganizationId = organizationID };
        OpenAIClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
        await TestConnection();
        _openAI.Connected = AuthChecked;
    }

    // ITranscriptionService implementation — zero-disk I/O via Stream
    public bool IsReady => OpenAIClient != null;

    public async Task<string?> TranscribeAsync(
        byte[] audioData,
        string audioFilename,
        string? model = null,
        string? language = null,
        CancellationToken ct = default)
    {
        if (OpenAIClient == null)
            throw new InvalidOperationException("OpenAI client is not initialized.");

        var audioClient = OpenAIClient.GetAudioClient(model ?? "whisper-1");

        var options = new AudioTranscriptionOptions();
        if (!string.IsNullOrEmpty(language))
            options.Language = language;

        using var stream = new MemoryStream(audioData);
        var result = await audioClient.TranscribeAudioAsync(
            stream, audioFilename, options, ct).ConfigureAwait(false);

        return result.Value.Text;
    }

    public bool AuthChecked { get; private set; } = false;

    public bool IsInitialized => OpenAIClient != null;
    public OpenAIClient? OpenAIClient { get; set; } = null;

}
