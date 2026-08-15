using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

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

    /// <summary>
    /// Push the line to the chatbox while it is still being typed, instead of only on send.
    /// </summary>
    [ObservableProperty] private bool _chatLiveTyping = false;

    /// <summary>
    /// How often an unsent line may be pushed while live typing, in milliseconds. VRChat rate-limits
    /// the chatbox, so this is a floor on the gap between pushes rather than a per-keystroke send.
    /// </summary>
    [ObservableProperty] private int _chatLiveTypingRateMs = 1200;

    /// <summary>
    /// Treat the line as sent once the person stops typing or leaves the box, without waiting for
    /// Enter. This is the only point at which the notification sound plays.
    /// </summary>
    [ObservableProperty] private bool _chatLiveTypingAutoFinalize = true;

    /// <summary>How long a live line may sit untouched before it counts as finished.</summary>
    [ObservableProperty] private int _chatLiveTypingFinalizeMs = 6000;

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

    partial void OnChatLiveTypingRateMsChanged(int value)
    {
        if (value < ChatLiveTypingRateMinMs) ChatLiveTypingRateMs = ChatLiveTypingRateMinMs;
        else if (value > ChatLiveTypingRateMaxMs) ChatLiveTypingRateMs = ChatLiveTypingRateMaxMs;
    }

    partial void OnChatLiveTypingFinalizeMsChanged(int value)
    {
        if (value < ChatLiveTypingFinalizeMinMs) ChatLiveTypingFinalizeMs = ChatLiveTypingFinalizeMinMs;
        else if (value > ChatLiveTypingFinalizeMaxMs) ChatLiveTypingFinalizeMs = ChatLiveTypingFinalizeMaxMs;
    }

    /// <summary>
    /// The fastest the chatbox may be pushed while a line is being typed.
    /// </summary>
    /// <remarks>
    /// VRChat meters the chatbox and puts you on a cooldown for going over, and the sustained rate it
    /// allows works out at about one message a second - the same figure the integration tick has run
    /// at for years without trouble. Anything below this is a floor that would eventually silence the
    /// chatbox entirely, which is a worse outcome than a slightly less smooth line.
    /// </remarks>
    public const int ChatLiveTypingRateMinMs = 1000;

    /// <summary>Past this, "live" has stopped being live.</summary>
    public const int ChatLiveTypingRateMaxMs = 3000;

    /// <summary>
    /// Bounds on the pause that counts as "finished".
    /// </summary>
    /// <remarks>
    /// The floor is deliberately well above a thinking pause. Finishing early is not harmless: the
    /// box clears, so the rest of the sentence becomes a second message and the thought arrives
    /// split in two.
    /// </remarks>
    public const int ChatLiveTypingFinalizeMinMs = 2000;

    public const int ChatLiveTypingFinalizeMaxMs = 20000;
}

public enum ChatAutocompleteMode
{
    [Description("Words you have typed before")]
    LocalHistory,

    [Description("OpenAI guesses the next words")]
    OpenAI
}
