using NAudio.Wave;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class OpenAIModule
    {


        private static readonly Lazy<OpenAIModule> instance = new Lazy<OpenAIModule>(() => new OpenAIModule());
        public OpenAIClient OpenAIClient { get; set; } = null;
        public bool AuthChecked { get; private set; } = false;

        public bool IsInitialized => OpenAIClient != null;

        private OpenAIModule()
        { }

        public static OpenAIModule Instance => instance.Value;

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

            OpenAIClient = new OpenAIClient(new OpenAIAuthentication(apiKey, organizationID));
            await TestConnection();
            ViewModel.Instance.OpenAIConnected = AuthChecked;
        }


        private async Task TestConnection()
        {
            try
            {
                var testMessage = new Message(Role.User, "say: OK");

                var responseMessage = await OpenAIClient.ChatEndpoint.GetCompletionAsync(new ChatRequest(messages: new List<Message> { testMessage }, maxTokens: 1));

                AuthChecked = responseMessage != null;

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

        public async Task<string> TranscribeAudioToText(string audioFilePath)
        {
            // Ensure the OpenAI client is initialized
            if (OpenAIClient == null)
            {
                throw new InvalidOperationException("OpenAI client is not initialized.");
            }

            // Create a request for audio transcription
            var request = new AudioTranscriptionRequest(Path.GetFullPath(audioFilePath), language: "en");

            // Call the AudioEndpoint to transcribe the audio
            var response = await OpenAIClient.AudioEndpoint.CreateTranscriptionAsync(request);

            // The response is expected to be a string containing the transcription
            return response; // Directly return the response
        }


        private void ReportTestConnectionError(Exception ex)
        {

            Logging.WriteException(ex, MSGBox: false);
            ViewModel.Instance.OpenAIAccessTokenEncrypted = string.Empty;
            ViewModel.Instance.OpenAIOrganizationIDEncrypted = string.Empty;
            ViewModel.Instance.OpenAIAccessToken = string.Empty;
            ViewModel.Instance.OpenAIOrganizationID = string.Empty;
            ViewModel.Instance.OpenAIConnected = false;
            ViewModel.Instance.OpenAIAccessError = true;
            ViewModel.Instance.OpenAIAccessErrorTxt = CreateCustomOpenAIAccessErrorTxt(ex);
            OpenAIClient = null;
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

    }
}
