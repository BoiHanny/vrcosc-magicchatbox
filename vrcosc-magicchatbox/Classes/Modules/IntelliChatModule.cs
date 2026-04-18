using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Messaging;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Provides AI-powered text enhancement features (completion, translation, grammar check, etc.)
/// via the OpenAI chat API, integrated with the VRChat chatbox.
/// </summary>
public partial class IntelliChatModule : ObservableObject, IModule, IRecipient<IntelliChatUiStatusMessage>
{
    private readonly IEnvironmentService _env;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly IMenuNavigationService _navService;
    private readonly IMessenger _messenger;
    private readonly IUiDispatcher _dispatcher;
    private readonly IOpenAiChatService _chatService;

    private const string IntelliChatSettingsFileName = "IntelliChatSettings.json";

    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isInitialized = false;

    [ObservableProperty]
    private IntelliChatModuleSettings settings = new IntelliChatModuleSettings();

    public string Name => "IntelliChat";
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning => _isInitialized;
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) { CancelAllCurrentTasks(); return Task.CompletedTask; }
    public void Dispose() => _messenger.UnregisterAll(this);


    public IntelliChatModule(IEnvironmentService env, ChatStatusDisplayState chatStatus, IMenuNavigationService navService, IOpenAiChatService chatService, IMessenger messenger, IUiDispatcher dispatcher)
    {
        _env = env;
        _chatStatus = chatStatus;
        _navService = navService;
        _chatService = chatService;
        _messenger = messenger;
        _dispatcher = dispatcher;

        // Subscribe to cross-module status messages (e.g. from WhisperModule)
        _messenger.RegisterAll(this);

        Initialize();
    }

    /// <summary>
    /// Handles status messages from WhisperModule (and any future senders).
    /// Applies text+visibility to IntelliChat UI label, with auto-hide for transient messages.
    /// </summary>
    public void Receive(IntelliChatUiStatusMessage message)
    {
        try
        {
            Settings.IntelliChatUILabelTxt = message.Text;
            Settings.IntelliChatUILabel = true;

            if (!message.ShowPermanently)
            {
                // Show briefly, then auto-hide after 2.5 s
                _ = Task.Run(async () =>
                {
                    await Task.Delay(Core.Constants.IntelliChatAutoHideDelay);
                    _dispatcher.Invoke(() => Settings.IntelliChatUILabel = false);
                });
            }
        }
        catch (Exception ex)
        {
            Logging.WriteInfo($"Error handling IntelliChatUiStatusMessage: {ex.Message}");
        }
    }

    private string AddEmojiToText(string text)
    {
        return text;
    }


    private bool EnsureInitialized()
    {
        if (!_isInitialized)
        {
            UpdateErrorState(true, "IntelliChat not initialized.");
            return false;
        }
        if (!_chatService.IsClientAvailable)
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

    private string FormatTextForVRChat(string text)
    {
        text = AddEmojiToText(text);
        text = LimitTextLength(text, 100);
        return text;
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

    private void Initialize()
    {
        if (_isInitialized) return;

        LoadSettings();
        EnsureValidSelections();
        _isInitialized = true;
    }

    private string LimitTextLength(string text, int maxLength)
    {
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength - 3) + "...";
        }
        return text;
    }

    private void MergeOrUpdateBuiltInStylesAndLanguages()
    {
        var defaultLanguages = GetDefaultLanguages();
        var defaultStyles = GetDefaultWritingStyles();

        foreach (var lang in defaultLanguages)
        {
            if (!Settings.SupportedLanguages.Any(l => l.ID == lang.ID))
            {
                Settings.SupportedLanguages.Add(lang);
            }
        }

        foreach (var style in defaultStyles)
        {
            var existingStyle = Settings.SupportedWritingStyles.FirstOrDefault(s => s.ID == style.ID);
            if (existingStyle == null)
            {
                Settings.SupportedWritingStyles.Add(style);
            }
            else
            {
                existingStyle.StyleName = style.StyleName;
                existingStyle.Temperature = style.Temperature;
                existingStyle.StyleDescription = style.StyleDescription;
                existingStyle.IsBuiltIn = true;
            }
        }

        SaveSettings();
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

    private void ProcessResponse(ChatCompletion? completion)
    {
        string rawResponse = completion?.Content?.Count > 0
            ? completion.Content[0].Text ?? string.Empty
            : string.Empty;
        string sanitizedResponse = SanitizeShortenedText(rawResponse);

        Settings.IntelliChatUILabel = false;
        Settings.IntelliChatUILabelTxt = string.Empty;

        Settings.IntelliChatTxt = sanitizedResponse;
        Settings.IntelliChatWaitingToAccept = true;

        ProcessUsedTokens(completion);
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





    private void UpdateErrorState(bool hasError, string errorMessage)
    {
        Settings.IntelliChatError = hasError;
        Settings.IntelliChatErrorTxt = errorMessage;
    }

    public void AcceptIntelliChatSuggestion()
    {
        _chatStatus.NewChattingTxt = Settings.IntelliChatTxt;
        Settings.IntelliChatTxt = string.Empty;
        Settings.IntelliChatWaitingToAccept = false;


    }
    /// <summary>
    /// Adds a user-defined writing style with the given parameters. Assigns IDs starting at 1000 to avoid
    /// collisions with built-in style IDs (1–999).
    /// </summary>
    public void AddWritingStyle(string styleName, string styleDescription, double temperature)
    {
        // Find the next available ID starting from 1000 for user-defined styles
        int nextId = Settings.SupportedWritingStyles.DefaultIfEmpty().Max(style => style?.ID ?? 999) + 1;
        if (nextId < 1000) nextId = 1000;

        if (Settings.SupportedWritingStyles.Any(style => style.StyleName.Equals(styleName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A writing style with the name \"{styleName}\" already exists.");
        }

        var newStyle = new IntelliChatWritingStyle
        {
            ID = nextId,
            StyleName = styleName,
            StyleDescription = styleDescription,
            Temperature = temperature,
            IsBuiltIn = false,
        };
        Settings.SupportedWritingStyles.Add(newStyle);

        SaveSettings();
    }

    public void CancelAllCurrentTasks()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public void CloseIntelliErrorPanel()
    {
        Settings.IntelliChatError = false;
        Settings.IntelliChatErrorTxt = string.Empty;
    }


    /// <summary>
    /// Validates and corrects the selected writing style and language against the current supported lists,
    /// and verifies that all model enum values are still defined.
    /// </summary>
    public void EnsureValidSelections()
    {
        var selectedStyle = Settings.SelectedWritingStyle != null
            ? Settings.SupportedWritingStyles.FirstOrDefault(style => style.ID == Settings.SelectedWritingStyle.ID)
            : null;

        var selectedTranslateLanguage = Settings.SelectedTranslateLanguage != null
            ? Settings.SupportedLanguages.FirstOrDefault(lang => lang.ID == Settings.SelectedTranslateLanguage.ID)
            : null;
        Settings.SelectedWritingStyle = selectedStyle ?? Settings.SupportedWritingStyles.FirstOrDefault(style => style.IsBuiltIn);
        Settings.SelectedTranslateLanguage = selectedTranslateLanguage ?? Settings.SupportedLanguages.Where(lang => lang.Language.Equals("English", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        var defaultModel = IntelliGPTModel.gpt5_nano;

        Settings.PerformSpellingCheckModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformSpellingCheckModel)
            ? Settings.PerformSpellingCheckModel : defaultModel;

        Settings.GenerateConversationStarterModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.GenerateConversationStarterModel)
            ? Settings.GenerateConversationStarterModel : defaultModel;

        Settings.PerformLanguageTranslationModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformLanguageTranslationModel)
            ? Settings.PerformLanguageTranslationModel : defaultModel;

        Settings.PerformShortenTextModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformShortenTextModel)
            ? Settings.PerformShortenTextModel : defaultModel;

        Settings.PerformBeautifySentenceModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformBeautifySentenceModel)
            ? Settings.PerformBeautifySentenceModel : defaultModel;

        Settings.PerformTextCompletionModel = Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformTextCompletionModel)
            ? Settings.PerformTextCompletionModel : defaultModel;

        if (Enum.IsDefined(typeof(IntelliGPTModel), Settings.PerformModerationCheckModel) &&
            GetModelType(Settings.PerformModerationCheckModel) == "Moderation")
        {
        }
        else
        {
            Settings.PerformModerationCheckModel = IntelliGPTModel.Moderation_Latest;
        }


    }

    /// <summary>
    /// Generates an AI text completion or next-word prediction for <paramref name="inputText"/> using the selected writing style.
    /// </summary>
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


            var writingStyle = Settings.SelectedWritingStyle;
            var promptMessage = isNextWordPrediction ? "Predict the next chat message word." : $"Complete the following chat message, max {Core.Constants.OscMaxMessageLength} characters";
            var messages = new List<ChatMessage>
    {
        new SystemChatMessage(promptMessage +  $"Use a {writingStyle.StyleName} writing style."),
        new UserChatMessage(inputText)
    };

            var modelName = GetModelDescription(Settings.PerformTextCompletionModel);

            var completion = await _chatService.GetChatCompletionAsync(
                messages,
                modelName,
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = isNextWordPrediction ? 3 : 100,
                    Temperature = (float)writingStyle.Temperature,
                    TopP = 1,
                    FrequencyPenalty = 0.3f,
                    PresencePenalty = 0.2f
                });

            if (completion?.Content?.Count > 0)
            {
                var generatedText = completion.Content[0].Text;

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



    /// <summary>
    /// Generates a creative, context-aware conversation opener and places it in the waiting-to-accept queue.
    /// </summary>
    public async Task GenerateConversationStarterAsync()
    {
        if (!_chatService.IsClientAvailable)
        {
            _navService.ActivateSetting("Settings_OpenAI");
        }
        if (!EnsureInitialized())
        {
            return;
        }

        try
        {
            Settings.IntelliChatUILabel = true;
            Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

            var prompt = "You are an imaginative conversationalist specializing all directions. Generate a creative and engaging conversation starter that is 140 characters or fewer, incorporating subtle lewdness or double entendres without being explicit.";

            ResetCancellationToken(Settings.IntelliChatTimeout);

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage(prompt)
    };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
            {
                var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                var languagesString = string.Join(", ", languages);
                messages.Add(new SystemChatMessage($"Consider these languages: {languagesString}"));
            }


            var modelName = GetModelDescription(Settings.GenerateConversationStarterModel);

            var completion = await _chatService.GetChatCompletionAsync(
                    messages,
                    modelName,
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 20,
                        Temperature = 0.7f
                    },
                _cancellationTokenSource.Token);

            if (completion == null)
            {
                throw new InvalidOperationException("The response from OpenAI was empty");
            }
            else
            {
                ProcessResponse(completion);
            }
        }
        catch (Exception ex)
        {
            ProcessError(ex);
        }
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

        return null;
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

        return "Unknown";
    }

    /// <summary>
    /// Loads persisted <see cref="IntelliChatModuleSettings"/> from disk, merges built-in defaults, and validates selections.
    /// </summary>
    public void LoadSettings()
    {
        var filePath = Path.Combine(_env.DataPath, IntelliChatSettingsFileName);
        if (File.Exists(filePath))
        {
            var jsonData = File.ReadAllText(filePath);

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





    /// <summary>
    /// Submits <paramref name="text"/> to the OpenAI Moderations API and returns <see langword="true"/> if no policy violations are detected.
    /// </summary>
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

            var moderationResponse = await _chatService.ClassifyTextAsync(text, modelName, _cancellationTokenSource.Token);

            if (moderationResponse?.Flagged ?? false)
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


    /// <summary>
    /// Rewrites <paramref name="text"/> using the given writing style (or the currently selected style if null).
    /// </summary>
    public async Task PerformBeautifySentenceAsync(string text, IntelliChatWritingStyle intelliChatWritingStyle = null)
    {
        if (!_chatService.IsClientAvailable)
        {
            _navService.ActivateSetting("Settings_OpenAI");
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

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage($"You are a helpful assistant that rewrites sentences in a {intelliChatWritingStyle.StyleDescription} style without adding any extra information."),
        new UserChatMessage(text)
    };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
            {
                var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                var languagesString = string.Join(", ", languages);
                messages.Add(new SystemChatMessage($"Consider these languages: {languagesString}"));
            }

            ResetCancellationToken(Settings.IntelliChatTimeout);

            var modelName = GetModelDescription(Settings.PerformBeautifySentenceModel);

            var completion = await _chatService.GetChatCompletionAsync(
                    messages,
                    modelName,
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 60,
                        Temperature = (float)intelliChatWritingStyle.Temperature
                    },
                _cancellationTokenSource.Token);

            if (completion == null)
            {
                throw new InvalidOperationException("The response from OpenAI was empty");
            }

            ProcessResponse(completion);
        }
        catch (Exception ex)
        {
            ProcessError(ex);
        }
    }



    /// <summary>
    /// Translates <paramref name="text"/> into the given target language (or the currently selected language if null).
    /// </summary>
    public async Task PerformLanguageTranslationAsync(string text, SupportedIntelliChatLanguage supportedIntelliChatLanguage = null)
    {
        if (!_chatService.IsClientAvailable)
        {
            _navService.ActivateSetting("Settings_OpenAI");
        }
        if (!EnsureInitializedAndNotEmpty(text))
        {
            return;
        }

        try
        {
            if (!await ModerationCheckPassedAsync(text)) return;

            SupportedIntelliChatLanguage intelliChatLanguage = supportedIntelliChatLanguage ?? Settings.SelectedTranslateLanguage;

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage($"You are a professional translator. Translate the following text to {intelliChatLanguage.Language} accurately without adding any additional information or context."),
        new UserChatMessage(text)
    };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
            {
                var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                var languagesString = string.Join(", ", languages);
                messages.Add(new SystemChatMessage($"Consider these languages: {languagesString}"));
            }

            Settings.IntelliChatUILabel = true;
            Settings.IntelliChatUILabelTxt = "Waiting for OpenAI to respond";

            var modelName = GetModelDescription(Settings.PerformLanguageTranslationModel);

            ResetCancellationToken(Settings.IntelliChatTimeout);

            var completion = await _chatService.GetChatCompletionAsync(
                    messages,
                    modelName,
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 120,
                        Temperature = 0.3f
                    },
                _cancellationTokenSource.Token);

            if (completion == null)
            {
                Settings.IntelliChatUILabel = false;
                throw new InvalidOperationException("The response from OpenAI was empty");
            }
            else
            {
                ProcessResponse(completion);
            }
        }
        catch (Exception ex)
        {
            ProcessError(ex);
        }
    }



    /// <summary>
    /// Corrects spelling and grammar in <paramref name="text"/> and places the result in the waiting-to-accept queue.
    /// </summary>
    public async Task PerformSpellingAndGrammarCheckAsync(string text)
    {
        if (!_chatService.IsClientAvailable)
        {
            _navService.ActivateSetting("Settings_OpenAI");
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

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage("You are a professional editor. Correct any spelling and grammar errors in the following text without adding or removing any additional information."),
        new UserChatMessage(text)
    };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
            {
                var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();

                var languagesString = string.Join(", ", languages);

                messages.Add(new SystemChatMessage($"Consider these languages: {languagesString}"));
            }

            var modelName = GetModelDescription(Settings.PerformSpellingCheckModel);

            ResetCancellationToken(Settings.IntelliChatTimeout);

            var completion = await _chatService.GetChatCompletionAsync(
                    messages,
                    modelName,
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 60,
                        Temperature = 0.3f
                    },
                _cancellationTokenSource.Token);

            if (completion == null)
            {
                throw new InvalidOperationException("The response from OpenAI was empty");
            }
            else
            {
                ProcessResponse(completion);
            }
        }
        catch (Exception ex)
        {
            ProcessError(ex);
        }
    }

    public void ProcessUsedTokens(ChatCompletion? completion)
    {
        if (completion?.Usage == null)
        {
            Logging.WriteInfo("No response or usage data available.");
            return;
        }

        string modelName = completion.Model;
        int promptTokens = completion.Usage.InputTokenCount;
        int completionTokens = completion.Usage.OutputTokenCount;

        Settings.TokenUsageData.AddTokenUsage(modelName, promptTokens, completionTokens);
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
        SaveSettings();
    }

    public void ResetCancellationToken(int timeoutInSeconds)
    {
        CancelAllCurrentTasks();
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource.CancelAfter(timeoutInSeconds * 1000);
    }
    public void SaveSettings()
    {
        var filePath = Path.Combine(_env.DataPath, IntelliChatSettingsFileName);
        var jsonData = JsonConvert.SerializeObject(Settings, Formatting.Indented);
        File.WriteAllText(filePath, jsonData);
    }

    /// <summary>
    /// Condenses <paramref name="text"/> to 140 characters or fewer, retrying up to twice if the first response still exceeds the limit.
    /// </summary>
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

            var messages = new List<ChatMessage>
    {
        new SystemChatMessage(prompt)
    };

            if (!Settings.AutolanguageSelection && Settings.SelectedSupportedLanguages.Count > 0)
            {
                var languages = Settings.SelectedSupportedLanguages.Select(lang => lang.Language).ToList();
                var languagesString = string.Join(", ", languages);
                messages.Add(new SystemChatMessage($"Consider these languages: {languagesString}"));
            }

            ResetCancellationToken(Settings.IntelliChatTimeout);

            var completion = await _chatService.GetChatCompletionAsync(
                    messages,
                    modelName,
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 60,
                        Temperature = 0.3f
                    },
                _cancellationTokenSource.Token);

            var shortenedText = completion?.Content?.Count > 0
                ? completion.Content[0].Text ?? string.Empty
                : string.Empty;

            string sanitizedShortenedText = SanitizeShortenedText(shortenedText);

            if (sanitizedShortenedText.Length > 140 && retryCount < 2) // Limiting to two retries
            {
                await ShortenTextAsync(sanitizedShortenedText, retryCount + 1);
            }
            else
            {
                ProcessResponse(completion);
            }
        }
        catch (Exception ex)
        {
            ProcessError(ex);
        }
    }

    public IEnumerable<IntelliGPTModel> AvailableChatModels => Enum.GetValues(typeof(IntelliGPTModel))
.Cast<IntelliGPTModel>()
.Where(m => GetModelType(m) == "Chat");

    public IEnumerable<IntelliGPTModel> AvailableSTTModels => Enum.GetValues(typeof(IntelliGPTModel))
.Cast<IntelliGPTModel>()
.Where(m => GetModelType(m) == "STT");


    public IEnumerable<IntelliGPTModel> AvailableTTSModels => Enum.GetValues(typeof(IntelliGPTModel))
.Cast<IntelliGPTModel>()
.Where(m => GetModelType(m) == "TSS");


}
