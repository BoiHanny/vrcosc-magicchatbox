using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>Marks an <see cref="IntelliGPTModel"/> enum field with its API model type (e.g. "Chat", "STT").</summary>
[AttributeUsage(AttributeTargets.Field)]
public class ModelTypeInfoAttribute : Attribute
{
    public ModelTypeInfoAttribute(string modelType)
    {
        ModelType = modelType;
    }

    public string ModelType { get; }
}

/// <summary>
/// Marks an <see cref="IntelliGPTModel"/> enum field as a reasoning model that does NOT support
/// sampling parameters (Temperature, TopP, FrequencyPenalty, PresencePenalty).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class ReasoningModelAttribute : Attribute { }

/// <summary>Enumerates available OpenAI models, tagged with their type via <see cref="ModelTypeInfoAttribute"/>.</summary>
public enum IntelliGPTModel
{
    [Description("gpt-5.2"), ModelTypeInfo("Chat")]
    gpt5_2,

    [Description("gpt-5.1"), ModelTypeInfo("Chat")]
    gpt5_1,

    [Description("gpt-5"), ModelTypeInfo("Chat")]
    gpt5,

    [Description("gpt-5-mini"), ModelTypeInfo("Chat")]
    gpt5_mini,

    [Description("gpt-5-nano"), ModelTypeInfo("Chat")]
    gpt5_nano,

    [Description("gpt-4.1"), ModelTypeInfo("Chat")]
    gpt4_1,

    [Description("gpt-4.1-mini"), ModelTypeInfo("Chat")]
    gpt4_1_mini,

    [Description("gpt-4.1-nano"), ModelTypeInfo("Chat")]
    gpt4_1_nano,

    [Description("gpt-4o"), ModelTypeInfo("Chat")]
    gpt4o,

    [Description("gpt-4o-mini"), ModelTypeInfo("Chat")]
    gpt4omini,

    [Description("o1"), ModelTypeInfo("Chat"), ReasoningModel]
    o1,

    [Description("o1-mini"), ModelTypeInfo("Chat"), ReasoningModel]
    o1_mini,

    [Description("o3"), ModelTypeInfo("Chat"), ReasoningModel]
    o3,

    [Description("o3-mini"), ModelTypeInfo("Chat"), ReasoningModel]
    o3_mini,

    [Description("whisper-1"), ModelTypeInfo("STT")]
    whisper1,

    [Description("gpt-4o-mini-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_mini_transcribe,

    [Description("gpt-4o-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_transcribe,

    [Description("gpt-4o-transcribe-diarize"), ModelTypeInfo("STT")]
    gpt_4o_transcribe_diarize,

    [Description("omni-moderation-latest"), ModelTypeInfo("Moderation")]
    Moderation_Latest,
}

/// <summary>Tracks prompt and completion token counts for a single model within a session.</summary>
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

/// <summary>Aggregates per-model token usage for a single calendar day.</summary>
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

/// <summary>Persists and exposes cumulative OpenAI token usage across all models and days.</summary>
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

/// <summary>Represents a language the user can translate into or target for AI-generated text.</summary>
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

/// <summary>Defines an AI writing style (name, description, and temperature) used when generating or rewriting text.</summary>
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
