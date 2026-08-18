using System;
using System.Linq;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Status;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Osc.Text;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc.Providers;

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
        int budget = ResolveBudget(context);

        if (afk != null && afk.IsAfk && afk.Settings.EnableAfkDetection)
        {
            string afkText = SegmentWriter.Truncate(afk.GenerateAFKString(), budget);
            if (!string.IsNullOrEmpty(afkText))
                return new OscSegment { Text = afkText };
        }

        if (!_intgr.IntgrStatus || _chatStatus.StatusList == null || _chatStatus.StatusList.Count == 0)
            return null;

        if (_app.CycleStatus)
            CycleStatus();

        StatusItem? active = _chatStatus.StatusList.FirstOrDefault(item => item.IsActive);
        if (active == null) return null;

        bool prefixIcon = _app.PrefixIconStatus;
        string? icon = prefixIcon ? _emojis.GetNextEmoji() : null;
        string text = StatusLine.Compose(active.msg, icon, prefixIcon, budget);

        return string.IsNullOrEmpty(text) ? null : new OscSegment { Text = text };
    }

    private static int ResolveBudget(OscBuildContext context)
    {
        int room = context.RemainingCharsIf(string.Empty);

        return room > 0
            ? room
            : OscBuildContext.MaxOscLength - context.Prefix.Length - context.Suffix.Length;
    }

    #region Status Cycling (moved from OSCController)

    private void CycleStatus()
    {
        if (_chatStatus.StatusList == null || _chatStatus.StatusList.Count == 0)
            return;

        if (DateTime.Now - _oscDisplay.LastSwitchCycle < TimeSpan.FromSeconds(_app.SwitchStatusInterval))
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

    private void CycleItems(System.Collections.Generic.List<StatusItem> cycleItems)
    {
        if (_app.IsRandomCycling)
        {
            ClearActiveItem();

            try
            {
                var weights = cycleItems.Select(item =>
                {
                    var timeWeight = (DateTime.Now - item.LastUsed).TotalSeconds;
                    return timeWeight * Random.Shared.NextDouble();
                }).ToList();

                int selected = WeightedRandomIndex(weights);
                cycleItems[selected].IsActive = true;
                cycleItems[selected].LastUsed = DateTime.Now;
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
                cycleItems[next].LastUsed = DateTime.Now;
                _oscDisplay.LastSwitchCycle = DateTime.Now;
            }
            else
            {
                ClearActiveItem();
                cycleItems[0].IsActive = true;
                cycleItems[0].LastUsed = DateTime.Now;
                _oscDisplay.LastSwitchCycle = DateTime.Now;
            }
        }
    }

    private void ClearActiveItem()
    {
        var statusList = _chatStatus.StatusList;
        if (statusList == null)
            return;

        for (int i = 0; i < statusList.Count; i++)
        {
            if (statusList[i].IsActive)
            {
                statusList[i].IsActive = false;
                return;
            }
        }
    }

    private static int WeightedRandomIndex(System.Collections.Generic.List<double> weights)
    {
        double total = weights.Sum();
        double point = Random.Shared.NextDouble() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            if (point < weights[i]) return i;
            point -= weights[i];
        }
        return weights.Count - 1;
    }

    #endregion
}
