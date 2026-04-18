using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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

    private ProcessInfo _lastProcessFocused;
    public ProcessInfo LastProcessFocused
    {
        get => _lastProcessFocused;
        set { if (SetProperty(ref _lastProcessFocused, value)) { } }
    }

    private ObservableCollection<ProcessInfo> _scannedApps = new();
    public ObservableCollection<ProcessInfo> ScannedApps
    {
        get => _scannedApps;
        set
        {
            if (_scannedApps != null)
                _scannedApps.CollectionChanged -= ScannedApps_CollectionChanged;
            if (SetProperty(ref _scannedApps, value))
            {
                if (_scannedApps != null)
                    _scannedApps.CollectionChanged += ScannedApps_CollectionChanged;
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
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    public void ScannedAppsItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "FocusCount")
        {
            _dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(ScannedApps).Refresh();
            });
        }
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
                _dispatcher.Invoke(() =>
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
    }

    private void ProcessInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        Resort();
    }
}
