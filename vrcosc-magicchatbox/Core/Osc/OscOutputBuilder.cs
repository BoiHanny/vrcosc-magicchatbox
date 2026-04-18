using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc;

/// <summary>
/// Assembles the final OSC message from <see cref="IOscProvider"/> instances.
/// Replaces the hardcoded Add* methods in OSCController.BuildOSC().
/// Uses <see cref="ModuleFaultTracker"/> to skip providers that have failed
/// repeatedly, preventing cascading failures.
/// </summary>
public sealed class OscOutputBuilder
{
    private readonly IEnumerable<IOscProvider> _providers;
    private readonly IAppState _appState;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly AppSettings _appSettings;
    private readonly ModuleFaultTracker _faultTracker;

    public OscOutputBuilder(
        IEnumerable<IOscProvider> providers,
        IAppState appState,
        IntegrationDisplayState integrationDisplay,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ModuleFaultTracker faultTracker)
    {
        _providers = providers;
        _appState = appState;
        _integrationDisplay = integrationDisplay;
        _appSettings = appSettingsProvider.Value;
        _faultTracker = faultTracker;
    }

    /// <summary>
    /// Builds the OSC message by iterating providers in the user's sort order.
    /// If the combined message exceeds the 144-char limit, lowest-priority
    /// segments are dropped until the message fits (graceful degradation).
    /// </summary>
    public OscBuildResult Build()
    {
        string separator = GetSeparator();
        string prefix = ExpandNewlines(_appSettings.OscMessagePrefix);
        string suffix = ExpandNewlines(_appSettings.OscMessageSuffix);
        bool isVR = _appState.IsVRRunning;

        var providerMap = new Dictionary<string, IOscProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _providers)
        {
            providerMap.TryAdd(p.SortKey, p);
        }

        IEnumerable<string> orderedKeys = _integrationDisplay.IntegrationSortOrder?.Count > 0
            ? _integrationDisplay.IntegrationSortOrder
            : IntegrationDisplayState.DefaultSortOrder;

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collected = new List<(string Text, string UiKey, int Priority)>();

        void TryAddProvider(IOscProvider provider)
        {
            if (_faultTracker.IsFaulted(provider.SortKey))
                return;

            if (!provider.IsEnabledForCurrentMode(isVR))
                return;

            var context = new OscBuildContext
            {
                CurrentSegments = collected.Select(c => c.Text).ToList(),
                Separator = separator,
                Prefix = prefix,
                Suffix = suffix,
                IsVRRunning = isVR
            };

            OscSegment? segment;
            try
            {
                segment = provider.TryBuild(context);
                _faultTracker.RecordSuccess(provider.SortKey);
            }
            catch (Exception ex)
            {
                _faultTracker.RecordFailure(provider.SortKey, ex);
                Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
                return;
            }

            if (segment == null || string.IsNullOrEmpty(segment.Text))
                return;

            collected.Add((segment.Text, provider.UiKey, provider.Priority));
        }

        foreach (var key in orderedKeys)
        {
            if (!providerMap.TryGetValue(key, out var provider))
                continue;
            usedKeys.Add(key);
            TryAddProvider(provider);
        }

        // Then any providers not in the sort order (safety net)
        foreach (var kvp in providerMap)
        {
            if (usedKeys.Contains(kvp.Key))
                continue;
            TryAddProvider(kvp.Value);
        }

        var trimmed = new List<string>();
        while (collected.Count > 0)
        {
            string message = AssembleMessage(collected.Select(c => c.Text), separator, prefix, suffix);
            if (message.Length <= OscBuildContext.MaxOscLength)
                break;

            // Find the lowest-priority segment (highest Priority number)
            int worstIdx = 0;
            for (int i = 1; i < collected.Count; i++)
            {
                if (collected[i].Priority > collected[worstIdx].Priority)
                    worstIdx = i;
            }

            trimmed.Add(collected[worstIdx].UiKey);
            collected.RemoveAt(worstIdx);
        }

        string finalMessage = collected.Count > 0
            ? AssembleMessage(collected.Select(c => c.Text), separator, prefix, suffix)
            : string.Empty;

        return new OscBuildResult
        {
            Message = finalMessage,
            ExceededLimit = trimmed.Count > 0,
            IncludedProviders = collected.Select(c => c.UiKey).ToList(),
            TrimmedProviders = trimmed
        };
    }

    /// <summary>
    /// Applies an <see cref="OscBuildResult"/> to the UI display state.
    /// This is the ONLY place that mutates display state from build results.
    /// </summary>
    public static void ApplyToDisplay(OscBuildResult result, OscDisplayState oscDisplay, IntegrationDisplayState integrationDisplay)
    {
        integrationDisplay.ResetAllOpacity();

        if (result.ExceededLimit)
        {
            oscDisplay.CharLimit = "Visible";
            foreach (var key in result.TrimmedProviders)
                integrationDisplay.SetOpacity(key, "0.5");
        }
        else
        {
            oscDisplay.CharLimit = "Hidden";
        }

        if (result.Length > OscBuildContext.MaxOscLength)
        {
            oscDisplay.OscToSent = string.Empty;
            oscDisplay.OscMsgCount = result.Length;
            oscDisplay.OscMsgCountUI = $"MAX/{OscBuildContext.MaxOscLength}";
        }
        else
        {
            oscDisplay.OscToSent = result.Message;
            oscDisplay.OscMsgCount = result.Length;
            oscDisplay.OscMsgCountUI = $"{result.Length}/{OscBuildContext.MaxOscLength}";
        }
    }

    #region Helpers

    private static string AssembleMessage(IEnumerable<string> segments, string separator, string prefix, string suffix)
    {
        string message = string.Join(separator, segments);
        if (!string.IsNullOrEmpty(message))
            message = $"{prefix}{message}{suffix}";
        return message;
    }

    private string GetSeparator()
    {
        if (_appSettings.SeperateWithENTERS)
            return "\n";
        return _appSettings.OscMessageSeparator ?? " ┆ ";
    }

    private static string ExpandNewlines(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\n", "\n").Replace("/n", "\n");
    }

    #endregion
}
