using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using vrcosc_magicchatbox.ViewModels;
using System.Windows.Threading;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class IntelliChatModule
    {
        public static async Task<string> PerformSpellingAndGrammarCheckAsync(string text, List<SupportedIntelliChatLanguage> languages = null)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }


                var promptBuilder = new StringBuilder();

            // Create a prompt indicating the possible languages
            promptBuilder.AppendLine("Detect and correct any spelling and grammar errors in the following text, return only correct text");
            if (languages != null && languages.Any())
            {
                promptBuilder.AppendLine($"Possible languages: {string.Join(", ", languages)}.");
            }
            promptBuilder.AppendLine($"Text: \"{text}\"");

            string prompt = promptBuilder.ToString();

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint.GetCompletionAsync(
                new ChatRequest(messages: new List<Message> { new Message(Role.System, prompt) }, maxTokens: 120));

            // Check the type of response.Content and convert to string accordingly
            if (response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
            {
                return response.Choices[0].Message.Content.GetString();
            }
            else
            {
                // If it's not a string, use ToString() to get the JSON-formatted text
                return response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
            }
        }

    }

    public enum SupportedIntelliChatLanguage
    {
        English,
        Spanish,
        French,
        German,
        Chinese,
        Japanese,
        Russian,
        Portuguese,
        Italian,
        Dutch,
        Arabic,
        Turkish,
        Korean,
        Hindi,
        Swedish,
    }
}
