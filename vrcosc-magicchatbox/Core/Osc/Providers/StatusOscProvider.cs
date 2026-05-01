using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

/// <summary>
/// Adapter: Status message + AFK override → OSC segment.
/// Includes status cycling (stateful pre-build step).
/// </summary>
public sealed class StatusOscProvider : IOscProvider
{
    private readonly Lazy<IModuleHost> _modules;
    private readonly IntegrationSettings _intgr;
    private readonly AppSettings _app;
    private readonly TimeSettings _time;
    private readonly ChatStatusDisplayState _chatStatus;
    private readonly OscDisplayState _oscDisplay;
    private readonly EmojiService _emojis;
    private readonly IAppState _appState;

    public StatusOscProvider(
        Lazy<IModuleHost> modules,
        ISettingsProvider<IntegrationSettings> intgrProvider,
        ISettingsProvider<AppSettings> appProvider,
        ISettingsProvider<TimeSettings> timeProvider,
        ChatStatusDisplayState chatStatus,
        OscDisplayState oscDisplay,
        EmojiService emojis,
        IAppState appState)
    {
        _modules = modules;
        _intgr = intgrProvider.Value;
        _app = appProvider.Value;
        _time = timeProvider.Value;
        _chatStatus = chatStatus;
        _oscDisplay = oscDisplay;
        _emojis = emojis;
        _appState = appState;
    }

    public string SortKey => "Status";
    public string UiKey => "Status";
    public int Priority => 10;

    public bool IsEnabledForCurrentMode(bool isVR)
    {
        var afk = _modules.Value.Afk;
        bool afkActive = afk != null && afk.IsAfk && afk.Settings.EnableAfkDetection;
        if (afkActive) return true;

        return _intgr.IntgrStatus && (isVR ? _intgr.IntgrStatus_VR : _intgr.IntgrStatus_DESKTOP);
    }

    public OscSegment? TryBuild(OscBuildContext context)
    {
        var afk = _modules.Value.Afk;

        // AFK override takes priority
        if (afk != null && afk.IsAfk && afk.Settings.EnableAfkDetection)
        {
            string afkText = afk.GenerateAFKString();
            if (!string.IsNullOrEmpty(afkText))
            {
                // In BussyBoysMode + MultiMODE, AFK doesn't block status — we fall through
                if (!(_appState.BussyBoysMode && _time.BussyBoysMultiMODE))
                    return new OscSegment { Text = afkText };

                // AFK segment still shown, but status also contributes below
                // For simplicity, just return AFK when both active
                return new OscSegment { Text = afkText };
            }
        }

        if (!_intgr.IntgrStatus || _chatStatus.StatusList == null || !_chatStatus.StatusList.Any())
            return null;

        // Cycle status if enabled (stateful pre-build step)
        if (_app.CycleStatus)
            CycleStatus();

        StatusItem? active = _chatStatus.StatusList.FirstOrDefault(item => item.IsActive);
        if (active == null) return null;

        active.LastUsed = DateTime.Now;
        string icon = _emojis.GetNextEmoji();
        string text = _app.PrefixIconStatus ? $"{icon} {active.msg}" : active.msg;

        return string.IsNullOrEmpty(text) ? null : new OscSegment { Text = text };
    }

    #region Status Cycling (moved from OSCController)

    private void CycleStatus()
    {
        if (_chatStatus.StatusList == null || !_chatStatus.StatusList.Any())
            return;

        if (_app.CycleOverrideCurrentGroup && !string.IsNullOrEmpty(_app.CycleOverrideGroupId))
        {
            var overrideGroupId = _app.CycleOverrideGroupId;
            var overrideItems = _chatStatus.StatusList
                .Where(item => item.UseInCycle && item.GroupId == overrideGroupId)
                .ToList();

            if (overrideItems.Count > 0)
            {
                CycleItems(overrideItems);
                return;
            }

        }

        // Build candidate list: UseInCycle AND group must be active for cycling
        var activeGroupIds = _chatStatus.GroupList
            .Where(g => g.IsActiveForCycle)
            .Select(g => g.GroupId)
            .ToHashSet();

        var cycleItems = _chatStatus.StatusList
            .Where(item => item.UseInCycle
                           && (item.GroupId == null || activeGroupIds.Contains(item.GroupId)))
            .ToList();

        if (cycleItems.Count == 0) return;

        CycleItems(cycleItems);
    }

    /// <summary>
    /// Advances to the next item in <paramref name="cycleItems"/> respecting interval and random mode.
    /// </summary>
    private void CycleItems(System.Collections.Generic.List<StatusItem> cycleItems)
    {
        if (DateTime.Now - _oscDisplay.LastSwitchCycle < TimeSpan.FromSeconds(_app.SwitchStatusInterval))
            return;

        if (_app.IsRandomCycling)
        {
            foreach (var item in _chatStatus.StatusList)
                item.IsActive = false;

            try
            {
                var rnd = new Random();
                var weights = cycleItems.Select(item =>
                {
                    var timeWeight = (DateTime.Now - item.LastUsed).TotalSeconds;
                    return timeWeight * rnd.NextDouble();
                }).ToList();

                int selected = WeightedRandomIndex(weights);
                cycleItems[selected].IsActive = true;
                _oscDisplay.LastSwitchCycle = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, MSGBox: false);
            }
        }
        else
        {
            var activeItem = cycleItems.FirstOrDefault(item => item.IsActive);
            if (activeItem != null)
            {
                int idx = cycleItems.IndexOf(activeItem);
                int next = (idx + 1) % cycleItems.Count;
                activeItem.IsActive = false;
                cycleItems[next].IsActive = true;
                _oscDisplay.LastSwitchCycle = DateTime.Now;
            }
            else
            {
                // Active item is outside cycle candidates — advance to first eligible
                foreach (var item in _chatStatus.StatusList) item.IsActive = false;
                cycleItems[0].IsActive = true;
                _oscDisplay.LastSwitchCycle = DateTime.Now;
            }
        }
    }

    private static int WeightedRandomIndex(System.Collections.Generic.List<double> weights)
    {
        var rnd = new Random();
        double total = weights.Sum();
        double point = rnd.NextDouble() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            if (point < weights[i]) return i;
            point -= weights[i];
        }
        return weights.Count - 1;
    }

    #endregion
}
