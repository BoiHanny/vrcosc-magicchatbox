using OpenAI.Chat;
using OpenAI.Moderations;
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

            if(ViewModel.Instance.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if(moderationResponse)
                    return string.Empty;
            }


            var messages = new List<Message>
            {
                new Message(
                Role.System,
                "Please detect and correct and return any spelling and grammar errors in the following text:")
            };

            if(languages != null && languages.Any() && !ViewModel.Instance.IntelliChatAutoLang)
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

            if (ViewModel.Instance.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if (moderationResponse)
                    return string.Empty;
            }

            var messages = new List<Message>
            {
                new Message(Role.System, $"Please rewrite the following sentence in a {writingStyle} style:")
            };

            if(languages != null && languages.Any() && !ViewModel.Instance.IntelliChatAutoLang)
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

        public static async Task<string> GenerateConversationStarterAsync()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return "OpenAI not initialized.";
            }

            var prompt = "Please generate a short a creative and engaging conversation starter of max 140 characters (this includes spaces), avoid AI. no '";

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(new List<Message>
                {
            new Message(Role.System, prompt)
                }, maxTokens: 60));

            return response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String
                ? response.Choices[0].Message.Content.GetString()
                : "Unable to generate conversation starter.";
        }

        public static async Task<string> ShortenTextAsync(string text, int retryCount = 0)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return "OpenAI not initialized.";
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            if (ViewModel.Instance.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if (moderationResponse)
                    return string.Empty;
            }

            string prompt = retryCount == 0
                ? $"Shorten the following text to 140 characters or less, including spaces: {text}"
                : $"Please be more concise. Shorten this text to 140 characters or less, including spaces: {text}";

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(new List<Message>
                {
            new Message(Role.System, prompt)
                }, maxTokens: 60));

            var shortenedText = response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String
                ? response.Choices[0].Message.Content.GetString()
                : string.Empty;

            // Check if the response is still over 140 characters and retry if necessary
            if (shortenedText.Length > 140 && retryCount < 1) // Limiting to one retry
            {
                return await ShortenTextAsync(shortenedText, retryCount + 1);
            }
            else
            {
                return shortenedText.Length <= 140 ? shortenedText : string.Empty;
            }
        }



        public static async Task<string> PerformLanguageTranslationAutoDetectAsync(
            string text,
            SupportedIntelliChatLanguage targetLanguage = SupportedIntelliChatLanguage.English)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            if (ViewModel.Instance.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if (moderationResponse)
                    return string.Empty;
            }

            var messages = new List<Message>
    {
        new Message(Role.System, $"Translate this to {targetLanguage}:"),
        new Message(Role.User, text)
    };

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120));

            return response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String
                ? response.Choices[0].Message.Content.GetString()
                : response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
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

        public static async Task<bool> PerformModerationCheckAsync(string checkString)
        {
            var moderationResponse = await OpenAIModule.Instance.OpenAIClient.ModerationsEndpoint.CreateModerationAsync(new ModerationsRequest(checkString));

            // Check if the moderationResponse is null, indicating a failure in making the request
            if (moderationResponse == null)
            {
                // Handle the error appropriately
                // For example, you might log the error or set an error message in the ViewModel
                ViewModel.Instance.IntelliChatError = true;
                ViewModel.Instance.IntelliChatErrorTxt = "Error in moderation check.";
                return false;
            }

            // Check if there are any violations in the response
            if (moderationResponse.Results.Any(result => result.Flagged))
            {
                ViewModel.Instance.IntelliChatWaitingToAccept = false;
                ViewModel.Instance.IntelliChatRequesting = false;
                ViewModel.Instance.IntelliChatError = true;
                ViewModel.Instance.IntelliChatErrorTxt = "Your message has been temporarily held back due to a moderation check.\nThis is to ensure compliance with OpenAI's guidelines and protect your account.";
                return true;
            }

            // If there are no violations, return false
            return false;
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
