using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Routes in-app navigation by mutating SelectedMenuIndex
/// and toggling the AppSettings visibility flags.
/// </summary>
public sealed class MenuNavigationService : IMenuNavigationService
{
    private const int MaxHistoryDepth = 3;

    private readonly Dictionary<string, Action<bool>> _settingsMap;
    private readonly Action<int> _setPageIndex;
    private readonly Func<int> _getPageIndex;
    private readonly IUiDispatcher _dispatcher;
    private readonly Stack<int> _backHistory = new();
    private readonly Stack<int> _forwardHistory = new();
    private Action? _expandPrivacy;
    private Action<string>? _scrollToSection;

    public MenuNavigationService(AppSettings appSettings, Action<int> setPageIndex, Func<int> getPageIndex, IUiDispatcher dispatcher)
    {
        _setPageIndex = setPageIndex;
        _getPageIndex = getPageIndex;
        _dispatcher = dispatcher;
        _settingsMap = new Dictionary<string, Action<bool>>
        {
            { "Settings_WindowActivity", v => appSettings.Settings_WindowActivity = v },
            { "Settings_MediaLink", v => appSettings.Settings_MediaLink = v },
            { "Settings_OpenAI", v => appSettings.Settings_OpenAI = v },
            { "Settings_Chatting", v => appSettings.Settings_Chatting = v },
            { "Settings_ComponentStats", v => appSettings.Settings_ComponentStats = v },
            { "Settings_TrackerBattery", v => appSettings.Settings_TrackerBattery = v },
            { "Settings_NetworkStatistics", v => appSettings.Settings_NetworkStatistics = v },
            { "Settings_AppOptions", v => appSettings.Settings_AppOptions = v },
            { "Settings_TTS", v => appSettings.Settings_TTS = v },
            { "Settings_Weather", v => appSettings.Settings_Weather = v },
            { "Settings_Twitch", v => appSettings.Settings_Twitch = v },
            { "Settings_Discord", v => appSettings.Settings_Discord = v },
            { "Settings_Spotify", v => appSettings.Settings_Spotify = v },
            { "Settings_Time", v => appSettings.Settings_Time = v },
            { "Settings_HeartRate", v => appSettings.Settings_HeartRate = v },
            { "Settings_Status", v => appSettings.Settings_Status = v },
            { "Settings_VrcRadar", v => appSettings.Settings_VrcRadar = v },
            { "Settings_EggDev", v => appSettings.SettingsDev = v }
        };
    }

    /// <summary>Wires the action that expands the Privacy section in the Options page.</summary>
    public void SetExpandPrivacyAction(Action expandPrivacy) => _expandPrivacy = expandPrivacy;

    /// <summary>Wires the callback that scrolls the Options page to a named section.</summary>
    public void SetScrollToSectionAction(Action<string> scrollToSection) => _scrollToSection = scrollToSection;

    public void ActivateSetting(string settingName)
    {
        if (settingName == "Settings_Privacy")
        {
            NavigateToPrivacy();
            return;
        }

        if (!_settingsMap.ContainsKey(settingName))
        {
            Logging.WriteInfo($"Navigation: Unknown settings section '{settingName}'.");
            return;
        }

        foreach (var setting in _settingsMap)
        {
            setting.Value(setting.Key == settingName);
        }

        NavigateToPage(3);
        _dispatcher.BeginInvoke(() => _scrollToSection?.Invoke(settingName));
    }

    public void NavigateToPage(int pageIndex)
        => NavigateToPage(pageIndex, recordHistory: true);

    public void NavigateBack()
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_backHistory.Count == 0) return;

            int current = _getPageIndex();
            int target = _backHistory.Pop();
            PushHistory(_forwardHistory, current);
            NavigateToPageCore(target);
        });
    }

    public void NavigateForward()
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_forwardHistory.Count == 0) return;

            int current = _getPageIndex();
            int target = _forwardHistory.Pop();
            PushHistory(_backHistory, current);
            NavigateToPageCore(target);
        });
    }

    public void NavigateToPrivacy()
    {
        _dispatcher.BeginInvoke(() =>
        {
            NavigateToPageCore(3, recordHistory: true);
            _expandPrivacy?.Invoke();
            _scrollToSection?.Invoke("Settings_Privacy");
        });
    }

    private void NavigateToPage(int pageIndex, bool recordHistory)
    {
        if (pageIndex < 0 || pageIndex > 3)
        {
            Logging.WriteInfo($"Navigation: Ignored invalid page index {pageIndex}.");
            return;
        }

        _dispatcher.BeginInvoke(() => NavigateToPageCore(pageIndex, recordHistory));
    }

    private void NavigateToPageCore(int pageIndex, bool recordHistory = false)
    {
        int current = _getPageIndex();
        if (current == pageIndex) return;

        if (recordHistory)
        {
            PushHistory(_backHistory, current);
            _forwardHistory.Clear();
        }

        _setPageIndex(pageIndex);
    }

    private static void PushHistory(Stack<int> history, int pageIndex)
    {
        if (pageIndex < 0 || pageIndex > 3) return;
        if (history.Count > 0 && history.Peek() == pageIndex) return;

        var items = new List<int>(history);
        items.Insert(0, pageIndex);
        if (items.Count > MaxHistoryDepth)
            items.RemoveRange(MaxHistoryDepth, items.Count - MaxHistoryDepth);

        history.Clear();
        for (int i = items.Count - 1; i >= 0; i--)
            history.Push(items[i]);
    }
}
