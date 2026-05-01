using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the chatbox messaging behavior.
/// </summary>
public partial class ChatSettings : VersionedSettings
{
    public static IEnumerable<ChatAutocompleteMode> AvailableChatAutocompleteModes { get; } =
    [
        ChatAutocompleteMode.LocalHistory,
        ChatAutocompleteMode.OpenAI
    ];

    [ObservableProperty] private bool _chatAddSmallDelay = true;
    [ObservableProperty] private double _chatAddSmallDelayTIME = 1.4;
    [ObservableProperty] private bool _chatLiveEdit = true;
    [ObservableProperty] private bool _chatSendAgainFX = true;
    [ObservableProperty] private double _chattingUpdateRate = 3;
    [ObservableProperty] private bool _chatFX = true;
    [ObservableProperty] private bool _keepUpdatingChat = true;
    [ObservableProperty] private bool _realTimeChatEdit = true;
    [ObservableProperty] private bool _prefixChat = false;
    [ObservableProperty] private bool _hideOpenAITools = false;
    [ObservableProperty] private bool _chatAutocompleteEnabled = false;
    [ObservableProperty] private ChatAutocompleteMode _chatAutocompleteMode = ChatAutocompleteMode.LocalHistory;
    [ObservableProperty] private int _chatAutocompleteMinCharacters = 4;
    [ObservableProperty] private int _chatAutocompleteMaxWords = 2;
    [ObservableProperty] private int _chatAutocompleteDelayMs = 900;
    [ObservableProperty] private bool _chatAutocompleteShowHint = true;

    [JsonIgnore]
    public bool ChatAutocompleteUsesOpenAI => ChatAutocompleteMode == ChatAutocompleteMode.OpenAI;

    partial void OnChatAddSmallDelayTIMEChanged(double value)
    {
        if (value < 0.1) ChatAddSmallDelayTIME = 0.1;
        else if (value > 10) ChatAddSmallDelayTIME = 10;
    }

    partial void OnChattingUpdateRateChanged(double value)
    {
        if (value < 1) ChattingUpdateRate = 1;
        else if (value > 10) ChattingUpdateRate = 10;
    }

    partial void OnKeepUpdatingChatChanged(bool value)
    {
        if (!value) ChatLiveEdit = false;
    }

    partial void OnChatAutocompleteMinCharactersChanged(int value)
    {
        if (value < 2) ChatAutocompleteMinCharacters = 2;
        else if (value > 32) ChatAutocompleteMinCharacters = 32;
    }

    partial void OnChatAutocompleteMaxWordsChanged(int value)
    {
        if (value < 1) ChatAutocompleteMaxWords = 1;
        else if (value > 8) ChatAutocompleteMaxWords = 8;
    }

    partial void OnChatAutocompleteModeChanged(ChatAutocompleteMode value)
        => OnPropertyChanged(nameof(ChatAutocompleteUsesOpenAI));

    partial void OnChatAutocompleteDelayMsChanged(int value)
    {
        if (value < 250) ChatAutocompleteDelayMs = 250;
        else if (value > 5000) ChatAutocompleteDelayMs = 5000;
    }
}

public enum ChatAutocompleteMode
{
    [Description("Local history")]
    LocalHistory,

    [Description("OpenAI next words")]
    OpenAI
}
