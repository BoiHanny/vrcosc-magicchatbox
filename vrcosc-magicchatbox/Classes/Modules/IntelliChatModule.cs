using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Moderations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ModelTypeInfoAttribute : Attribute
    {
        public string ModelType { get; }

        public ModelTypeInfoAttribute(string modelType)
        {
            ModelType = modelType;
        }
    }

    public enum IntelliGPTModel
    {
        [Description("gpt-4o"), ModelTypeInfo("Chat")]
        gpt4o,

        [Description("gpt-4o-mini"), ModelTypeInfo("Chat")]
        gpt4omini,

        [Description("gpt-4-turbo"), ModelTypeInfo("Chat")]
        gpt4_turbo,

        [Description("gpt-4"), ModelTypeInfo("Chat")]
        gpt4,

        [Description("gpt-3.5-turbo"), ModelTypeInfo("Chat")]
        gpt3_5_turbo,

        [Description("whisper-1"), ModelTypeInfo("STT")]
        whisper1,

        [Description("omni-moderation-latest"), ModelTypeInfo("Moderation")]
        Moderation_Latest,
    }


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
        private string _lastRequestModelName;

        public TokenUsageData()
        {
            DailyUsages = new ObservableCollection<DailyTokenUsage>();
        }

        public ObservableCollection<DailyTokenUsage> DailyUsages { get; set; }

        public int TotalDailyTokens => DailyUsages.LastOrDefault()?.TotalDailyTokens ?? 0;
        public int TotalDailyRequests => DailyUsages.LastOrDefault()?.TotalDailyRequests ?? 0;

        // Expose the last request's total tokens
        public int LastRequestTotalTokens => _lastRequestTotalTokens;

        // Expose the last request's model name
        public string LastRequestModelName => _lastRequestModelName;

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
            _lastRequestModelName = modelName;

            // Notify UI about changes
            OnPropertyChanged(nameof(TotalDailyTokens));
            OnPropertyChanged(nameof(TotalDailyRequests));
            OnPropertyChanged(nameof(LastRequestTotalTokens));
            OnPropertyChanged(nameof(LastRequestModelName));
        }
    }

    public partial class IntelliChatModuleSettings : ObservableObject
    {
        [ObservableProperty]
    private IntelliGPTModel performSpellingCheckModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel generateConversationStarterModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel performLanguageTranslationModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel performShortenTextModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel performBeautifySentenceModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel performTextCompletionModel = IntelliGPTModel.gpt4o;

        [ObservableProperty]
        private IntelliGPTModel performModerationCheckModel = IntelliGPTModel.Moderation_Latest;

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

        public IEnumerable<IntelliGPTModel> AvailableChatModels => Enum.GetValues(typeof(IntelliGPTModel))
    .Cast<IntelliGPTModel>()
    .Where(m => GetModelType(m) == "Chat");

        public IEnumerable<IntelliGPTModel> AvailableSTTModels => Enum.GetValues(typeof(IntelliGPTModel))
    .Cast<IntelliGPTModel>()
    .Where(m => GetModelType(m) == "STT");


        public IEnumerable<IntelliGPTModel> AvailableTTSModels => Enum.GetValues(typeof(IntelliGPTModel))
    .Cast<IntelliGPTModel>()
    .Where(m => GetModelType(m) == "TSS");


        public IntelliChatModule()
        {
            Initialize();
        }

        public static string GetModelDescription(IntelliGPTModel model)
        {
            var type = model.GetType();
            var memberInfo = type.GetMember(model.ToString());
            if (memberInfo.Length > 0)
            {
                var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attrs.Length > 0)
                {
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }

            return null; // Or a sensible default
        }

        public static string GetModelType(IntelliGPTModel model)
        {
            var type = model.GetType();
            var memberInfo = type.GetMember(model.ToString());
            if (memberInfo.Length > 0)
            {
                var attrs = memberInfo[0].GetCustomAttributes(typeof(ModelTypeInfoAttribute), false);
                if (attrs.Length > 0)
                {
                    return ((ModelTypeInfoAttribute)attrs[0]).ModelType;
                }
            }

            return "Unknown"; // Or any default value you see fit
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
            EnsureValidSelections();
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

                var defaultModel = IntelliGPTModel.gpt4o;

    // Check if the selected models are still valid
    Settings.PerformSpellingCheckModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformSpellingCheckModel)
        ? Settings.PerformSpellingCheckModel : IntelliGPTModel.gpt4o;

    Settings.GenerateConversationStarterModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.GenerateConversationStarterModel)
        ? Settings.GenerateConversationStarterModel : IntelliGPTModel.gpt4o;

    Settings.PerformLanguageTranslationModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformLanguageTranslationModel)
        ? Settings.PerformLanguageTranslationModel : IntelliGPTModel.gpt4o;

    Settings.PerformShortenTextModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformShortenTextModel)
        ? Settings.PerformShortenTextModel : IntelliGPTModel.gpt4o;

    Settings.PerformBeautifySentenceModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformBeautifySentenceModel)
        ? Settings.PerformBeautifySentenceModel : IntelliGPTModel.gpt4o;

    Settings.PerformTextCompletionModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformTextCompletionModel)
        ? Settings.PerformTextCompletionModel : IntelliGPTModel.gpt4o;

            if (Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformModerationCheckModel) &&
                GetModelType(Settings.PerformModerationCheckModel) == "Moderation")
            {
            }
            else
            {
                Settings.PerformModerationCheckModel = IntelliGPTModel.Moderation_Latest;
            }


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
            string rawResponse = response?.Choices?[0].Message.Content.GetString() ?? string.Empty;
            string sanitizedResponse = SanitizeShortenedText(rawResponse);

            Settings.IntelliChatUILabel = false;
            Settings.IntelliChatUILabelTxt = string.Empty;

            Settings.IntelliChatTxt = sanitizedResponse;
            Settings.IntelliChatWaitingToAccept = true;

            ProcessUsedTokens(response);
        }

        public void ProcessUsedTokens(ChatResponse response)
        {
            if (response?.Usage == null)
            {
                Console.WriteLine("No response or usage data available.");
                return;
            }

            string modelName = response.Model;
            int promptTokens = response.Usage.PromptTokens ?? 0;
            int completionTokens = response.Usage.CompletionTokens ?? 0;

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
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }
            if (!EnsureInitialized())
            {
                return;
            }

            try
            {
                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                // Define the refined prompt
                var prompt = "You are an imaginative conversationalist specializing all directions. Generate a creative and engaging conversation starter that is 140 characters or fewer, incorporating subtle lewdness or double entendres without being explicit.";

                ResetCancellationToken(Settings.IntelliChatTimeout);

                // Construct messages with role clarity
                var messages = new List<Message>
        {
            new Message(Role.System, prompt)
        };

                // Include language considerations if applicable
                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    // Extracting the Language property from each SupportedIntelliChatLanguage object
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                    // Joining the language strings with commas
                    var languagesString = string.Join(", ", languages);

                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
                }


                var modelName = GetModelDescription(Settings.GenerateConversationStarterModel); 

                // Adjusted model parameters
                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(
                        messages: messages,
                        maxTokens: 20, // Sufficient for a 140-character response
                        temperature: 0.7, // Balanced creativity and coherence
                        model: modelName),
                    _cancellationTokenSource.Token);

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
                new IntelliChatWritingStyle { ID = 1, StyleName = "Casual", StyleDescription = "A casual, everyday writing style", Temperature = 0.6, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 2, StyleName = "Formal", StyleDescription = "A formal, professional writing style", Temperature = 0.3, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 3, StyleName = "Friendly", StyleDescription = "A friendly, approachable writing style", Temperature = 0.5, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 4, StyleName = "Professional", StyleDescription = "A professional, business-oriented writing style", Temperature = 0.4, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 5, StyleName = "Academic", StyleDescription = "An academic, scholarly writing style", Temperature = 0.25, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 6, StyleName = "Creative", StyleDescription = "A creative, imaginative writing style", Temperature = 1.1, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 7, StyleName = "Humorous", StyleDescription = "A humorous, funny writing style", Temperature = 1.0, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 8, StyleName = "British", StyleDescription = "A British, UK-specific writing style", Temperature = 0.5, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 9, StyleName = "Sarcastic", StyleDescription = "A sarcastic, witty writing style", Temperature = 0.9, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 10, StyleName = "Romantic", StyleDescription = "A romantic, lovey-dovey writing style", Temperature = 0.6, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 11, StyleName = "Action-Packed", StyleDescription = "An action-packed, adrenaline-fueled writing style", Temperature = 1.1, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 12, StyleName = "Mysterious", StyleDescription = "A mysterious, suspenseful writing style", Temperature = 0.9, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 13, StyleName = "Sci-Fi", StyleDescription = "A futuristic, sci-fi writing style", Temperature = 1.1, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 14, StyleName = "Horror", StyleDescription = "A chilling, horror writing style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 15, StyleName = "Western", StyleDescription = "A wild west, cowboy writing style", Temperature = 0.6, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 16, StyleName = "Fantasy", StyleDescription = "A magical, fantasy writing style", Temperature = 1.1, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 17, StyleName = "Superhero", StyleDescription = "A heroic, superhero writing style", Temperature = 0.65, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 18, StyleName = "Film Noir", StyleDescription = "A dark, film noir writing style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 19, StyleName = "Comedy", StyleDescription = "A hilarious, comedy writing style", Temperature = 1.0, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 20, StyleName = "Drama", StyleDescription = "A dramatic, tear-jerking writing style", Temperature = 0.7, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 21, StyleName = "Risqué Humor", StyleDescription = "A bold, cheeky humor style", Temperature = 0.9, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 22, StyleName = "Flirty Banter", StyleDescription = "A playful, flirtatious banter style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 23, StyleName = "Sensual Poetry", StyleDescription = "A deeply sensual, poetic style", Temperature = 0.7, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 24, StyleName = "Bold Confessions", StyleDescription = "A daring, confessional narrative style", Temperature = 0.8, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 25, StyleName = "Seductive Fantasy", StyleDescription = "A seductive, enchanting fantasy style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 26, StyleName = "Candid Chronicles", StyleDescription = "A frank, revealing chronicle style", Temperature = 0.7, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 27, StyleName = "Playful Teasing", StyleDescription = "A light-hearted, teasing narrative style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 28, StyleName = "Intimate Reflections", StyleDescription = "A deeply personal, intimate reflection style", Temperature = 0.65, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 29, StyleName = "Enigmatic Erotica", StyleDescription = "A mysterious, subtly erotic style", Temperature = 0.9, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 30, StyleName = "Whimsical Winks", StyleDescription = "A whimsical, suggestive wink style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 31, StyleName = "Lavish Desires", StyleDescription = "A richly descriptive, desire-filled style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 32, StyleName = "Cheeky Charm", StyleDescription = "A cheeky, charmingly persuasive style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 33, StyleName = "Elegant Enticement", StyleDescription = "An elegantly enticing, sophisticated style", Temperature = 0.7, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 34, StyleName = "Veiled Allusions", StyleDescription = "A subtly allusive, veiled narrative style", Temperature = 0.8, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 35, StyleName = "Rousing Revelations", StyleDescription = "A stimulating, revelation-rich style", Temperature = 0.8, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 36, StyleName = "Tantalizing Tales", StyleDescription = "A tantalizing, teasing tale style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 37, StyleName = "Forbidden Fantasies", StyleDescription = "A style rich with forbidden fantasies", Temperature = 0.95, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 38, StyleName = "Wicked Whispers", StyleDescription = "A wickedly whispering, secretive style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 39, StyleName = "Coquettish Confidences", StyleDescription = "A coquettish, confidently playful style", Temperature = 0.75, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 40, StyleName = "Thriller", StyleDescription = "A thrilling, suspenseful writing style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 41, StyleName = "Noir Romance", StyleDescription = "A sultry, mysterious romance style", Temperature = 0.7, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 42, StyleName = "Sensual Noir", StyleDescription = "A dark, sensual noir style", Temperature = 0.85, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 43, StyleName = "Seductive Noir", StyleDescription = "A seductive, mysterious noir style", Temperature = 0.8, IsBuiltIn = true },
                new IntelliChatWritingStyle { ID = 44, StyleName = "Sultry Noir", StyleDescription = "A sultry, mysterious noir style", Temperature = 0.7, IsBuiltIn = true }
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

                // Check if the JSON data is empty, contains only null characters, or is whitespace
                if (string.IsNullOrWhiteSpace(jsonData) || jsonData.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The settings JSON file is empty or corrupted.");
                    Settings = new IntelliChatModuleSettings();
                    MergeOrUpdateBuiltInStylesAndLanguages();
                    EnsureValidSelections();
                    return;
                }

                try
                {
                    var settings = JsonConvert.DeserializeObject<IntelliChatModuleSettings>(jsonData);
                    if (settings != null)
                    {
                        Settings = settings;
                        MergeOrUpdateBuiltInStylesAndLanguages();
                        EnsureValidSelections();
                    }
                    else
                    {
                        Logging.WriteInfo("Failed to deserialize the settings JSON.");
                        Settings = new IntelliChatModuleSettings();
                        MergeOrUpdateBuiltInStylesAndLanguages();
                        EnsureValidSelections();
                    }
                }
                catch (JsonException ex)
                {
                    Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
                    Settings = new IntelliChatModuleSettings();
                    MergeOrUpdateBuiltInStylesAndLanguages();
                    EnsureValidSelections();
                }
            }
            else
            {
                Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
                Settings = new IntelliChatModuleSettings();
                MergeOrUpdateBuiltInStylesAndLanguages();
                EnsureValidSelections();
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

                var modelName = GetModelDescription(Settings.PerformModerationCheckModel);

                var moderationResponse = await OpenAIModule.Instance.OpenAIClient.ModerationsEndpoint.CreateModerationAsync(new ModerationsRequest(text, modelName), _cancellationTokenSource.Token);

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
            new Message(Role.System, $"You are a helpful assistant that rewrites sentences in a {intelliChatWritingStyle.StyleDescription} style without adding any extra information."),
            new Message(Role.User, text)
        };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                    var languagesString = string.Join(", ", languages);
                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
                }

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var modelName = GetModelDescription(Settings.PerformBeautifySentenceModel);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(
                        messages: messages,
                        maxTokens: 60,
                        temperature: intelliChatWritingStyle.Temperature,
                        model: modelName),
                    _cancellationTokenSource.Token);

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

                SupportedIntelliChatLanguage intelliChatLanguage = supportedIntelliChatLanguage ?? Settings.SelectedTranslateLanguage;

                var messages = new List<Message>
        {
            new Message(Role.System, $"You are a professional translator. Translate the following text to {intelliChatLanguage.Language} accurately without adding any additional information or context."),
            new Message(Role.User, text)
        };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                    var languagesString = string.Join(", ", languages);
                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
                }

                Settings.IntelliChatUILabel = true;
                Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

                var modelName = GetModelDescription(Settings.PerformLanguageTranslationModel);

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(
                        messages: messages,
                        maxTokens: 120, 
                        temperature: 0.3,
                        model: modelName),
                    _cancellationTokenSource.Token);

                if (response == null)
                {
                    Settings.IntelliChatUILabel = false;
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

                IntelliChatWritingStyle intelliChatWritingStyle = Settings.SelectedWritingStyle;

                var messages = new List<Message>
        {
            new Message(Role.System, "You are a professional editor. Correct any spelling and grammar errors in the following text without adding or removing any additional information."),
            new Message(Role.User, text)
        };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                    var languagesString = string.Join(", ", languages);

                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
                }

                var modelName = GetModelDescription(Settings.PerformSpellingCheckModel);

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(
                        messages: messages,
                        maxTokens: 60,
                        temperature: 0.3,
                        model: modelName),
                    _cancellationTokenSource.Token);

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

                // Define the prompt based on retry count
                string prompt = retryCount == 0
                    ? $"You are an expert at condensing text. Please shorten the following text to **140 characters or fewer** without adding, removing, or altering any information:\n\n{text}"
                    : $"The previous attempt did not meet the 140-character limit. Please shorten the following text to **140 characters or fewer** without adding, removing, or altering any information:\n\n{text}";

                var modelName = GetModelDescription(Settings.PerformShortenTextModel);

                // Construct messages with role clarity
                var messages = new List<Message>
        {
            new Message(Role.System, prompt)
        };

                if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
                {
                    // Extracting the Language property from each SupportedIntelliChatLanguage object
                    var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                    // Joining the language strings with commas
                    var languagesString = string.Join(", ", languages);

                    messages.Add(new Message(Role.System, $"Consider these languages: {languagesString}"));
                }

                ResetCancellationToken(Settings.IntelliChatTimeout);

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint
                    .GetCompletionAsync(new ChatRequest(
                        messages: messages,
                        maxTokens: 60, // Adjusted for conciseness
                        temperature: 0.3, // Reduced for determinism
                        model: modelName),
                    _cancellationTokenSource.Token);

                var shortenedText = response?.Choices?[0].Message.Content.ValueKind == System.Text.Json.JsonValueKind.String
                    ? response.Choices[0].Message.Content.GetString()
                    : string.Empty;

                // Sanitize the shortened text
                string sanitizedShortenedText = SanitizeShortenedText(shortenedText);

                // Check if the response is still over 140 characters and retry if necessary
                if (sanitizedShortenedText.Length > 140 && retryCount < 2) // Limiting to two retries
                {
                    await ShortenTextAsync(sanitizedShortenedText, retryCount + 1);
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

        private string SanitizeShortenedText(string response)
        {
            if (string.IsNullOrEmpty(response))
                return string.Empty;

            // Remove any leading/trailing quotation marks
            response = RemoveQuotationMarkAroundResponse(response);

            // Trim the response to 140 characters if necessary
            if (response.Length > 140)
            {
                response = response.Substring(0, 140).TrimEnd('.') + "...";
            }

            return response;
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
                var modelName = GetModelDescription(Settings.PerformTextCompletionModel);


                // Apply the selected writing style
                var writingStyle = Settings.SelectedWritingStyle;
                var promptMessage = isNextWordPrediction ? "Predict the next chat message word." : "Complete the following chat message, max 144 characters";
                var messages = new List<Message>
        {
            new Message(Role.System, promptMessage +  $"Use a {writingStyle.StyleName} writing style."),
            new Message(Role.User, inputText)
        };

                // Customizing ChatRequest for the task
                var chatRequest = new ChatRequest(
                    messages: messages,
                    model: modelName,
                    maxTokens: isNextWordPrediction ? 3 : 100, // Increased max tokens for better context
                    temperature: writingStyle.Temperature, // Use the temperature from the selected writing style
                    topP: 1,
                    frequencyPenalty: 0.3, // Slightly reduced to encourage more common phrases in VRChat
                    presencePenalty: 0.2 // Slightly reduced to allow some repetition, mimicking real chat
                );

                var response = await OpenAIModule.Instance.OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

                if (response?.Choices?.Count > 0)
                {
                    var generatedText = response.Choices[0].Message.Content.GetString();

                    if (!string.IsNullOrEmpty(generatedText))
                    {
                        generatedText = generatedText.Trim();
                    }

                    Settings.IntelliChatTxt = generatedText;
                    Settings.IntelliChatWaitingToAccept = true;
                }
                else
                {
                    throw new InvalidOperationException("The response from OpenAI was empty or invalid.");
                }
            }
            catch (Exception ex)
            {
                ProcessError(ex);
            }
            finally
            {
                Settings.IntelliChatUILabel = false;
            }
        }

        private string FormatTextForVRChat(string text)
        {
            // Perform any necessary formatting or processing of the text for VRChat
            // For example, you can add emoji, apply text effects, or limit the length

            // Add some cool emoji
            text = AddEmojiToText(text);

            // Limit the length to fit nicely in the VRChat chatbox
            text = LimitTextLength(text, 100);

            return text;
        }

        private string AddEmojiToText(string text)
        {
            // Use regex or other methods to identify places to insert emoji
            // For example, you can add a smiley face after a joke or a heart after a compliment
            // Implement your emoji insertion logic here

            return text;
        }

        private string LimitTextLength(string text, int maxLength)
        {
            if (text.Length > maxLength)
            {
                text = text.Substring(0, maxLength - 3) + "...";
            }
            return text;
        }


    }
}
