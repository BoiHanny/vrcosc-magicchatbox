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
        public static async Task<string> PerformSpellingAndGrammarCheckAsync(
            string text,
            List<SupportedIntelliChatLanguage> languages = null)
        {
            if(!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }
            if(string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }


            var messages = new List<Message>
            {
                new Message(
                Role.System,
                "Please detect and correct any spelling and grammar errors in the following text:")
            };

            if(languages != null && languages.Any())
            {
                messages.Add(new Message(Role.System, $"Consider these languages: {string.Join(", ", languages)}"));
            }

            messages.Add(new Message(Role.User, text));

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120));

            // Check the type of response.Content and convert to string accordingly
            if(response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
            {
                return response.Choices[0].Message.Content.GetString();
            } else
            {
                // If it's not a string, use ToString() to get the JSON-formatted text
                return response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
            }
        }


        public static async Task<string> PerformBeautifySentenceAsync(
            string text,
            IntelliChatWritingStyle writingStyle = IntelliChatWritingStyle.Casual,
            List<SupportedIntelliChatLanguage> languages = null)
        {
            if(!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }
            if(string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            var messages = new List<Message>
            {
                new Message(Role.System, $"Please rewrite the following sentence in a {writingStyle} style:")
            };

            if(languages != null && languages.Any())
            {
                messages.Add(new Message(Role.System, $"Consider these languages: {string.Join(", ", languages)}"));
            }

            messages.Add(new Message(Role.User, text));

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120));

            if(response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
            {
                return response.Choices[0].Message.Content.GetString();
            } else
            {
                return response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
            }
        }

        public static void AcceptIntelliChatSuggestion()
        {
            ViewModel.Instance.NewChattingTxt = ViewModel.Instance.IntelliChatTxt;
            ViewModel.Instance.IntelliChatTxt = string.Empty;
            ViewModel.Instance.IntelliChatWaitingToAccept = false;
        }

        public static void RejectIntelliChatSuggestion()
        {
            ViewModel.Instance.IntelliChatTxt = string.Empty;
            ViewModel.Instance.IntelliChatWaitingToAccept = false;
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

    public enum IntelliChatWritingStyle
    {
        Casual,
        Formal,
        Friendly,
        Professional,
        Academic,
        Creative,
        Humorous,
        British,
    }
}
