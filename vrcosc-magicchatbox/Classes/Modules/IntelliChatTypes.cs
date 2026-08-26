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

[AttributeUsage(AttributeTargets.Field)]
public class ReasoningModelAttribute : ModelCapabilitiesAttribute
{
    public ReasoningModelAttribute() : base(supportsSamplingParams: false, minOutputTokens: 1000) { }
}

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

    [Description("whisper-1"), ModelTypeInfo("STT")]
    whisper1 = 14,

    [Description("gpt-4o-mini-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_mini_transcribe = 15,

    [Description("gpt-4o-transcribe"), ModelTypeInfo("STT")]
    gpt_4o_transcribe = 16,

    [Description("gpt-4o-transcribe-diarize"), ModelTypeInfo("STT")]
    gpt_4o_transcribe_diarize = 17,

    [Description("omni-moderation-latest"), ModelTypeInfo("Moderation")]
    Moderation_Latest = 18,

    [Description("gpt-transcribe"), ModelTypeInfo("STT")]
    gpt_transcribe = 19,
}

public partial class ModelTokenUsage : ObservableObject
{
    [ObservableProperty]
    private int completionTokens;

    [ObservableProperty]
    private string modelName = string.Empty;

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
    private string _lastRequestModelName = string.Empty;
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
    private string language = string.Empty;
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
    private string styleDescription = string.Empty;

    [ObservableProperty]
    private string styleName = string.Empty;

    [ObservableProperty]
    private double temperature;
}
