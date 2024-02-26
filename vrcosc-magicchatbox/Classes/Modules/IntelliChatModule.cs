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
using System;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class IntelliChatModuleSettings
    {
        public List<SupportedIntelliChatLanguage> SupportedLanguages { get; set; } = new List<SupportedIntelliChatLanguage>();
        public List<IntelliChatWritingStyle> SupportedWritingStyles { get; set; } = new List<IntelliChatWritingStyle>();
        public bool IntelliChatPerformModeration { get; set; } = true;
        public bool IntelliChatRequesting { get; set; } = false;
        public string IntelliChatRequestingText { get; set; } = string.Empty;
        public bool IntelliChatWaitingToAccept { get; set; } = false;
        public string IntelliChatTxt { get; set; } = string.Empty;
        public bool IntelliChatError { get; set; } = false;
        public string IntelliChatErrorTxt { get; set; } = string.Empty;
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
        public int ID { get; set; }
        public string StyleName { get; set; }
        public string StyleDescription { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsActivated { get; set; }
        public double Temperature { get; set; }
    }
    public class IntelliChatModule
    {
        private const string IntelliChatSettingsFileName = "IntelliChatSettings.json";
        public static IntelliChatModuleSettings Settings = new IntelliChatModuleSettings();
        private static bool _isInitialized = false;
        public static IntelliChatWritingStyle SelectedWritingStyle
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
        new IntelliChatWritingStyle { ID = 1, StyleName = "Casual", StyleDescription = "A casual, everyday writing style", Temperature = 0.6, IsBuiltIn = true, IsActivated = true },
        new IntelliChatWritingStyle { ID = 2, StyleName = "Formal", StyleDescription = "A formal, professional writing style", Temperature = 0.3, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 3, StyleName = "Friendly", StyleDescription = "A friendly, approachable writing style", Temperature = 0.5, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 4, StyleName = "Professional", StyleDescription = "A professional, business-oriented writing style", Temperature = 0.4, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 5, StyleName = "Academic", StyleDescription = "An academic, scholarly writing style", Temperature = 0.3, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 6, StyleName = "Creative", StyleDescription = "A creative, imaginative writing style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 7, StyleName = "Humorous", StyleDescription = "A humorous, funny writing style", Temperature = 0.9, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 8, StyleName = "British", StyleDescription = "A British, UK-specific writing style", Temperature = 0.5, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 9, StyleName = "Sarcastic", StyleDescription = "A sarcastic, witty writing style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 10, StyleName = "Romantic", StyleDescription = "A romantic, lovey-dovey writing style", Temperature = 0.6, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 11, StyleName = "Action-Packed", StyleDescription = "An action-packed, adrenaline-fueled writing style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 12, StyleName = "Mysterious", StyleDescription = "A mysterious, suspenseful writing style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 13, StyleName = "Sci-Fi", StyleDescription = "A futuristic, sci-fi writing style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 14, StyleName = "Horror", StyleDescription = "A chilling, horror writing style", Temperature = 0.85, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 15, StyleName = "Western", StyleDescription = "A wild west, cowboy writing style", Temperature = 0.6, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 16, StyleName = "Fantasy", StyleDescription = "A magical, fantasy writing style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 17, StyleName = "Superhero", StyleDescription = "A heroic, superhero writing style", Temperature = 0.65, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 18, StyleName = "Film Noir", StyleDescription = "A dark, film noir writing style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 19, StyleName = "Comedy", StyleDescription = "A hilarious, comedy writing style", Temperature = 0.9, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 20, StyleName = "Drama", StyleDescription = "A dramatic, tear-jerking writing style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 21, StyleName = "Risqué Humor", StyleDescription = "A bold, cheeky humor style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 22, StyleName = "Flirty Banter", StyleDescription = "A playful, flirtatious banter style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 23, StyleName = "Sensual Poetry", StyleDescription = "A deeply sensual, poetic style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 24, StyleName = "Bold Confessions", StyleDescription = "A daring, confessional narrative style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 25, StyleName = "Seductive Fantasy", StyleDescription = "A seductive, enchanting fantasy style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 26, StyleName = "Candid Chronicles", StyleDescription = "A frank, revealing chronicle style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 27, StyleName = "Playful Teasing", StyleDescription = "A light-hearted, teasing narrative style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 28, StyleName = "Intimate Reflections", StyleDescription = "A deeply personal, intimate reflection style", Temperature = 0.65, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 29, StyleName = "Enigmatic Erotica", StyleDescription = "A mysterious, subtly erotic style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 30, StyleName = "Whimsical Winks", StyleDescription = "A whimsical, suggestive wink style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 31, StyleName = "Lavish Desires", StyleDescription = "A richly descriptive, desire-filled style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 32, StyleName = "Cheeky Charm", StyleDescription = "A cheeky, charmingly persuasive style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 33, StyleName = "Elegant Enticement", StyleDescription = "An elegantly enticing, sophisticated style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 34, StyleName = "Veiled Allusions", StyleDescription = "A subtly allusive, veiled narrative style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 35, StyleName = "Rousing Revelations", StyleDescription = "A stimulating, revelation-rich style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 36, StyleName = "Tantalizing Tales", StyleDescription = "A tantalizing, teasing tale style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 37, StyleName = "Forbidden Fantasies", StyleDescription = "A style rich with forbidden fantasies", Temperature = 0.85, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 38, StyleName = "Wicked Whispers", StyleDescription = "A wickedly whispering, secretive style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 39, StyleName = "Coquettish Confidences", StyleDescription = "A coquettish, confidently playful style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 40, StyleName = "Thriller", StyleDescription = "A thrilling, suspenseful writing style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 41, StyleName = "Noir Romance", StyleDescription = "A sultry, mysterious romance style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 42, StyleName = "Sensual Noir", StyleDescription = "A dark, sensual noir style", Temperature = 0.8, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 43, StyleName = "Seductive Noir", StyleDescription = "A seductive, mysterious noir style", Temperature = 0.75, IsBuiltIn = true, IsActivated = false },
        new IntelliChatWritingStyle { ID = 44, StyleName = "Sultry Noir", StyleDescription = "A sultry, mysterious noir style", Temperature = 0.7, IsBuiltIn = true, IsActivated = false }
    };

            Settings.SupportedWritingStyles = defaultStyles;
        }
        public static void AddWritingStyle(string styleName, string styleDescription, double temperature)
        {
            // Find the next available ID starting from 1000 for user-defined styles
            int nextId = Settings.SupportedWritingStyles.DefaultIfEmpty().Max(style => style?.ID ?? 999) + 1;
            if (nextId < 1000) nextId = 1000; // Ensure starting from 1000

            // Check if the style name already exists
            if (Settings.SupportedWritingStyles.Any(style => style.StyleName.Equals(styleName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A writing style with the name \"{styleName}\" already exists.");
            }

            // Add the new writing style
            var newStyle = new IntelliChatWritingStyle
            {
                ID = nextId,
                StyleName = styleName,
                StyleDescription = styleDescription,
                Temperature = temperature,
                IsBuiltIn = false, // User-defined styles are not built-in
                IsActivated = true // Assuming new styles are activated by default
            };
            Settings.SupportedWritingStyles.Add(newStyle);

            SaveSettings(); // Save the updated settings
        }
        public static void RemoveWritingStyle(int styleId)
        {
            var styleToRemove = Settings.SupportedWritingStyles.FirstOrDefault(style => style.ID == styleId);
            if (styleToRemove == null)
            {
                throw new InvalidOperationException($"No writing style found with ID {styleId}.");
            }

            if (styleToRemove.IsBuiltIn)
            {
                throw new InvalidOperationException("Built-in writing styles cannot be removed.");
            }

            Settings.SupportedWritingStyles.Remove(styleToRemove);
            SaveSettings(); // Save the updated settings
        }

        private static void UpdateErrorState(bool hasError, string errorMessage)
        {
            Settings.IntelliChatError = hasError;
            Settings.IntelliChatErrorTxt = errorMessage;
        }

        private static bool EnsureInitializedAndNotEmpty(string text)
        {
            if (!_isInitialized)
            {
                UpdateErrorState(true, "IntelliChat not initialized.");
                return false;
            }
            if(OpenAIModule.Instance.IsInitialized)
            {
                UpdateErrorState(true, "OpenAI client not initialized.");
                return false;
            }
            if(string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return true;
        }

        public static async Task<bool> ModerationCheckAsync(string text)
        {
            if (!Settings.IntelliChatPerformModeration)
            {
                return true;
            }

            bool x = EnsureInitializedAndNotEmpty(text);
            var moderationResponse = await OpenAIModule.Instance.OpenAIClient.ModerationsEndpoint.CreateModerationAsync(new ModerationsRequest(text));

            if (moderationResponse?.Results.Any(r => r.Flagged) ?? false)
            {
                UpdateErrorState(true, "Your message has been temporarily held back due to a moderation check.\nThis is to ensure compliance with OpenAI's guidelines and protect your account.");
            }

            UpdateErrorState(false, "");
        }
        

        public static async Task PerformSpellingAndGrammarCheckAsync(string text)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }
            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                Settings.IntelliChatRequesting = true;

                if (Settings.IntelliChatPerformModeration)
                {
                    Settings.IntelliChatRequestingText = "performing moderation check";
                    bool moderationResponse = await PerformModerationCheckAsync(text);
                        moderationResponse: return;
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

                //catch any errors and throw an exception
                if (response == null)
                {
                    throw new InvalidOperationException("The response from OpenAI was null.");
                }

                // Check the type of response.Content and convert to string accordingly
                if (response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
                {
                    Settings.IntelliChatTxt = response.Choices[0].Message.Content.GetString();
                    Settings.IntelliChatWaitingToAccept = true;

                }
                else
                {
                    // If it's not a string, use ToString() to get the JSON-formatted text
                    Settings.IntelliChatTxt = response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
                    Settings.IntelliChatWaitingToAccept = true;
                }
            }
            catch (Exception ex)
            {
                Settings.IntelliChatError = true;
                Settings.IntelliChatErrorTxt = ex.Message;
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




    }
}
