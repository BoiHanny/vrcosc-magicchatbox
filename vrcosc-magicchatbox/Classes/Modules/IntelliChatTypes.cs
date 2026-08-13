using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules;

[AttributeUsage(AttributeTargets.Field)]
public class ModelTypeInfoAttribute : Attribute
{
    public ModelTypeInfoAttribute(string modelType)
    {
        ModelType = modelType;
    }

    public string ModelType { get; }
}

[AttributeUsage(AttributeTargets.Field)]
public class ModelCapabilitiesAttribute : Attribute
{
    public ModelCapabilitiesAttribute(bool supportsSamplingParams, int minOutputTokens)
    {
        SupportsSamplingParams = supportsSamplingParams;
        MinOutputTokens = minOutputTokens;
    }

    public bool SupportsSamplingParams { get; }

    public int MinOutputTokens { get; }

    public static readonly ModelCapabilitiesAttribute Default = new(supportsSamplingParams: true, minOutputTokens: 0);
}

/// <summary>
/// Reasoning models reject sampling parameters (temperature, top-p, penalties) and spend output
/// tokens on hidden reasoning before any visible text appears, so a budget sized for a classic
/// sampling model gets consumed entirely by reasoning and the reply comes back empty.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class ReasoningModelAttribute : ModelCapabilitiesAttribute
{
    public ReasoningModelAttribute() : base(supportsSamplingParams: false, minOutputTokens: 1000) { }
}

/// <summary>
/// Every value here is written out explicitly and must never be reused or renumbered. The selected
/// model is persisted as its number, so inserting a member in the middle would silently move every
/// existing user onto whatever now sits at their saved number - a settings file that said whisper-1
/// yesterday could ask for a chat model today, and transcription would simply stop working.
/// New models take the next free number at the end, whatever order they read in.
/// </summary>
public enum IntelliGPTModel
{
    [Description("gpt-5.2"), ModelTypeInfo("Chat"), ReasoningModel]
    gpt5_2 = 0,

    [Description("gpt-5.1"), ModelTypeInfo("Chat"), ReasoningModel]
    gpt5_1 = 1,

    [Description("gpt-5"), ModelTypeInfo("Chat"), ReasoningModel]
    gpt5 = 2,

    [Description("gpt-5-mini"), ModelTypeInfo("Chat"), ReasoningModel]
    gpt5_mini = 3,

    [Description("gpt-5-nano"), ModelTypeInfo("Chat"), ReasoningModel]
    gpt5_nano = 4,

    [Description("gpt-4.1"), ModelTypeInfo("Chat")]
    gpt4_1 = 5,

    [Description("gpt-4.1-mini"), ModelTypeInfo("Chat")]
    gpt4_1_mini = 6,

    [Description("gpt-4.1-nano"), ModelTypeInfo("Chat")]
    gpt4_1_nano = 7,

    [Description("gpt-4o"), ModelTypeInfo("Chat")]
    gpt4o = 8,

    [Description("gpt-4o-mini"), ModelTypeInfo("Chat")]
    gpt4omini = 9,

    [Description("o1"), ModelTypeInfo("Chat"), ReasoningModel]
    o1 = 10,

    [Description("o1-mini"), ModelTypeInfo("Chat"), ReasoningModel]
    o1_mini = 11,

    [Description("o3"), ModelTypeInfo("Chat"), ReasoningModel]
    o3 = 12,

    [Description("o3-mini"), ModelTypeInfo("Chat"), ReasoningModel]
    o3_mini = 13,

    // Still supported, and the only transcription model that can translate to English, return
    // word-level timestamps or produce subtitles.
    [Description("whisper-1"), ModelTypeInfo("STT")]
    whisper1 = 14,

    [Description("gpt-4o-mini-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_mini_transcribe = 15,

    [Description("gpt-4o-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_transcribe = 16,

    // Returns speaker-labelled segments rather than a plain transcript.
    [Description("gpt-4o-transcribe-diarize"), ModelTypeInfo("STT")]
    gpt_4o_transcribe_diarize = 17,

    [Description("omni-moderation-latest"), ModelTypeInfo("Moderation")]
    Moderation_Latest = 18,

    // OpenAI's current recommendation for transcribing recorded speech, and what the 4o transcribe
    // models are now described as legacy against.
    [Description("gpt-transcribe"), ModelTypeInfo("STT")]
    gpt_transcribe = 19,
}

public partial class ModelTokenUsage : ObservableObject
{
    [ObservableProperty]
    private int completionTokens;

    [ObservableProperty]
    private string modelName;

    [ObservableProperty]
    private int promptTokens;

    public int TotalTokens => PromptTokens + CompletionTokens;
}

public partial class DailyTokenUsage : ObservableObject
{
    [ObservableProperty]
    private DateTime date;

    public DailyTokenUsage()
    {
        Date = DateTime.Today;
        ModelUsages = new ObservableCollection<ModelTokenUsage>();
    }

    public ObservableCollection<ModelTokenUsage> ModelUsages { get; set; }

    public int TotalDailyRequests => ModelUsages.Count;

    public int TotalDailyTokens => ModelUsages.Sum(mu => mu.TotalTokens);
}

public class TokenUsageData : ObservableObject
{
    private string _lastRequestModelName;
    private int _lastRequestTotalTokens;

    public TokenUsageData()
    {
        DailyUsages = new ObservableCollection<DailyTokenUsage>();
    }

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

        _lastRequestTotalTokens = promptTokens + completionTokens;
        _lastRequestModelName = modelName;

        OnPropertyChanged(nameof(TotalDailyTokens));
        OnPropertyChanged(nameof(TotalDailyRequests));
        OnPropertyChanged(nameof(LastRequestTotalTokens));
        OnPropertyChanged(nameof(LastRequestModelName));
    }

    public ObservableCollection<DailyTokenUsage> DailyUsages { get; set; }

    public string LastRequestModelName => _lastRequestModelName;

    public int LastRequestTotalTokens => _lastRequestTotalTokens;
    public int TotalDailyRequests => DailyUsages.LastOrDefault()?.TotalDailyRequests ?? 0;

    public int TotalDailyTokens => DailyUsages.LastOrDefault()?.TotalDailyTokens ?? 0;
}

public partial class SupportedIntelliChatLanguage : ObservableObject
{
    [ObservableProperty]
    private int iD;

    [ObservableProperty]
    private bool isBuiltIn = false;

    [ObservableProperty]
    private bool isFavorite = false;

    [ObservableProperty]
    private string language;
}

public partial class IntelliChatWritingStyle : ObservableObject
{
    [ObservableProperty]
    private int iD;

    [ObservableProperty]
    private bool isBuiltIn;

    [ObservableProperty]
    private bool isFavorite = false;

    [ObservableProperty]
    private string styleDescription;

    [ObservableProperty]
    private string styleName;

    [ObservableProperty]
    private double temperature;
}
