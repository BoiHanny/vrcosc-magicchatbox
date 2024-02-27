using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class IntelliChatModuleSettings : ObservableObject
    {

        [ObservableProperty]
        private bool autolanguageSelection = true;

        [ObservableProperty]
        private bool intelliChatError = false;

        [ObservableProperty]
        private string intelliChatErrorTxt = string.Empty;

        [ObservableProperty]
        private bool intelliChatPerformModeration = true;

        [ObservableProperty]
        private int intelliChatPerformModerationTimeout = 7;

        [ObservableProperty]
        private int intelliChatTimeout = 10;

        [ObservableProperty]
        private string intelliChatTxt = string.Empty;

        [ObservableProperty]
        private bool intelliChatUILabel = false;

        [ObservableProperty]
        private string intelliChatUILabelTxt = string.Empty;

        [ObservableProperty]
        private bool intelliChatWaitingToAccept = false;

        [ObservableProperty]
        private List<int> selectedSupportedLanguageIDs = new List<int>();

        [ObservableProperty]
        private int selectedTranslateLanguageID;

        [ObservableProperty]
        private int selectedWritingStyleID;
        [ObservableProperty]
        private List<SupportedIntelliChatLanguage> supportedLanguages = new List<SupportedIntelliChatLanguage>();

        [ObservableProperty]
        private List<IntelliChatWritingStyle> supportedWritingStyles = new List<IntelliChatWritingStyle>();
    }

    public partial class SupportedIntelliChatLanguage : ObservableObject
    {
        [ObservableProperty]
        private int iD;

        [ObservableProperty]
        private bool isActivated;

        [ObservableProperty]
        private bool isBuiltIn = false;

        [ObservableProperty]
        private string language;
    }

    public partial class IntelliChatWritingStyle : ObservableObject
    {
        [ObservableProperty]
        private int iD;

        [ObservableProperty]
        private bool isActivated;

        [ObservableProperty]
        private bool isBuiltIn;

        [ObservableProperty]
        private string styleDescription;

        [ObservableProperty]
        private string styleName;

        [ObservableProperty]
        private double temperature;
    }

    public class IntelliChatModule
    {
        private const string IntelliChatSettingsFileName = "IntelliChatSettings.json";

        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static bool _isInitialized = false;
        public static IntelliChatModuleSettings Settings = new IntelliChatModuleSettings();

        public IntelliChatModule()
        {
            Initialize();
        }

        private bool EnsureInitialized()
        {
            if (!_isInitialized)
            {
                UpdateErrorState(true, "IntelliChat not initialized.");
                return false;
            }
            if (!OpenAIModule.Instance.IsInitialized)
            {
                UpdateErrorState(true, "OpenAI client not initialized.");
                return false;
            }

            return true;
        }

        private bool EnsureInitializedAndNotEmpty(string text)
        {
            if (!EnsureInitialized())
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return true;
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            LoadSettings();
            _isInitialized = true;
        }
        private void InitializeDefaultLanguageSettings()
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
        private void InitializeDefaultSettings()
        {
            InitializeDefaultLanguageSettings();
            InitializeDefaultWritingStyleSettings();

            SaveSettings();
        }
        private void InitializeDefaultWritingStyleSettings()
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

        private static void ProcessError(Exception ex)
        {

            if (ex is OperationCanceledException)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    // This block is entered either if the operation was manually cancelled
                    // or if it was cancelled due to a timeout.
                    // You might want to check if the cancellation was due to a timeout:
                    if (_cancellationTokenSource.Token.WaitHandle.WaitOne(0))
                    {
                        // Handle the timeout-specific logic here
                        UpdateErrorState(true, "The operation was cancelled due to a timeout.");
                        return;
                    }
                    return;

                }
            }

            Settings.IntelliChatError = true;
            Settings.IntelliChatErrorTxt = ex.Message;
        }

        public void CloseIntelliErrorPanel()
        {
            Settings.IntelliChatError = false;
            Settings.IntelliChatErrorTxt = string.Empty;
        }

        private void ProcessResponse(ChatResponse? response)
        {
            if (response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String)
            {
                Settings.IntelliChatUILabel = false;
                Settings.IntelliChatUILabelTxt = string.Empty;

                Settings.IntelliChatTxt = response.Choices[0].Message.Content.GetString();
                Settings.IntelliChatWaitingToAccept = true;

            }
            else
            {
                Settings.IntelliChatUILabel = false;
                Settings.IntelliChatUILabelTxt = string.Empty;

                Settings.IntelliChatTxt = response?.Choices?[0].Message.Content.ToString() ?? string.Empty;
                Settings.IntelliChatWaitingToAccept = true;
            }
        }

        private static void UpdateErrorState(bool hasError, string errorMessage)
        {
            Settings.IntelliChatError = hasError;
            Settings.IntelliChatErrorTxt = errorMessage;
        }

        public void AcceptIntelliChatSuggestion()
        {
            ViewModel.Instance.NewChattingTxt = Settings.IntelliChatTxt;
            Settings.IntelliChatTxt = string.Empty;
            Settings.IntelliChatWaitingToAccept = false;


        }
        public void AddWritingStyle(string styleName, string styleDescription, double temperature)
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

        public void CancelAllCurrentTasks()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }



        public async Task GenerateConversationStarterAsync()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            try
            {

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                var prompt = "Please generate a short a creative and engaging conversation starter of max 140 characters (this includes spaces), avoid AI and tech";

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint.GetCompletionAsync(new ChatRequest(new List<Message> { new Message(Role.System, prompt) }, maxTokens: 60), _cancellationTokenSource.Token);

                if (response == null)
                {
                    throw new InvalidOperationException("The response from OpenAI was empty");
                }
                else
                {
                    ProcessResponse(response);
                }
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }

        }
        public void LoadSettings()
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

        public async Task<bool> ModerationCheckPassedAsync(string text, bool cancelAllTasks = true)
        {
            if (cancelAllTasks)
                CancelAllCurrentTasks();

            if (!Settings.IntelliChatPerformModeration) return true;

            Settings.IntelliChatUILabel = true;
            Settings.IntelliChatUILabelTxt = "performing moderation check";

            try
            {
                ResetCancellationToken(Settings.IntelliChatPerformModerationTimeout);

                var moderationResponse = await OpenAIModule.Instance.OpenAIClient.ModerationsEndpoint.CreateModerationAsync(new ModerationsRequest(text), _cancellationTokenSource.Token);

                if (moderationResponse?.Results.Any(r => r.Flagged) ?? false)
                {
                    Settings.IntelliChatUILabel = false;

                    UpdateErrorState(true, "Your message has been temporarily held back due to a moderation check.\nThis is to ensure compliance with OpenAI's guidelines and protect your account.");
                    return false;
                }
                else
                {
                    Settings.IntelliChatUILabel = false;

                    UpdateErrorState(false, string.Empty);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                Settings.IntelliChatUILabel = false;

                UpdateErrorState(false, string.Empty);
                return true;
            }
        }


        public async Task PerformBeautifySentenceAsync(string text, IntelliChatWritingStyle intelliChatWritingStyle = null)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }

            if (!EnsureInitializedAndNotEmpty(text))
            {
                return;
            }

            try
            {
                if (!await ModerationCheckPassedAsync(text)) return;

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                intelliChatWritingStyle = intelliChatWritingStyle ?? SelectedWritingStyle;

                var messages = new List<Message>
                {
                    new Message(Role.System, $"Please rewrite the following sentence in {intelliChatWritingStyle.StyleDescription} style:")
                };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguageIDs.Count > 0)
                {
                    messages.Add(new Message(Role.System, $"Consider these languages: {string.Join(", ", SelectedSupportedLanguages)}"));
                }

                messages.Add(new Message(Role.User, text));

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120, temperature: intelliChatWritingStyle.Temperature), _cancellationTokenSource.Token);

                if (response == null)
                {
                    throw new InvalidOperationException("The response from OpenAI was empty");
                }

                ProcessResponse(response);
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }



        }



        public async Task PerformLanguageTranslationAsync(string text, SupportedIntelliChatLanguage supportedIntelliChatLanguage = null)
        {
            if (!EnsureInitializedAndNotEmpty(text))
            {
                return;
            }

            try
            {
                if (!await ModerationCheckPassedAsync(text)) return;

                SupportedIntelliChatLanguage intelliChatLanguage = supportedIntelliChatLanguage ?? SelectedTranslateLanguage;

                var messages = new List<Message>
                {
                    new Message(Role.System, $"Translate this to {intelliChatLanguage.Language}:"),
                    new Message(Role.User, text)
                };

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120), _cancellationTokenSource.Token);

                if (response == null)
                {
                    throw new InvalidOperationException("The response from OpenAI was empty");
                }
                else
                {
                    ProcessResponse(response);
                }

            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }


        }


        public async Task PerformSpellingAndGrammarCheckAsync(string text)
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }
            if (!EnsureInitializedAndNotEmpty(text))
            {
                return;
            }

            try
            {
                if (!await ModerationCheckPassedAsync(text)) return;

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

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

                ResetCancellationToken(Settings.IntelliChatTimeout);

                ChatResponse response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120), _cancellationTokenSource.Token);

                if (response == null)
                {
                    throw new InvalidOperationException("The response from OpenAI was empty");
                }
                else
                {
                    ProcessResponse(response);
                }
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }


        }

        public void RejectIntelliChatSuggestion()
        {
            Settings.IntelliChatTxt = string.Empty;
            Settings.IntelliChatWaitingToAccept = false;
        }
        public void RemoveWritingStyle(int styleId)
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

        public void ResetCancellationToken(int timeoutInSeconds)
        {
            CancelAllCurrentTasks();
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource.CancelAfter(timeoutInSeconds * 1000);
        }
        public void SaveSettings()
        {
            var filePath = Path.Combine(ViewModel.Instance.DataPath, IntelliChatSettingsFileName);
            var jsonData = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(filePath, jsonData);
        }

        public async Task ShortenTextAsync(string text, int retryCount = 0)
        {
            if (!EnsureInitializedAndNotEmpty(text))
            {
                return;
            }

            try
            {
                if (!await ModerationCheckPassedAsync(text)) return;

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                string prompt = retryCount == 0
                ? $"Shorten the following text to 140 characters or less, including spaces: {text}"
                : $"Please be more concise. Shorten this text to 140 characters or less, including spaces: {text}";

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(new List<Message>
                    {
            new Message(Role.System, prompt)
                    }, maxTokens: 60), _cancellationTokenSource.Token);

                var shortenedText = response?.Choices?[0].Message.Content.ValueKind == JsonValueKind.String
                    ? response.Choices[0].Message.Content.GetString()
                    : string.Empty;

                // Check if the response is still over 140 characters and retry if necessary
                if (shortenedText.Length > 140 && retryCount < 2) // Limiting to one retry
                {
                    await ShortenTextAsync(shortenedText, retryCount + 1);
                }
                else
                {
                    ProcessResponse(response);
                }
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }


        }

        public List<SupportedIntelliChatLanguage> SelectedSupportedLanguages
        {
            get
            {
                return Settings.SupportedLanguages.Where(lang => Settings.SelectedSupportedLanguageIDs.Contains(lang.ID)).ToList();
            }
        }
        public SupportedIntelliChatLanguage SelectedTranslateLanguage
        {
            get
            {
                return Settings.SupportedLanguages.FirstOrDefault(lang => lang.ID == Settings.SelectedTranslateLanguageID);
            }
        }

        public IntelliChatWritingStyle SelectedWritingStyle
        {
            get
            {
                return Settings.SupportedWritingStyles.FirstOrDefault(style => style.ID == Settings.SelectedWritingStyleID);
            }
        }

    }
}
