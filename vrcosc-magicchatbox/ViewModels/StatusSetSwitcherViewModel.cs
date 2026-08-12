using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Status;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels;

/// <summary>
/// One entry in the side panel's set picker. A null <see cref="GroupId"/> is the "every set" entry,
/// which is not a group and never gets one.
/// </summary>
public partial class StatusSetOption : ObservableObject
{
    public StatusSetOption(string? groupId, string name)
    {
        GroupId = groupId;
        Name = name;
    }

    public string? GroupId { get; }
    public bool IsEveryGroup => GroupId == null;

    [ObservableProperty] private string _name;
    [ObservableProperty] private int _cyclingCount;
}

/// <summary>
/// Switches which set of statuses goes to the chatbox, from the side panel, without opening the
/// Status page. It is a view onto the cycle-override settings rather than a second source of truth:
/// pinning a group on the Status page and choosing it here are the same act, so the two always agree.
/// </summary>
public partial class StatusSetSwitcherViewModel : ObservableObject
{
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly AppSettings _appSettings;
    private readonly IStatusListService _statusList;
    private readonly IMenuNavigationService _menuNav;
    private readonly IUiDispatcher _dispatcher;

    private bool _applyingSelection;
    private ObservableCollection<StatusItem>? _observedStatusList;
    private ObservableCollection<StatusGroup>? _observedGroupList;

    public StatusSetSwitcherViewModel(
        ChatStatusDisplayState chatStatus,
        ISettingsProvider<AppSettings> appSettingsProvider,
        IStatusListService statusList,
        IMenuNavigationService menuNav,
        IUiDispatcher dispatcher)
    {
        _chatStatus = chatStatus;
        _appSettings = appSettingsProvider.Value;
        _statusList = statusList;
        _menuNav = menuNav;
        _dispatcher = dispatcher;

        Sets = new ObservableCollection<StatusSetOption>();

        _chatStatus.PropertyChanged += OnChatStatusChanged;
        _appSettings.PropertyChanged += OnAppSettingChanged;

        Rebuild();
    }

    public ObservableCollection<StatusSetOption> Sets { get; }

    [ObservableProperty] private StatusSetOption? _selectedSet;

    public AppSettings AppSettings => _appSettings;

    public string Summary => StatusSetSummary.Describe(
        SelectedSet?.CyclingCount ?? 0,
        _appSettings.CycleStatus,
        _appSettings.SwitchStatusInterval);

    /// <summary>True when the chosen set has nothing that will ever be sent, which is worth flagging.</summary>
    public bool IsEmptySet => (SelectedSet?.CyclingCount ?? 0) == 0;

    private void OnChatStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatStatusDisplayState.GroupList)
                           or nameof(ChatStatusDisplayState.StatusList))
            _dispatcher.BeginInvoke(Rebuild);
    }

    private void OnAppSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Someone else moved the live set - the pin on the Status page, or a group being deleted.
            case nameof(AppSettings.CycleOverrideGroupId):
            case nameof(AppSettings.CycleOverrideCurrentGroup):
                _dispatcher.BeginInvoke(SyncSelectionFromSettings);
                break;

            case nameof(AppSettings.CycleStatus):
            case nameof(AppSettings.SwitchStatusInterval):
                OnPropertyChanged(nameof(Summary));
                break;
        }
    }

    private void Rebuild()
    {
        // Loading and importing hand back brand new collections rather than mutating the old ones, so
        // a subscription taken once in the constructor goes stale and the counts quietly freeze.
        ObserveCollections();

        Sets.Clear();
        Sets.Add(new StatusSetOption(null, "Every set"));

        foreach (var group in _chatStatus.GroupList)
            Sets.Add(new StatusSetOption(group.GroupId, group.Name));

        RefreshCounts();
        SyncSelectionFromSettings();
    }

    private void ObserveCollections()
    {
        if (!ReferenceEquals(_observedStatusList, _chatStatus.StatusList))
        {
            if (_observedStatusList != null)
            {
                _observedStatusList.CollectionChanged -= OnStatusListChanged;
                foreach (var item in _observedStatusList)
                    item.PropertyChanged -= OnStatusItemChanged;
            }

            _observedStatusList = _chatStatus.StatusList;
            _observedStatusList.CollectionChanged += OnStatusListChanged;
            foreach (var item in _observedStatusList)
                item.PropertyChanged += OnStatusItemChanged;
        }

        if (!ReferenceEquals(_observedGroupList, _chatStatus.GroupList))
        {
            if (_observedGroupList != null)
                _observedGroupList.CollectionChanged -= OnGroupListChanged;

            _observedGroupList = _chatStatus.GroupList;
            _observedGroupList.CollectionChanged += OnGroupListChanged;
        }
    }

    private void OnGroupListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => _dispatcher.BeginInvoke(Rebuild);

    private void OnStatusListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (StatusItem item in e.OldItems)
                item.PropertyChanged -= OnStatusItemChanged;

        if (e.NewItems != null)
            foreach (StatusItem item in e.NewItems)
                item.PropertyChanged += OnStatusItemChanged;

        _dispatcher.BeginInvoke(RefreshCounts);
    }

    // Hearting a message or moving it between sets changes what the counts should say.
    private void OnStatusItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StatusItem.UseInCycle) or nameof(StatusItem.GroupId))
            _dispatcher.BeginInvoke(RefreshCounts);
    }

    private void RefreshCounts()
    {
        foreach (var set in Sets)
        {
            set.CyclingCount = set.IsEveryGroup
                ? CountCyclingAcrossActiveGroups()
                : _chatStatus.StatusList.Count(item => item.UseInCycle && item.GroupId == set.GroupId);
        }

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsEmptySet));
    }

    // Mirrors what the OSC provider does when no set is pinned: only groups still marked for cycling
    // take part, so the count matches what will actually be sent.
    private int CountCyclingAcrossActiveGroups()
    {
        var activeGroupIds = _chatStatus.GroupList
            .Where(g => g.IsActiveForCycle)
            .Select(g => g.GroupId)
            .ToHashSet();

        return _chatStatus.StatusList.Count(item =>
            item.UseInCycle && (item.GroupId == null || activeGroupIds.Contains(item.GroupId)));
    }

    private void SyncSelectionFromSettings()
    {
        StatusSetOption? match = null;

        if (_appSettings.CycleOverrideCurrentGroup && !string.IsNullOrEmpty(_appSettings.CycleOverrideGroupId))
            match = Sets.FirstOrDefault(s => s.GroupId == _appSettings.CycleOverrideGroupId);

        _applyingSelection = true;
        SelectedSet = match ?? Sets.FirstOrDefault();
        _applyingSelection = false;

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsEmptySet));
    }

    partial void OnSelectedSetChanged(StatusSetOption? value)
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsEmptySet));

        // Only a real user choice writes back; echoing the settings we just read would fight the pin.
        if (_applyingSelection || value == null)
            return;

        if (value.IsEveryGroup)
        {
            _appSettings.CycleOverrideCurrentGroup = false;
            _appSettings.CycleOverrideGroupId = string.Empty;
        }
        else
        {
            _appSettings.CycleOverrideCurrentGroup = true;
            _appSettings.CycleOverrideGroupId = value.GroupId!;

            // So the Status page opens on the set you just chose rather than wherever you left it.
            _appSettings.LastSelectedGroupId = value.GroupId!;
        }

        _statusList.RequestSave();
    }

    [RelayCommand]
    private void EnableCycling() => _appSettings.CycleStatus = true;

    [RelayCommand]
    private void OpenStatusPage() => _menuNav.NavigateToPage(1);
}
