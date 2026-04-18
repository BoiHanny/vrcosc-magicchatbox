using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Persisted settings for the chatbox messaging behavior.
/// </summary>
public partial class ChatSettings : VersionedSettings
{
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
}
