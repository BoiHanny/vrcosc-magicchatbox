using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{

    public partial class ModelTokenUsage : ObservableObject
    {
        [ObservableProperty]
        private string modelName;

        [ObservableProperty]
        private int promptTokens;

        [ObservableProperty]
        private int completionTokens;

        public int TotalTokens => PromptTokens + CompletionTokens;
    }

    public partial class DailyTokenUsage : ObservableObject
    {
        public DailyTokenUsage()
        {
            Date = DateTime.Today;
            ModelUsages = new ObservableCollection<ModelTokenUsage>();
        }

        [ObservableProperty]
        private DateTime date;

        public ObservableCollection<ModelTokenUsage> ModelUsages { get; set; }

        public int TotalDailyTokens => ModelUsages.Sum(mu => mu.TotalTokens);

        public int TotalDailyRequests => ModelUsages.Count;
    }

    public class TokenUsageData : ObservableObject
    {
        private int _lastRequestTotalTokens;

        public TokenUsageData()
        {
            DailyUsages = new ObservableCollection<DailyTokenUsage>();
        }

        public ObservableCollection<DailyTokenUsage> DailyUsages { get; set; }

        public int TotalDailyTokens => DailyUsages.LastOrDefault()?.TotalDailyTokens ?? 0;
        public int TotalDailyRequests => DailyUsages.LastOrDefault()?.TotalDailyRequests ?? 0;

        // Expose the last request's total tokens
        public int LastRequestTotalTokens => _lastRequestTotalTokens;

        public void AddTokenUsage(string modelName, int promptTokens, int completionTokens)
        {
            var today = DateTime.Today;
            var todayUsage = DailyUsages.FirstOrDefault(du => du.Date == today);

            if (todayUsage == null)
            {
                todayUsage = new DailyTokenUsage { Date = today };
                DailyUsages.Add(todayUsage);
            }

            var modelUsage = todayUsage.ModelUsages.FirstOrDefault(mu => mu.ModelName == modelName);
            if (modelUsage == null)
            {
                modelUsage = new ModelTokenUsage { ModelName = modelName };
                todayUsage.ModelUsages.Add(modelUsage);
            }

            modelUsage.PromptTokens += promptTokens;
            modelUsage.CompletionTokens += completionTokens;

            // Update the last request total tokens
            _lastRequestTotalTokens = promptTokens + completionTokens;

            // Notify UI about changes
            OnPropertyChanged(nameof(TotalDailyTokens));
            OnPropertyChanged(nameof(TotalDailyRequests));
            OnPropertyChanged(nameof(LastRequestTotalTokens));
        }
    }





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
        private List<SupportedIntelliChatLanguage> selectedSupportedLanguages = new List<SupportedIntelliChatLanguage>();

        [ObservableProperty]
        private SupportedIntelliChatLanguage selectedTranslateLanguage;

        [ObservableProperty]
        private IntelliChatWritingStyle selectedWritingStyle;
        [ObservableProperty]
        private List<SupportedIntelliChatLanguage> supportedLanguages = new List<SupportedIntelliChatLanguage>();

        [ObservableProperty]
        private List<IntelliChatWritingStyle> supportedWritingStyles = new List<IntelliChatWritingStyle>();

        [ObservableProperty]
        private TokenUsageData tokenUsageData = new TokenUsageData();
    }

    public partial class SupportedIntelliChatLanguage : ObservableObject
    {
        [ObservableProperty]
        private int iD;

        [ObservableProperty]
        private bool isBuiltIn = false;

        [ObservableProperty]
        private string language;


        [ObservableProperty]
        private bool isFavorite = false;
    }

    public partial class IntelliChatWritingStyle : ObservableObject
    {
        [ObservableProperty]
        private int iD;

        [ObservableProperty]
        private bool isBuiltIn;

        [ObservableProperty]
        private string styleDescription;

        [ObservableProperty]
        private string styleName;

        [ObservableProperty]
        private double temperature;

        [ObservableProperty]
        private bool isFavorite = false;
    }

    public partial class IntelliChatModule : ObservableObject
    {
        private const string IntelliChatSettingsFileName = "IntelliChatSettings.json";

        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static bool _isInitialized = false;

        [ObservableProperty]
        private IntelliChatModuleSettings settings = new IntelliChatModuleSettings();

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

        public void EnsureValidSelections()
        {
            // Update the selected writing style based on ID, ensuring it is part of the supported styles list.
            var selectedStyle = Settings.SelectedWritingStyle != null
                ? Settings.SupportedWritingStyles.FirstOrDefault(style => style.ID == Settings.SelectedWritingStyle.ID)
                : null;

            var selectedTranslateLanguage = Settings.SelectedTranslateLanguage != null
                ? Settings.SupportedLanguages.FirstOrDefault(lang => lang.ID == Settings.SelectedTranslateLanguage.ID)
                : null;

            Settings.SelectedWritingStyle = selectedStyle ?? Settings.SupportedWritingStyles.FirstOrDefault(style => style.IsBuiltIn);

            Settings.SelectedTranslateLanguage = selectedTranslateLanguage ?? Settings.SupportedLanguages.Where(lang => lang.Language.Equals("English", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }


        private void ProcessError(Exception ex)
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

            Settings.IntelliChatUILabel = false;
            Settings.IntelliChatUILabelTxt = string.Empty;

            UpdateErrorState(true, ex.Message);
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

                Settings.IntelliChatTxt = RemoveQuotationMarkAroundResponse(response.Choices[0].Message.Content.GetString());
                Settings.IntelliChatWaitingToAccept = true;

            }
            else
            {
                Settings.IntelliChatUILabel = false;
                Settings.IntelliChatUILabelTxt = string.Empty;

                Settings.IntelliChatTxt = RemoveQuotationMarkAroundResponse(response?.Choices?[0].Message.Content.ToString() ?? string.Empty);
                Settings.IntelliChatWaitingToAccept = true;
            }
            ProcessUsedTokens(response);
        }

        public void ProcessUsedTokens(ChatResponse response)
        {
            // Check if the response or its usage data is null
            if (response == null || response.Usage == null)
            {
                // Handle the case where there's no response or usage data
                Console.WriteLine("No response or usage data available.");
                return;
            }

            // Extracting the necessary information
            string modelName = response.Model; // Get the model name from the response
            int promptTokens = response.Usage.PromptTokens ?? 0; // Safely handle null with ?? operator
            int completionTokens = response.Usage.CompletionTokens ?? 0; // Safely handle null with ?? operator

            // Assuming TokenUsageDataInstance is your accessible TokenUsageData instance within the ViewModel
            // Update the token usage data for the specific model and day with the extracted information
            Settings.TokenUsageData.AddTokenUsage(modelName, promptTokens, completionTokens);
        }




        private void UpdateErrorState(bool hasError, string errorMessage)
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

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint.GetCompletionAsync(new ChatRequest(new List<Message> { new Message(Role.System, prompt) }, maxTokens: 60, temperature:2), _cancellationTokenSource.Token);

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

        private List<SupportedIntelliChatLanguage> GetDefaultLanguages()
        {
            return new List<SupportedIntelliChatLanguage>
            {
                new SupportedIntelliChatLanguage { ID = 1, Language = "English", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 2, Language = "Spanish", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 3, Language = "French", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 4, Language = "German", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 5, Language = "Chinese", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 6, Language = "Japanese", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 7, Language = "Russian", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 8, Language = "Portuguese", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 9, Language = "Italian", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 10, Language = "Dutch", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 11, Language = "Arabic", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 12, Language = "Turkish", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 13, Language = "Korean", IsBuiltIn = true, IsFavorite = true },
                new SupportedIntelliChatLanguage { ID = 14, Language = "Hindi", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 15, Language = "Swedish", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 16, Language = "Norwegian", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 17, Language = "Danish", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 18, Language = "Finnish", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 19, Language = "Greek", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 20, Language = "Hebrew", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 21, Language = "Polish", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 22, Language = "Czech", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 23, Language = "Thai", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 24, Language = "Indonesian", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 25, Language = "Malay", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 26, Language = "Vietnamese", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 27, Language = "Tagalog", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 28, Language = "Bengali", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 29, Language = "Tamil", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 30, Language = "Telugu", IsBuiltIn = true },
                new SupportedIntelliChatLanguage { ID = 31, Language = "Language no one understands", IsBuiltIn = true }
            };
        }

        private List<IntelliChatWritingStyle> GetDefaultWritingStyles()
        {
            return new List<IntelliChatWritingStyle>
            {
                new IntelliChatWritingStyle { ID = 1, StyleName = "Casual", Temperature = 0.6 },
                new IntelliChatWritingStyle { ID = 2, StyleName = "Formal", Temperature = 0.3 },
                new IntelliChatWritingStyle { ID = 3, StyleName = "Friendly", Temperature = 0.5 },
                new IntelliChatWritingStyle { ID = 4, StyleName = "Professional", Temperature = 0.4 },
                new IntelliChatWritingStyle { ID = 5, StyleName = "Academic", Temperature = 0.3 },
                new IntelliChatWritingStyle { ID = 6, StyleName = "Creative", Temperature = 1.2 },
                new IntelliChatWritingStyle { ID = 7, StyleName = "Humorous", Temperature = 1.1 },
                new IntelliChatWritingStyle { ID = 8, StyleName = "British", Temperature = 0.5 },
                new IntelliChatWritingStyle { ID = 9, StyleName = "Sarcastic", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 10, StyleName = "Romantic", Temperature = 0.6 },
                new IntelliChatWritingStyle { ID = 11, StyleName = "Action-Packed", Temperature = 1.2 },
                new IntelliChatWritingStyle { ID = 12, StyleName = "Mysterious", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 13, StyleName = "Sci-Fi", Temperature = 1.2 },
                new IntelliChatWritingStyle { ID = 14, StyleName = "Horror", Temperature = 1.1 },
                new IntelliChatWritingStyle { ID = 15, StyleName = "Western", Temperature = 0.6 },
                new IntelliChatWritingStyle { ID = 16, StyleName = "Fantasy", Temperature = 1.2 },
                new IntelliChatWritingStyle { ID = 17, StyleName = "Superhero", Temperature = 0.65 },
                new IntelliChatWritingStyle { ID = 18, StyleName = "Film Noir", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 19, StyleName = "Comedy", Temperature = 1.1 },
                new IntelliChatWritingStyle { ID = 20, StyleName = "Drama", Temperature = 0.7 },
                new IntelliChatWritingStyle { ID = 21, StyleName = "Risqué Humor", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 22, StyleName = "Flirty Banter", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 23, StyleName = "Sensual Poetry", Temperature = 0.7 },
                new IntelliChatWritingStyle { ID = 24, StyleName = "Bold Confessions", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 25, StyleName = "Seductive Fantasy", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 26, StyleName = "Candid Chronicles", Temperature = 0.7 },
                new IntelliChatWritingStyle { ID = 27, StyleName = "Playful Teasing", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 28, StyleName = "Intimate Reflections", Temperature = 0.65 },
                new IntelliChatWritingStyle { ID = 29, StyleName = "Enigmatic Erotica", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 30, StyleName = "Whimsical Winks", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 31, StyleName = "Lavish Desires", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 32, StyleName = "Cheeky Charm", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 33, StyleName = "Elegant Enticement", Temperature = 0.7 },
                new IntelliChatWritingStyle { ID = 34, StyleName = "Veiled Allusions", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 35, StyleName = "Rousing Revelations", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 36, StyleName = "Tantalizing Tales", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 37, StyleName = "Forbidden Fantasies", Temperature = 1.0 },
                new IntelliChatWritingStyle { ID = 38, StyleName = "Wicked Whispers", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 39, StyleName = "Coquettish Confidences", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 40, StyleName = "Thriller", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 41, StyleName = "Noir Romance", Temperature = 0.7 },
                new IntelliChatWritingStyle { ID = 42, StyleName = "Sensual Noir", Temperature = 0.8 },
                new IntelliChatWritingStyle { ID = 43, StyleName = "Seductive Noir", Temperature = 0.75 },
                new IntelliChatWritingStyle { ID = 44, StyleName = "Sultry Noir", Temperature = 0.7 }
            };
        }

        private void MergeOrUpdateBuiltInStylesAndLanguages()
        {
            var defaultLanguages = GetDefaultLanguages();
            var defaultStyles = GetDefaultWritingStyles();

            // Merge or update languages
            foreach (var lang in defaultLanguages)
            {
                if (!Settings.SupportedLanguages.Any(l => l.ID == lang.ID))
                {
                    Settings.SupportedLanguages.Add(lang);
                }
            }

            // Merge or update writing styles
            foreach (var style in defaultStyles)
            {
                var existingStyle = Settings.SupportedWritingStyles.FirstOrDefault(s => s.ID == style.ID);
                if (existingStyle == null)
                {
                    Settings.SupportedWritingStyles.Add(style);
                }
                else
                {
                    // Update existing built-in styles to ensure they are current
                    existingStyle.StyleName = style.StyleName;
                    existingStyle.Temperature = style.Temperature;
                    existingStyle.StyleDescription = style.StyleDescription;
                    // Mark as built-in in case it was changed
                    existingStyle.IsBuiltIn = true;
                }
            }

            // Optionally, save settings if needed
            SaveSettings();
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
                Settings = new IntelliChatModuleSettings();
            }

            MergeOrUpdateBuiltInStylesAndLanguages();
            EnsureValidSelections();
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

                intelliChatWritingStyle = intelliChatWritingStyle ?? Settings.SelectedWritingStyle;

                var messages = new List<Message>
                {
                    new Message(Role.System, $"Rewrite this sentence in {intelliChatWritingStyle.StyleDescription} and same length")
                };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    // Extracting the Language property from each SupportedIntelliChatLanguage object
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                    // Joining the language strings with commas
                    var languagesString = string.Join(", ", languages);

                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
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

                SupportedIntelliChatLanguage intelliChatLanguage = supportedIntelliChatLanguage ?? settings.SelectedTranslateLanguage;

                var messages = new List<Message>
                {
                    new Message(Role.System, $"Translate this to {intelliChatLanguage.Language}"),
                    new Message(Role.User, text)
                };

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(messages: messages, maxTokens: 120, temperature:0.3), _cancellationTokenSource.Token);

                if (response == null)
                {
                    Settings.IntelliChatUILabel = false;
                    throw new InvalidOperationException("The response from OpenAI was empty");
                }
                else
                {
                    Settings.IntelliChatUILabel = false;
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
                    "Please correct any spelling and grammar errors in the following text (return also if correct):")
                };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    // Extracting the Language property from each SupportedIntelliChatLanguage object
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                    // Joining the language strings with commas
                    var languagesString = string.Join(", ", languages);

                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
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
                ? $"Shorten ONLY the following text to 140 characters or less dont add anything, including spaces: {text}"
                : $"Please be more concise. Shorten ONLY this text to 140 characters or less don't add more into it, including spaces: {text}";

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

        private string RemoveQuotationMarkAroundResponse(string response)
        {
            if (!string.IsNullOrEmpty(response))
            {
                if (response.Length >= 2 && response[0] == '"' && response[response.Length - 1] == '"')
                {
                    return response.Substring(1, response.Length - 2);
                }
                else if (response[0] == '"')
                {
                    return response.Substring(1);
                }
                else if (response[response.Length - 1] == '"')
                {
                    return response.Substring(0, response.Length - 1);
                }
            }
            return response;
        }

        public async Task GenerateCompletionOrPredictionAsync(string inputText, bool isNextWordPrediction = false)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                UpdateErrorState(true, "Input text cannot be empty.");
                return;
            }

            if (!await ModerationCheckPassedAsync(inputText))
            {
                return;
            }

            try
            {
                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = isNextWordPrediction ? "Predicting next word..." : "Generating completion...";

                var promptMessage = isNextWordPrediction ? "Predict the next word." : "Complete the following text.";
                var messages = new List<Message>
            {
                new Message(Role.System, promptMessage),
                new Message(Role.User, inputText)
            };

                // Customizing ChatRequest for the task
                var chatRequest = new ChatRequest(
                    messages: messages,
                    model: "gpt-3.5-turbo", // Fast and efficient model
                    maxTokens: isNextWordPrediction ? 1 : 50, // Adjust based on the task
                    temperature: 0.7, // Fine-tune for creativity vs. randomness
                    topP: 1,
                    frequencyPenalty: 0.5, // Adjust as needed
                    presencePenalty: 0.5 // Adjust as needed
                );

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

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


    }
}
