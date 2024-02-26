using OpenAI.Chat;
using OpenAI.Moderations;
using OpenAI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json;
using System.IO;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class IntelliChatModuleSettings
    {
        public List<SupportedIntelliChatLanguage> SupportedLanguages { get; set; } = new List<SupportedIntelliChatLanguage>();
        public List<IntelliChatWritingStyle> SupportedWritingStyles { get; set; } = new List<IntelliChatWritingStyle>();
        public bool IntelliChatPerformModeration { get; set; } = true;
        public bool IntelliChatRequesting { get; set; } = false;
        public bool IntelliChatWaitingToAccept { get; set; } = false;
        public string IntelliChatTxt { get; set; } = string.Empty;
        public bool IntelliChatError { get; set; } = false;
        public string IntelliChatErrorTxt { get; set; } = string.Empty;

        // New fields

        public bool AutolanguageSelection { get; set; } = true;
        public List<int> SelectedSupportedLanguageIDs { get; set; } = new List<int>();
        public int SelectedTranslateLanguageID { get; set; }
        public int SelectedWritingStyleID { get; set; }
    }




    public class SupportedIntelliChatLanguage
    {
        public int ID { get; set; }
        public string Language { get; set; }
        public bool IsBuiltIn { get; set; } = false;
        public bool IsActivated { get; set; }
    }

    public class IntelliChatWritingStyle
    {
        public int ID { get; set; } // Unique identifier for each writing style
        public string StyleName { get; set; }
        public string StyleDescription { get; set; }
        public bool IsBuiltIn { get; set; } = false;
        public bool IsActivated { get; set; }
        public double Temperature { get; set; } = 0.7;
    }

    public class IntelliChatModule
    {
        private const string IntelliChatSettingsFileName = "IntelliChatSettings.json";
        public static IntelliChatModuleSettings Settings = new IntelliChatModuleSettings();
        private static bool _isInitialized = false;

        public  static IntelliChatWritingStyle SelectedWritingStyle
        {
            get
            {
                return Settings.SupportedWritingStyles.FirstOrDefault(style => style.ID == Settings.SelectedWritingStyleID);
            }
        }

        public static SupportedIntelliChatLanguage SelectedTranslateLanguage
        {
            get
            {
                return Settings.SupportedLanguages.FirstOrDefault(lang => lang.ID == Settings.SelectedTranslateLanguageID);
            }
        }

        public static List<SupportedIntelliChatLanguage> SelectedSupportedLanguages
        {
            get
            {
                return Settings.SupportedLanguages.Where(lang => Settings.SelectedSupportedLanguageIDs.Contains(lang.ID)).ToList();
            }
        }


        static IntelliChatModule()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_isInitialized) return;

            LoadSettings();
            _isInitialized = true;
        }

        private static void InitializeDefaultSettings()
        {
            InitializeDefaultLanguageSettings();
            InitializeDefaultWritingStyleSettings();

            SaveSettings();
        }

        public static void LoadSettings()
        {
            var filePath = Path.Combine(ViewModel.Instance.DataPath, IntelliChatSettingsFileName);
            if (File.Exists(filePath))
            {
                var jsonData = File.ReadAllText(filePath);
                Settings = JsonConvert.DeserializeObject<IntelliChatModuleSettings>(jsonData) ?? new IntelliChatModuleSettings();
            }
            else
            {
                InitializeDefaultSettings();
            }
        }

        public static void SaveSettings()
        {
            var filePath = Path.Combine(ViewModel.Instance.DataPath, IntelliChatSettingsFileName);
            var jsonData = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(filePath, jsonData);
        }



        private static void InitializeDefaultLanguageSettings()
        {
            var defaultLanguages = new List<SupportedIntelliChatLanguage>
            {
                new SupportedIntelliChatLanguage { Language = "English", IsBuiltIn = true, IsActivated = true },
                new SupportedIntelliChatLanguage { Language = "Spanish", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "French", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "German", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Chinese", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Japanese", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Russian", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Portuguese", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Italian", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Dutch", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Arabic", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Turkish", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Korean", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Hindi", IsBuiltIn = true, IsActivated = false },
                new SupportedIntelliChatLanguage { Language = "Swedish", IsBuiltIn = true, IsActivated = false },
            };

            Settings.SupportedLanguages = defaultLanguages;
        }

        private static void InitializeDefaultWritingStyleSettings()
        {
            var defaultStyles = new List<IntelliChatWritingStyle>
            {
                new IntelliChatWritingStyle { StyleName = "Casual", StyleDescription = "A casual, everyday writing style", IsBuiltIn = true, IsActivated = true },
                new IntelliChatWritingStyle { StyleName = "Formal", StyleDescription = "A formal, professional writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Friendly", StyleDescription = "A friendly, approachable writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Professional", StyleDescription = "A professional, business-oriented writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Academic", StyleDescription = "An academic, scholarly writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Creative", StyleDescription = "A creative, imaginative writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Humorous", StyleDescription = "A humorous, funny writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "British", StyleDescription = "A British, UK-specific writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Sarcastic", StyleDescription = "A sarcastic, witty writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Romantic", StyleDescription = "A romantic, lovey-dovey writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Action-Packed", StyleDescription = "An action-packed, adrenaline-fueled writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Mysterious", StyleDescription = "A mysterious, suspenseful writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Sci-Fi", StyleDescription = "A futuristic, sci-fi writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Horror", StyleDescription = "A chilling, horror writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Western", StyleDescription = "A wild west, cowboy writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Fantasy", StyleDescription = "A magical, fantasy writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Superhero", StyleDescription = "A heroic, superhero writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Film Noir", StyleDescription = "A dark, film noir writing style", IsBuiltIn = true, IsActivated = false },
                new IntelliChatWritingStyle { StyleName = "Comedy", StyleDescription = "A hilarious, comedy writing style", IsBuiltIn = true, IsActivated = false },
            };

            Settings.SupportedWritingStyles = defaultStyles;

        }







        public static async Task<string> PerformSpellingAndGrammarCheckAsync(string text)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }
            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            if (Settings.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if (moderationResponse)
                    return string.Empty;
            }


            var messages = new List<Message>
            {
                new Message(
                Role.System,
                "Please detect and correct and return any spelling and grammar errors in the following text:")
            };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguageIDs.Count > 0)
            {
                messages.Add(new Message(Role.System, $"Consider these languages: {string.Join(", ", SelectedSupportedLanguages)}"));
            }

            messages.Add(new Message(Role.User, text));

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120));

            // Check the type of response.Content and convert to string accordingly
            if (response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
            {
                return response.Choices[0].Message.Content.GetString();
            } else
            {
                // If it's not a string, use ToString() to get the JSON-formatted text
                return response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
            }
        }


        public static async Task<string> PerformBeautifySentenceAsync(string text, IntelliChatWritingStyle intelliChatWritingStyle = null)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }

            if(!_isInitialized || string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }


            if (Settings.IntelliChatPerformModeration)
            {
                bool moderationResponse = await PerformModerationCheckAsync(text);
                if (moderationResponse)
                    return string.Empty;
            }

            // Determine the writing style for beautification
            IntelliChatWritingStyle writingStyle = intelliChatWritingStyle ?? SelectedWritingStyle;

            var messages = new List<Message>
            {
                new Message(Role.System, $"Please rewrite the following sentence in {writingStyle.StyleDescription} style:")
            };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguageIDs.Count > 0)
            {
                messages.Add(new Message(Role.System, $"Consider these languages: {string.Join(", ", SelectedSupportedLanguages)}"));
            }

            messages.Add(new Message(Role.User, text));

            var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120,temperature: writingStyle.Temperature));

            if (response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
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

            if (!_isInitialized)
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            var prompt = "Please generate a short a creative and engaging conversation starter of max 140 characters (this includes spaces), avoid AI and tech";

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

            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
            {
                ViewModel.Instance.IntelliChatRequesting = false;
                return string.Empty;
            }

            if (Settings.IntelliChatPerformModeration)
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



        public static async Task<string> PerformLanguageTranslationAutoDetectAsync(string text, SupportedIntelliChatLanguage supportedIntelliChatLanguage = null)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                return string.Empty;
            }

            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
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

            // Determine the language for translation
            SupportedIntelliChatLanguage intelliChatLanguage = supportedIntelliChatLanguage ?? SelectedTranslateLanguage;

            var messages = new List<Message>
    {
        new Message(Role.System, $"Translate this to {intelliChatLanguage.Language}:"),
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
            ViewModel.Instance.NewChattingTxt = Settings.IntelliChatTxt;
            Settings.IntelliChatTxt = string.Empty;
            Settings.IntelliChatWaitingToAccept = false;

            
        }

        public static void RejectIntelliChatSuggestion()
        {
            Settings.IntelliChatTxt = string.Empty;
            Settings.IntelliChatWaitingToAccept = false;
        }

        public static async Task<bool> PerformModerationCheckAsync(string checkString)
        {
            var moderationResponse = await OpenAIModule.Instance.OpenAIClient.ModerationsEndpoint.CreateModerationAsync(new ModerationsRequest(checkString));

            // Check if the moderationResponse is null, indicating a failure in making the request
            if (moderationResponse == null)
            {
                // Handle the error appropriately
                // For example, you might log the error or set an error message in the ViewModel
                Settings.IntelliChatError = true;
                Settings.IntelliChatErrorTxt = "Error in moderation check.";
                return false;
            }

            // Check if there are any violations in the response
            if (moderationResponse.Results.Any(result => result.Flagged))
            {
                Settings.IntelliChatWaitingToAccept = false;
                Settings.IntelliChatRequesting = false;
                Settings.IntelliChatError = true;
                Settings.IntelliChatErrorTxt = "Your message has been temporarily held back due to a moderation check.\nThis is to ensure compliance with OpenAI's guidelines and protect your account.";
                return true;
            }

            // If there are no violations, return false
            return false;
        }




    }
}
