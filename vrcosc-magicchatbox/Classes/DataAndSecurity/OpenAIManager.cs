using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class OpenAIManager
    {
        private static readonly Lazy<OpenAIManager> instance = new Lazy<OpenAIManager>(() => new OpenAIManager());
        public OpenAIClient OpenAIClient { get; private set; } = null;
        public bool AuthChecked { get; private set; } = false;

        public bool IsInitialized => OpenAIClient != null;

        private OpenAIManager()
        {

        }

        public static OpenAIManager Instance => instance.Value;

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
                var testMessage = new Message(Role.User, "Connection validation, say OK");

                var responseMessage = await OpenAIClient.ChatEndpoint.GetCompletionAsync(new ChatRequest(messages: new List<Message> { testMessage }, maxTokens:1));

                AuthChecked = responseMessage != null;

                if(!AuthChecked)
                {
                    ReportTestConnectionError(new Exception("OpenAI connection test failed"));
                }
            }
            catch(Exception ex)
            {
                AuthChecked = false;
                ReportTestConnectionError(ex);
            }
        }


        private void ReportTestConnectionError(Exception ex)
        {

                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
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
            if(ex.Message.Contains("Incorrect API"))
            {
                return "Invalid API key";
            }
            else if(ex.Message.Contains("No such organization"))
            {
                return "Invalid organization ID";
            }
            else if(ex.Message.Contains("500"))
            {
                return "Internal server error, try again later";
            }
            else if(ex.Message.Contains("503"))
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
