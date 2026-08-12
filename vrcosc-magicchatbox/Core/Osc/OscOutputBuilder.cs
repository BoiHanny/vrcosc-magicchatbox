using System;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Core.Osc;

public sealed class OscOutputBuilder
{
    private const string DefaultSeparator = " ┆ ";

    private readonly IEnumerable<IOscProvider> _providers;
    private readonly IAppState _appState;
    private readonly IntegrationDisplayState _integrationDisplay;
    private readonly AppSettings _appSettings;
    private readonly ModuleFaultTracker _faultTracker;
    private readonly HashSet<string> _unorderedProvidersLogged = new(StringComparer.OrdinalIgnoreCase);

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

    public OscBuildResult Build(bool allowExternalRefresh = true)
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
            if (!provider.IsEnabledForCurrentMode(isVR))
                return;

            if (_faultTracker.IsFaulted(provider.SortKey))
                return;

            var context = new OscBuildContext
            {
                CurrentSegments = collected.Select(c => c.Text).ToList(),
                Separator = separator,
                Prefix = prefix,
                Suffix = suffix,
                IsVRRunning = isVR,
                AllowExternalRefresh = allowExternalRefresh
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

            string text = ExpandNewlines(segment.Text);
            collected.Add((text, provider.UiKey, provider.Priority));
        }

        foreach (var key in orderedKeys)
        {
            if (!providerMap.TryGetValue(key, out var provider))
                continue;
            usedKeys.Add(key);
            TryAddProvider(provider);
        }

        foreach (var provider in _providers)
        {
            if (usedKeys.Contains(provider.SortKey))
                continue;

            if (_unorderedProvidersLogged.Add(provider.SortKey))
            {
                Classes.DataAndSecurity.Logging.WriteInfo(
                    $"OscOutputBuilder: provider '{provider.SortKey}' (UiKey '{provider.UiKey}') is not present in IntegrationSortOrder; appending via safety-net path.");
            }

            TryAddProvider(provider);
        }

        var trimmed = new List<string>();
        while (collected.Count > 0)
        {
            string message = AssembleMessage(collected.Select(c => c.Text), separator, prefix, suffix);
            if (message.Length <= OscBuildContext.MaxOscLength)
                break;

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

        finalMessage = ClampToOscLimit(finalMessage);

        return new OscBuildResult
        {
            Message = finalMessage,
            ExceededLimit = trimmed.Count > 0,
            IncludedProviders = collected.Select(c => c.UiKey).ToList(),
            TrimmedProviders = trimmed
        };
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
        return NormalizeSeparator(_appSettings.OscMessageSeparator);
    }

    internal static string NormalizeSeparator(string? configured)
    {
        return string.IsNullOrWhiteSpace(configured) ? DefaultSeparator : configured;
    }

    internal static string ExpandNewlines(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\n", "\n");
    }

    internal static string ClampToOscLimit(string message)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= Constants.OscMaxMessageLength)
            return message ?? string.Empty;

        int cut = Constants.OscMaxMessageLength;
        if (cut > 0 && char.IsHighSurrogate(message[cut - 1]))
            cut--;

        string truncated = message.Substring(0, cut);

        Classes.DataAndSecurity.Logging.WriteInfo(
            $"OscOutputBuilder: final message length {message.Length} exceeded OSC limit {Constants.OscMaxMessageLength}; truncated to {truncated.Length}. " +
            "Check OSC prefix/suffix/separator settings.");

        return truncated;
    }

    #endregion
}
