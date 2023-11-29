using OpenAI;
using System;

namespace vrcosc_magicchatbox.Classes.DataAndSecurity
{
    public class OpenAIManager
    {
        public OpenAIClient Client;

        private OpenAIManager()
        {
        }

        public static OpenAIManager Create(string apiKey, string organizationID)
        {
            OpenAIManager StartedAIManager = new OpenAIManager();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }
            bool HasBeenSet = StartedAIManager.SetClient(new OpenAIClient(new OpenAIAuthentication(apiKey, organizationID)));
            if (HasBeenSet)
            {
                return StartedAIManager;
            }
            else
            {
                return null;
            }
        }

        public bool SetClient(OpenAIClient client)
        {
            try
            {
                Client = client;
                return true;

            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
                return false;
            }

        }
    }
}
