using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels
{
    /// <summary>
    /// Page ViewModel for the Status page. Owns add/delete/edit, sorting, groups, selection mode, and cycling.
    /// </summary>
    public partial class StatusPageViewModel : ObservableObject
    {
        private const string DefaultGroupName = "Default";
        private const string BoxColorNormal = "#FF6B5F98";
        private const string BoxColorWarning = "#FFFF9393";

        private readonly ChatStatusDisplayState _chatStatus;
        private readonly IAppState _appState;
        private readonly IStatusListService _statusListService;
        private readonly IMenuNavigationService _menuNav;

        private ICollectionView? _filteredView;
        private readonly Dictionary<StatusSortField, bool> _sortDirections = new();
        private bool _initialGroupRestored;

        [ObservableProperty] private StatusGroup? _selectedGroup;
        [ObservableProperty] private StatusSortField _currentSortField = StatusSortField.CreationDate;
        [ObservableProperty] private bool _isGroupDropdownOpen;
        [ObservableProperty] private bool _isSelectionMode;
        [ObservableProperty] private string _newGroupName = string.Empty;

        public ChatStatusDisplayState ChatStatus { get; }
        public AppSettings AppSettings { get; }
        public ObservableCollection<StatusItem> SelectedItems { get; } = new();

        /// <summary>True when any item is currently being inline-edited.</summary>
        public bool IsAnyItemEditing => _chatStatus.StatusList.Any(i => i.IsEditing);

        public ICollectionView? FilteredView
        {
            get => _filteredView;
            private set { _filteredView = value; OnPropertyChanged(); }
        }

        public string SortDirectionSymbol
        {
            get
            {
                bool descending = _sortDirections.GetValueOrDefault(CurrentSortField, true);
                return descending ? "↓" : "↑";
            }
        }

        public string SelectedGroupDisplayName
            => SelectedGroup?.Name ?? "All groups";

        public StatusPageViewModel(
            ChatStatusDisplayState chatStatus,
            IAppState appState,
            IStatusListService statusListService,
            IMenuNavigationService menuNav,
            ISettingsProvider<AppSettings> appSettingsProvider)
        {
            _chatStatus = chatStatus;
            _appState = appState;
            _statusListService = statusListService;
            _menuNav = menuNav;
            ChatStatus = chatStatus;
            AppSettings = appSettingsProvider.Value;

            chatStatus.PropertyChanged += OnChatStatusPropertyChanged;
            RebuildFilteredView();
        }

        private void OnChatStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ChatStatusDisplayState.StatusList)
                               or nameof(ChatStatusDisplayState.GroupList))
            {
                Application.Current?.Dispatcher.BeginInvoke(RebuildFilteredView);
            }
        }

        private void RebuildFilteredView()
        {
            foreach (var item in _chatStatus.StatusList)
                item.PropertyChanged -= OnItemPropertyChanged;

            _chatStatus.StatusList.CollectionChanged -= OnStatusListCollectionChanged;

            var view = CollectionViewSource.GetDefaultView(_chatStatus.StatusList);
            view.Filter = FilterItem;

            foreach (var item in _chatStatus.StatusList)
                item.PropertyChanged += OnItemPropertyChanged;

            _chatStatus.StatusList.CollectionChanged += OnStatusListCollectionChanged;

            ApplySortDescriptions(view);
            FilteredView = view;

            if (!_initialGroupRestored && _chatStatus.GroupList.Count > 0)
            {
                _initialGroupRestored = true;
                string savedId = AppSettings.LastSelectedGroupId;
                if (string.IsNullOrEmpty(savedId))
                {
                    // "All groups" was selected
                    SelectedGroup = null;
                }
                else
                {
                    SelectedGroup = _chatStatus.GroupList.FirstOrDefault(g => g.GroupId == savedId)
                                    ?? _chatStatus.GroupList.FirstOrDefault(g => g.Name == DefaultGroupName)
                                    ?? _chatStatus.GroupList.FirstOrDefault();
                }
            }
        }

        private void OnStatusListCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (StatusItem item in e.NewItems) item.PropertyChanged += OnItemPropertyChanged;
            if (e.OldItems != null)
                foreach (StatusItem item in e.OldItems) item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StatusItem.GroupId))
            {
                _filteredView?.Refresh();
                _statusListService.RequestSave();
            }
            if (e.PropertyName == nameof(StatusItem.IsEditing))
            {
                OnPropertyChanged(nameof(IsAnyItemEditing));
                EnterSelectionModeCommand.NotifyCanExecuteChanged();
            }
        }

        private bool FilterItem(object obj)
        {
            if (obj is not StatusItem item) return false;
            if (SelectedGroup == null) return true;
            return item.GroupId == SelectedGroup.GroupId;
        }

        private void ApplySortDescriptions(ICollectionView view)
        {
            view.SortDescriptions.Clear();
            bool descending = _sortDirections.GetValueOrDefault(CurrentSortField, true);
            var dir = descending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            string prop = CurrentSortField switch
            {
                StatusSortField.LastUsed => nameof(StatusItem.LastUsed),
                StatusSortField.MyCycles => nameof(StatusItem.UseInCycle),
                StatusSortField.CreationDate => nameof(StatusItem.CreationDate),
                StatusSortField.LastEdited => nameof(StatusItem.LastEdited),
                _ => nameof(StatusItem.CreationDate)
            };
            view.SortDescriptions.Add(new SortDescription(prop, dir));
        }

        // ── sort ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void SortByField(StatusSortField field)
        {
            CurrentSortField = field;
            if (_filteredView != null) ApplySortDescriptions(_filteredView);
            OnPropertyChanged(nameof(SortDirectionSymbol));
        }

        [RelayCommand]
        private void ToggleSortDirection()
        {
            bool current = _sortDirections.GetValueOrDefault(CurrentSortField, true);
            _sortDirections[CurrentSortField] = !current;
            if (_filteredView != null) ApplySortDescriptions(_filteredView);
            OnPropertyChanged(nameof(SortDirectionSymbol));
        }

        partial void OnCurrentSortFieldChanged(StatusSortField value)
            => OnPropertyChanged(nameof(SortDirectionSymbol));

        // ── group commands ────────────────────────────────────────────────────

        [RelayCommand]
        private void SelectGroup(StatusGroup? group)
        {
            SelectedGroup = group;
            IsGroupDropdownOpen = false;
        }

        partial void OnSelectedGroupChanged(StatusGroup? value)
        {
            _filteredView?.Refresh();
            OnPropertyChanged(nameof(SelectedGroupDisplayName));
            OnPropertyChanged(nameof(IsAllGroupsSelected));

            // Persist selected group across restarts
            AppSettings.LastSelectedGroupId = value?.GroupId ?? "";

            if (AppSettings.CycleOverrideCurrentGroup)
                AppSettings.CycleOverrideGroupId = value?.GroupId ?? "";

            // Update popup visual selection indicator
            foreach (var g in _chatStatus.GroupList)
                g.IsPopupSelected = (g == value);
        }

        /// <summary>True when "All groups" is the active filter (SelectedGroup is null).</summary>
        public bool IsAllGroupsSelected => SelectedGroup == null;

        [RelayCommand]
        private void ConfirmAddGroup()
        {
            string trimmed = NewGroupName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 50) return;
            _statusListService.AddGroup(trimmed);
            NewGroupName = string.Empty;
        }

        [RelayCommand]
        private void BeginRenameGroup(StatusGroup? group)
        {
            if (group == null || group.Name == DefaultGroupName) return;
            group.RenameBuffer = group.Name;
            group.IsRenaming = true;
        }

        [RelayCommand]
        private void ConfirmRenameGroup(StatusGroup? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.RenameBuffer)) return;
            _statusListService.RenameGroup(group.GroupId, group.RenameBuffer);
            group.IsRenaming = false;
            OnPropertyChanged(nameof(SelectedGroupDisplayName));
        }

        [RelayCommand]
        private void CancelRenameGroup(StatusGroup? group)
        {
            if (group == null) return;
            group.IsRenaming = false;
            group.RenameBuffer = string.Empty;
        }

        [RelayCommand]
        private void DeleteGroup(StatusGroup? group)
        {
            if (group == null || group.Name == DefaultGroupName) return;
            if (SelectedGroup?.GroupId == group.GroupId)
                SelectedGroup = _chatStatus.GroupList.FirstOrDefault(g => g.Name == DefaultGroupName);
            _statusListService.DeleteGroup(group.GroupId);
            IsGroupDropdownOpen = false;
        }

        [RelayCommand]
        private void ToggleGroupCycleActive(StatusGroup? group)
        {
            if (group == null) return;
            // IsActiveForCycle is already toggled by the ToggleButton's IsChecked binding
            _statusListService.SaveStatusList();
        }

        [RelayCommand]
        private void ToggleCycleOverride()
        {
            // IsChecked TwoWay binding already toggled CycleOverrideCurrentGroup
            if (AppSettings.CycleOverrideCurrentGroup && SelectedGroup != null)
                AppSettings.CycleOverrideGroupId = SelectedGroup.GroupId;
            else if (!AppSettings.CycleOverrideCurrentGroup)
                AppSettings.CycleOverrideGroupId = "";
        }

        // ── selection mode ────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanEnterSelectionMode))]
        private void EnterSelectionMode()
        {
            SelectedItems.Clear();
            foreach (var item in _chatStatus.StatusList) item.IsSelected = false;
            IsSelectionMode = true;
        }

        private bool CanEnterSelectionMode() => !IsAnyItemEditing && _chatStatus.StatusList.Count > 0;

        [RelayCommand]
        private void ExitSelectionMode()
        {
            IsSelectionMode = false;
            SelectedItems.Clear();
            foreach (var item in _chatStatus.StatusList) item.IsSelected = false;
        }

        [RelayCommand]
        private void ToggleItemSelected(StatusItem? item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
            if (item.IsSelected) SelectedItems.Add(item);
            else SelectedItems.Remove(item);
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in _chatStatus.StatusList)
            {
                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    SelectedItems.Add(item);
                }
            }
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var item in SelectedItems.ToList())
                item.IsSelected = false;
            SelectedItems.Clear();
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            foreach (var item in SelectedItems.ToList())
            {
                HandleEggDelete(item);
                _chatStatus.StatusList.Remove(item);
            }
            SelectedItems.Clear();
            IsSelectionMode = false;
            _statusListService.SaveStatusList();
        }

        [RelayCommand]
        private void CloneSelected()
        {
            string? defaultGroupId = _chatStatus.GroupList.FirstOrDefault(g => g.Name == "Default")?.GroupId;
            foreach (var item in SelectedItems.ToList())
            {
                _chatStatus.StatusList.Add(new StatusItem
                {
                    CreationDate = DateTime.Now,
                    IsActive = false,
                    IsFavorite = item.IsFavorite,
                    UseInCycle = item.UseInCycle,
                    msg = item.msg,
                    MSGID = Random.Shared.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax),
                    GroupId = item.GroupId ?? defaultGroupId
                });
            }
            ExitSelectionMode();
            _statusListService.SaveStatusList();
        }

        [RelayCommand]
        private void MoveSelectedToGroup(StatusGroup? group)
        {
            if (group == null) return;
            foreach (var item in SelectedItems)
                item.GroupId = group.GroupId;
            ExitSelectionMode();
            _statusListService.SaveStatusList();
        }

        // ── navigation ────────────────────────────────────────────────────────

        [RelayCommand]
        private void ActivateSetting(string settingName)
            => _menuNav.ActivateSetting(settingName);

        [RelayCommand]
        private void ClearStatusInput()
            => _chatStatus.NewStatusItemTxt = string.Empty;

        // ── CRUD ──────────────────────────────────────────────────────────────

        public void SaveStatusList() => _statusListService.SaveStatusList();

        [RelayCommand]
        public void AddStatus()
        {
            string text = _chatStatus.NewStatusItemTxt?.Trim() ?? "";
            if (text.Length <= 0 || text.Length > Core.Constants.MaxChatMessageLength) return;

            bool isActive = _chatStatus.StatusList.Count == 0;
            string? groupId = SelectedGroup?.GroupId
                              ?? _chatStatus.GroupList.FirstOrDefault(g => g.Name == DefaultGroupName)?.GroupId;

            _chatStatus.StatusList.Add(new StatusItem
            {
                CreationDate = DateTime.Now,
                IsActive = isActive,
                IsFavorite = false,
                msg = text,
                MSGID = Random.Shared.Next(Core.Constants.StatusRandomIdMin, Core.Constants.StatusRandomIdMax),
                GroupId = groupId
            });

            if (text.Equals("sr4 series", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("boihanny", StringComparison.OrdinalIgnoreCase))
            {
                _appState.Egg_Dev = true;
                MessageBox.Show("u found the dev egggmoooodeee go to options", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (text.Equals("bussyboys", StringComparison.OrdinalIgnoreCase))
            {
                _appState.BussyBoysMode = true;
                MessageBox.Show("Bussy Boysss letsss goooo, go to afk options", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _chatStatus.NewStatusItemTxt = string.Empty;
            SaveStatusList();
        }

        [RelayCommand]
        public void DeleteStatus(StatusItem? item)
        {
            if (item == null) return;
            try
            {
                HandleEggDelete(item);
                _chatStatus.StatusList.Remove(item);
                SaveStatusList();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
                try { _statusListService.LoadStatusList(); }
                catch { /* best-effort reload */ }
            }
        }

        [RelayCommand]
        public void CancelEdit(StatusItem? item)
        {
            if (item == null) return;
            item.editMsg = string.Empty;
            item.IsEditing = false;
        }

        [RelayCommand]
        public void ToggleFavorite(StatusItem? item)
        {
            if (item == null) return;
            item.UseInCycle = !item.UseInCycle;
            _statusListService.RequestSave();
        }

        public void UpdateStatusBoxCount(int count)
        {
            _chatStatus.StatusBoxCount = $"{count}/140";
            if (count > 140)
            {
                int overmax = count - 140;
                _chatStatus.StatusBoxColor = BoxColorWarning;
                _chatStatus.StatusTopBarTxt = $"You're soaring past the 140 char limit by {overmax}. Reign in that message!";
            }
            else if (count == 0)
            {
                _chatStatus.StatusBoxColor = BoxColorNormal;
                _chatStatus.StatusTopBarTxt = string.Empty;
            }
            else
            {
                _chatStatus.StatusBoxColor = BoxColorNormal;
                _chatStatus.StatusTopBarTxt = count > 22
                    ? "Buckle up! Keep it tight to 20-25 or integrations may suffer."
                    : string.Empty;
            }
        }

        [RelayCommand]
        private void ActivateStatus(StatusItem? item)
        {
            try
            {
                foreach (var i in _chatStatus.StatusList)
                {
                    i.IsActive = i == item;
                    if (i == item) i.LastUsed = DateTime.Now;
                }
                _statusListService.SaveStatusList();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        public void BeginEdit(StatusItem? item)
        {
            if (item == null) return;
            item.editMsg = item.msg;
        }

        public void ConfirmEdit(StatusItem? item)
        {
            if (item == null) return;
            string trimmed = item.editMsg?.Trim() ?? "";
            if (trimmed.Length > 0 && trimmed.Length < 145)
            {
                item.msg = trimmed;
                item.IsEditing = false;
                item.editMsg = string.Empty;
                item.LastEdited = DateTime.Now;
                SaveStatusList();
            }
        }

        private void HandleEggDelete(StatusItem item)
        {
            if (item.msg.Equals("sr4 series", StringComparison.OrdinalIgnoreCase) ||
                item.msg.Equals("boihanny", StringComparison.OrdinalIgnoreCase))
            {
                _appState.Egg_Dev = false;
                MessageBox.Show("damn u left the dev egggmoooodeee", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (item.msg.Equals("bussyboys", StringComparison.OrdinalIgnoreCase))
            {
                _appState.BussyBoysMode = false;
                MessageBox.Show("damn u left the bussyboys mode", "Egg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
