using System;
using System.Collections.Generic;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Routes in-app navigation by mutating SelectedMenuIndex
/// and toggling the AppSettings visibility flags.
/// </summary>
public sealed class MenuNavigationService : IMenuNavigationService
{
    private readonly Dictionary<string, Action<bool>> _settingsMap;
    private readonly Action<int> _setPageIndex;
    private readonly IUiDispatcher _dispatcher;
    private Action? _expandPrivacy;
    private Action<string>? _scrollToSection;

    public MenuNavigationService(AppSettings appSettings, Action<int> setPageIndex, IUiDispatcher dispatcher)
    {
        _setPageIndex = setPageIndex;
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
            { "Settings_Time", v => appSettings.Settings_Time = v },
            { "Settings_HeartRate", v => appSettings.Settings_HeartRate = v },
            { "Settings_Status", v => appSettings.Settings_Status = v },
            { "Settings_VrcRadar", v => appSettings.Settings_VrcRadar = v }
        };
    }

    /// <summary>Wires the action that expands the Privacy section in the Options page.</summary>
    public void SetExpandPrivacyAction(Action expandPrivacy) => _expandPrivacy = expandPrivacy;

    /// <summary>Wires the callback that scrolls the Options page to a named section.</summary>
    public void SetScrollToSectionAction(Action<string> scrollToSection) => _scrollToSection = scrollToSection;

    public void ActivateSetting(string settingName)
    {
        if (_settingsMap.ContainsKey(settingName))
        {
            foreach (var setting in _settingsMap)
            {
                setting.Value(setting.Key == settingName);
            }
            NavigateToPage(3);
            _scrollToSection?.Invoke(settingName);
        }
    }

    public void NavigateToPage(int pageIndex)
    {
        if (pageIndex >= 0 && pageIndex <= 3)
            _dispatcher.BeginInvoke(() => _setPageIndex(pageIndex));
    }

    public void NavigateToPrivacy()
    {
        _dispatcher.BeginInvoke(() =>
        {
            _setPageIndex(3);
            _expandPrivacy?.Invoke();
        });
    }
}
