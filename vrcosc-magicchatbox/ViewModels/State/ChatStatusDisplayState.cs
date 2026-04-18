using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Runtime display state for the Chat/Status input UI.
/// Owns text inputs, top-bar labels, character count displays,
/// typing indicator, countdown, scan-pause state,
/// and the StatusList / LastMessages collections.
/// </summary>
public partial class ChatStatusDisplayState : ObservableObject
{
    [ObservableProperty] private bool _scanPause;

    // Custom property: ScanLoopService does self-assignment to force UI refresh,
    // so we must always raise PropertyChanged (no same-value short-circuit).
    private int _scanPauseCountDown;
    public int ScanPauseCountDown
    {
        get => _scanPauseCountDown;
        set { _scanPauseCountDown = value; OnPropertyChanged(); }
    }

    [ObservableProperty] private string _newStatusItemTxt = string.Empty;
    [ObservableProperty] private string _newChattingTxt = string.Empty;
    [ObservableProperty] private string _chatFeedbackTxt = string.Empty;
    [ObservableProperty] private string _focusedWindow = string.Empty;
    [ObservableProperty] private string _statusTopBarTxt = string.Empty;
    [ObservableProperty] private string _chatTopBarTxt = string.Empty;
    [ObservableProperty] private string _statusBoxCount = "0/140";
    [ObservableProperty] private string _statusBoxColor = "#FF504767";
    [ObservableProperty] private string _chatBoxCount = "0/140";
    [ObservableProperty] private string _chatBoxColor = "#FF504767";
    [ObservableProperty] private bool _typingIndicator;
    [ObservableProperty] private bool _countDownUI = true;
    [ObservableProperty] private bool _intelliChatRequesting = false;

    private ObservableCollection<StatusItem> _statusList = new();
    public ObservableCollection<StatusItem> StatusList
    {
        get => _statusList;
        set { _statusList = value; OnPropertyChanged(); }
    }

    private ObservableCollection<StatusGroup> _groupList = new();
    public ObservableCollection<StatusGroup> GroupList
    {
        get => _groupList;
        set { _groupList = value; OnPropertyChanged(); }
    }

    private ObservableCollection<ChatItem> _lastMessages = new();
    public ObservableCollection<ChatItem> LastMessages
    {
        get => _lastMessages;
        set { _lastMessages = value; OnPropertyChanged(); }
    }

    private int _statusIndex;
    public int StatusIndex
    {
        get => _statusIndex;
        set
        {
            if (_statusIndex != value)
            {
                _statusIndex = value;
                OnPropertyChanged();
            }
        }
    }
}

