using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.ViewModels.State;

/// <summary>
/// Owns ScannedApps collection, sorting, error state, and related commands
/// for the Window Activity section of the UI.
/// </summary>
public partial class WindowActivityDisplayState : ObservableObject
{
    private readonly object _lock = new();
    private readonly IUiDispatcher _dispatcher;
    private CollectionViewSource? _scannedAppsViewSource;
    private SortProperty _currentSortProperty;
    private readonly Dictionary<SortProperty, bool> _sortDirection = new()
    {
        { SortProperty.ProcessName, true },
        { SortProperty.FocusCount, true },
        { SortProperty.UsedNewMethod, true },
        { SortProperty.IsPrivateApp, true },
        { SortProperty.ShowInfo, true },
        { SortProperty.ApplyCustomAppName, true }
    };

    [ObservableProperty] private string _errorInWindowActivityMsg = "Error without information";
    [ObservableProperty] private bool _errorInWindowActivity = false;
    [ObservableProperty] private string _deletedAppslabel = string.Empty;
    [ObservableProperty] private string _lastUsedSortDirection = string.Empty;
    [ObservableProperty] private string _appFilterText = string.Empty;
    [ObservableProperty] private bool _showImportantAppsOnly = true;
    [ObservableProperty] private int _minimumFocusCount = 0;
    [ObservableProperty] private string _filteredAppsSummary = "Showing 0 / 0 apps";

    private ProcessInfo _lastProcessFocused;
    public ProcessInfo LastProcessFocused
    {
        get => _lastProcessFocused;
        set { if (SetProperty(ref _lastProcessFocused, value)) { } }
    }

    private ObservableCollection<ProcessInfo> _scannedApps = new();
    public ICollectionView? ScannedAppsView => _scannedAppsViewSource?.View;

    public ObservableCollection<ProcessInfo> ScannedApps
    {
        get => _scannedApps;
        set
        {
            if (_scannedApps != null)
            {
                _scannedApps.CollectionChanged -= ScannedApps_CollectionChanged;
                foreach (ProcessInfo processInfo in _scannedApps)
                    processInfo.PropertyChanged -= ProcessInfo_PropertyChanged;
            }

            if (SetProperty(ref _scannedApps, value ?? new ObservableCollection<ProcessInfo>()))
            {
                _scannedApps.CollectionChanged += ScannedApps_CollectionChanged;
                foreach (ProcessInfo processInfo in _scannedApps)
                    processInfo.PropertyChanged += ProcessInfo_PropertyChanged;

                UpdateScannedAppsViewSource();
            }
        }
    }

    [RelayCommand]
    private void SortByProcessName() => SortScannedApps(SortProperty.ProcessName);

    [RelayCommand]
    private void SortByFocusCount() => SortScannedApps(SortProperty.FocusCount);

    [RelayCommand]
    private void SortByUsedNewMethod() => SortScannedApps(SortProperty.UsedNewMethod);

    [RelayCommand]
    private void SortByIsPrivateApp() => SortScannedApps(SortProperty.IsPrivateApp);

    [RelayCommand]
    private void SortByIsShowInfoApp() => SortScannedApps(SortProperty.ShowInfo);

    [RelayCommand]
    private void SortByApplyCustomAppName() => SortScannedApps(SortProperty.ApplyCustomAppName);

    public WindowActivityDisplayState(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _scannedApps.CollectionChanged += ScannedApps_CollectionChanged;
        _dispatcher.Invoke(InitializeScannedAppsViewSource);
    }

    partial void OnAppFilterTextChanged(string value) => RefreshScannedAppsViewDebounced();

    partial void OnShowImportantAppsOnlyChanged(bool value) => RefreshScannedAppsViewDebounced();

    partial void OnMinimumFocusCountChanged(int value)
    {
        if (value < 0)
        {
            MinimumFocusCount = 0;
            return;
        }

        RefreshScannedAppsViewDebounced();
    }

    public void SortScannedApps(SortProperty sortProperty)
    {
        if (!_sortDirection.ContainsKey(sortProperty))
        {
            Logging.WriteException(new Exception($"No sortDirection: {sortProperty}"), MSGBox: false);
            return;
        }
        try
        {
            _currentSortProperty = sortProperty;
            var isAscending = _sortDirection[sortProperty];
            _sortDirection[sortProperty] = !isAscending;
            UpdateSortedApps();
            OnPropertyChanged(nameof(ScannedApps));
            RefreshScannedAppsView();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void ScannedAppsItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessInfo.FocusCount))
            RefreshScannedAppsView();
    }

    private void UpdateSortedApps()
    {
        lock (_lock)
        {
            ObservableCollection<ProcessInfo> tempList = null;
            try
            {
                var copiedList = ScannedApps.ToList();
                IOrderedEnumerable<ProcessInfo> sortedScannedApps = null;

                switch (_currentSortProperty)
                {
                    case SortProperty.ProcessName:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.ProcessName)
                            : copiedList.OrderByDescending(p => p.ProcessName);
                        break;
                    case SortProperty.UsedNewMethod:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.UsedNewMethod)
                            : copiedList.OrderByDescending(p => p.UsedNewMethod);
                        break;
                    case SortProperty.ApplyCustomAppName:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.ApplyCustomAppName)
                            : copiedList.OrderByDescending(p => p.ApplyCustomAppName);
                        break;
                    case SortProperty.IsPrivateApp:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.IsPrivateApp)
                            : copiedList.OrderByDescending(p => p.IsPrivateApp);
                        break;
                    case SortProperty.FocusCount:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.FocusCount)
                            : copiedList.OrderByDescending(p => p.FocusCount);
                        break;
                    case SortProperty.ShowInfo:
                        sortedScannedApps = _sortDirection[_currentSortProperty]
                            ? copiedList.OrderBy(p => p.ShowTitle)
                            : copiedList.OrderByDescending(p => p.ShowTitle);
                        break;
                }

                if (sortedScannedApps != null && sortedScannedApps.Any())
                    tempList = new ObservableCollection<ProcessInfo>(sortedScannedApps);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }

            if (tempList != null)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    ScannedApps = tempList;
                });
            }
        }
    }

    private void Resort()
    {
        if (_currentSortProperty != default)
            UpdateSortedApps();
    }

    private void ScannedApps_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Resort();
        if (e.OldItems != null)
        {
            foreach (ProcessInfo processInfo in e.OldItems)
                processInfo.PropertyChanged -= ProcessInfo_PropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (ProcessInfo processInfo in e.NewItems)
                processInfo.PropertyChanged += ProcessInfo_PropertyChanged;
        }

        RefreshScannedAppsView();
    }

    private void ProcessInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Resort();
        RefreshScannedAppsView();
    }

    private void UpdateScannedAppsViewSource()
    {
        void UpdateSource()
        {
            InitializeScannedAppsViewSource();
            _scannedAppsViewSource.Source = ScannedApps;
            OnPropertyChanged(nameof(ScannedAppsView));
            RefreshScannedAppsView();
        }

        if (_dispatcher.CheckAccess())
            UpdateSource();
        else
            _dispatcher.BeginInvoke(UpdateSource);
    }

    private void RefreshScannedAppsView()
    {
        void RefreshView()
        {
            try
            {
                _scannedAppsViewSource?.View?.Refresh();
                UpdateFilteredAppsSummary();
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }

        if (_dispatcher.CheckAccess())
            RefreshView();
        else
            _dispatcher.BeginInvoke(RefreshView);
    }

    private void ScannedAppsViewSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = e.Item is ProcessInfo processInfo && MatchesFilters(processInfo);
    }

    private bool MatchesFilters(ProcessInfo processInfo)
    {
        if (MinimumFocusCount > 0 && processInfo.FocusCount < MinimumFocusCount)
            return false;

        if (ShowImportantAppsOnly && !IsImportantApp(processInfo))
            return false;

        var searchText = AppFilterText?.Trim();
        if (string.IsNullOrEmpty(searchText))
            return true;

        return Contains(processInfo.ProcessName, searchText) || Contains(processInfo.CustomAppName, searchText);
    }

    private static bool Contains(string? value, string searchText)
        => !string.IsNullOrEmpty(value) && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private static bool IsImportantApp(ProcessInfo processInfo)
        => processInfo.FocusCount > 1
           || processInfo.ShowTitle
           || processInfo.ApplyCustomAppName
           || processInfo.IsPrivateApp
           || processInfo.UseCustomRegex;

    private void UpdateFilteredAppsSummary()
    {
        int totalCount = ScannedApps?.Count ?? 0;
        int visibleCount = _scannedAppsViewSource?.View?.Cast<object>().Count() ?? 0;
        FilteredAppsSummary = $"Showing {visibleCount} / {totalCount} apps";
    }

    private void InitializeScannedAppsViewSource()
    {
        if (_scannedAppsViewSource != null)
            return;

        _scannedAppsViewSource = new CollectionViewSource { Source = _scannedApps };
        _scannedAppsViewSource.Filter += ScannedAppsViewSource_Filter;
        OnPropertyChanged(nameof(ScannedAppsView));
        RefreshScannedAppsView();
    }

    private CancellationTokenSource? _filterDebounceCts;

    private async void RefreshScannedAppsViewDebounced()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;

        try
        {
            await Task.Delay(150, token); // 150ms debounce
            if (!token.IsCancellationRequested)
            {
                RefreshScannedAppsView();
            }
        }
        catch (TaskCanceledException) { }
    }

}
