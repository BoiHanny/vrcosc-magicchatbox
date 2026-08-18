using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using vrcosc_magicchatbox.ViewModels.Models;

namespace vrcosc_magicchatbox.ViewModels.State;

public partial class ChatStatusDisplayState : ObservableObject
{
    public ChatStatusDisplayState()
    {
        _statusList.CollectionChanged += OnStatusListChanged;
        _groupList.CollectionChanged += OnGroupListChanged;
        _lastMessages.CollectionChanged += OnLastMessagesChanged;

        RefreshStatusListSnapshot();
        RefreshGroupListSnapshot();
        RefreshLastMessagesSnapshot();
    }

    [ObservableProperty] private bool _scanPause;

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
    [ObservableProperty] private string _statusBoxColor = "#230E52";
    [ObservableProperty] private string _chatBoxCount = "0/140";
    [ObservableProperty] private string _chatBoxColor = "#230E52";
    [ObservableProperty] private bool _typingIndicator;
    [ObservableProperty] private bool _countDownUI = true;
    [ObservableProperty] private bool _intelliChatRequesting = false;
    [ObservableProperty] private string _chatAutocompleteSuggestion = string.Empty;
    [ObservableProperty] private bool _chatAutocompleteActive = false;

    private IReadOnlyList<StatusItem> _statusListSnapshot = Array.Empty<StatusItem>();

    private ObservableCollection<StatusItem> _statusList = new();
    public ObservableCollection<StatusItem> StatusList
    {
        get => _statusList;
        set
        {
            var replacement = value ?? new ObservableCollection<StatusItem>();

            if (!ReferenceEquals(_statusList, replacement))
            {
                _statusList.CollectionChanged -= OnStatusListChanged;
                _statusList = replacement;
                _statusList.CollectionChanged += OnStatusListChanged;
            }

            RefreshStatusListSnapshot();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<StatusItem> StatusListSnapshot => Volatile.Read(ref _statusListSnapshot);

    private IReadOnlyList<StatusGroup> _groupListSnapshot = Array.Empty<StatusGroup>();

    private ObservableCollection<StatusGroup> _groupList = new();
    public ObservableCollection<StatusGroup> GroupList
    {
        get => _groupList;
        set
        {
            var replacement = value ?? new ObservableCollection<StatusGroup>();

            if (!ReferenceEquals(_groupList, replacement))
            {
                _groupList.CollectionChanged -= OnGroupListChanged;
                _groupList = replacement;
                _groupList.CollectionChanged += OnGroupListChanged;
            }

            RefreshGroupListSnapshot();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<StatusGroup> GroupListSnapshot => Volatile.Read(ref _groupListSnapshot);

    private IReadOnlyList<ChatItem> _lastMessagesSnapshot = Array.Empty<ChatItem>();

    private ObservableCollection<ChatItem> _lastMessages = new();
    public ObservableCollection<ChatItem> LastMessages
    {
        get => _lastMessages;
        set
        {
            var replacement = value ?? new ObservableCollection<ChatItem>();

            if (!ReferenceEquals(_lastMessages, replacement))
            {
                _lastMessages.CollectionChanged -= OnLastMessagesChanged;
                _lastMessages = replacement;
                _lastMessages.CollectionChanged += OnLastMessagesChanged;
            }

            RefreshLastMessagesSnapshot();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<ChatItem> LastMessagesSnapshot => Volatile.Read(ref _lastMessagesSnapshot);

    private void OnStatusListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshStatusListSnapshot();

    private void OnGroupListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshGroupListSnapshot();

    private void OnLastMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshLastMessagesSnapshot();

    private void RefreshStatusListSnapshot()
    {
        var source = _statusList;

        IReadOnlyList<StatusItem> snapshot = source.Count == 0
            ? Array.Empty<StatusItem>()
            : source.ToArray();

        Volatile.Write(ref _statusListSnapshot, snapshot);
    }

    private void RefreshGroupListSnapshot()
    {
        var source = _groupList;

        IReadOnlyList<StatusGroup> snapshot = source.Count == 0
            ? Array.Empty<StatusGroup>()
            : source.ToArray();

        Volatile.Write(ref _groupListSnapshot, snapshot);
    }

    private void RefreshLastMessagesSnapshot()
    {
        var source = _lastMessages;

        IReadOnlyList<ChatItem> snapshot = source.Count == 0
            ? Array.Empty<ChatItem>()
            : source.ToArray();

        Volatile.Write(ref _lastMessagesSnapshot, snapshot);
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

