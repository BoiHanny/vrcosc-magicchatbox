using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;
using vrcosc_magicchatbox.Core.Vrc.Sharing;

namespace vrcosc_magicchatbox.ViewModels;

public partial class AvatarPageViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(750);

    private readonly Lazy<IModuleHost> _modules;
    private readonly ISettingsProvider<VrcBridgeSettings> _settingsProvider;
    private readonly ISettingsProvider<IntegrationSettings> _integrationsProvider;
    private readonly ISettingsProvider<AvatarPresetSettings> _presetsProvider;
    private readonly LocalAvatarDataReader _localAvatarData;
    private readonly IPrivacyConsentService _consent;
    private readonly TaskScheduler _uiScheduler;
    private DispatcherTimer? _timer;

    public VrcBridgeSettings Settings => _settingsProvider.Value;

    [ObservableProperty] private AvatarPageRung _rung = AvatarPageRung.BridgeOff;
    [ObservableProperty] private string _rungMessage = string.Empty;
    [ObservableProperty] private string _avatarName = "No avatar yet";
    [ObservableProperty] private string _avatarId = string.Empty;
    [ObservableProperty] private string _parameterSummary = string.Empty;
    [ObservableProperty] private string _bridgeStatus = "Not started";
    [ObservableProperty] private string _portText = "—";
    [ObservableProperty] private string _trafficText = "Nothing received yet";
    [ObservableProperty] private string _neighbourText = string.Empty;
    [ObservableProperty] private string _hiddenText = string.Empty;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private bool _hideAdult = true;
    [ObservableProperty] private bool _writableOnly = true;

    public ObservableCollection<Avatar.AvatarControlGroupViewModel> Groups { get; } = new();

    public ObservableCollection<AvatarSense> RecentlyChanged { get; } = new();

    public ObservableCollection<ReadinessRow> Readiness { get; } = new();

    public ObservableCollection<AvatarConfigChange> ConfigChanges { get; } = new();

    [ObservableProperty] private bool _hasConfigChanges;

    public ObservableCollection<AvatarPreset> Presets { get; } = new();

    public ObservableCollection<PresetApplyRow> PresetRefusals { get; } = new();

    [ObservableProperty] private string _newPresetName = string.Empty;

    [ObservableProperty] private string _presetStatus = string.Empty;

    [ObservableProperty] private bool _canCapturePreset;

    [ObservableProperty] private string _libraryText = string.Empty;

    [ObservableProperty] private bool _canImportSavedState;

    [ObservableProperty] private string _layoutCode = string.Empty;

    [ObservableProperty] private string _layoutShareStatus = string.Empty;

    public ObservableCollection<LayoutMatchRow> LayoutMatches { get; } = new();

    [ObservableProperty] private string _speechText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEcosystems))]
    private IReadOnlyList<EcosystemMarker> _ecosystems = Array.Empty<EcosystemMarker>();

    public bool HasEcosystems => Ecosystems.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissingControls))]
    [NotifyPropertyChangedFor(nameof(HasLayoutReport))]
    private LayoutReport? _layout;

    public bool HasMissingControls => Layout?.MissingControls.Count > 0;

    public bool HasLayoutReport => Layout != null && Layout.State != LayoutState.Unknown;

    private static readonly string[] ExpectedControls = AvatarParameterContract.Parameters
        .Where(p => p.Tier == AvatarParameterTier.Control)
        .Select(p => p.Name)
        .ToArray();

    private readonly IAvatarParameterSink _sink;
    private readonly Dictionary<string, Avatar.AvatarControlRowViewModel> _rows = new(StringComparer.Ordinal);
    private string _rowsSignature = string.Empty;

    public AvatarPageViewModel(
        ISettingsProvider<VrcBridgeSettings> settingsProvider,
        ISettingsProvider<IntegrationSettings> integrationsProvider,
        ISettingsProvider<AvatarPresetSettings> presetsProvider,
        Lazy<IModuleHost> modules,
        IAvatarParameterSink sink,
        IPrivacyConsentService consent,
        LocalAvatarDataReader? localAvatarData = null)
    {
        _settingsProvider = settingsProvider;
        _integrationsProvider = integrationsProvider;
        _presetsProvider = presetsProvider;
        _modules = modules;
        _sink = sink;
        _consent = consent;
        _localAvatarData = localAvatarData ?? new LocalAvatarDataReader();

        _uiScheduler = SynchronizationContext.Current != null
            ? TaskScheduler.FromCurrentSynchronizationContext()
            : TaskScheduler.Default;
    }

    public void Activate()
    {
        if (_timer != null)
            return;

        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
        DescribeLibraryAsync();
    }

    public void Deactivate()
    {
        if (_timer == null)
            return;

        _timer.Stop();
        _timer = null;
    }

    partial void OnSearchChanged(string value) => RebuildGroups();

    partial void OnHideAdultChanged(bool value) => RebuildGroups();

    partial void OnWritableOnlyChanged(bool value) => RebuildGroups();

    [RelayCommand]
    private void Refresh()
    {
        var bridge = _modules.Value.VrcBridge;

        if (bridge == null)
        {
            Rung = AvatarPageRung.BridgeOff;
            RungMessage = AvatarPageRungs.Describe(Rung);
            return;
        }

        BridgeStatus = bridge.StatusMessage;

        int port = bridge.OscReceivePort;
        PortText = port == 0 ? "—" : port.ToString();

        long received = bridge.ParametersReceived;
        TrafficText = received == 0
            ? "Nothing received yet"
            : $"{received:N0} values received";

        var neighbours = bridge.DescribeNeighbours();
        NeighbourText = neighbours.Count == 0
            ? "No other OSC apps announced"
            : $"{neighbours.Count} other OSC service(s) on this PC";

        AvatarIdentity identity = bridge.Identity;
        AvatarId = identity.Id;
        AvatarName = identity.DisplayName;

        AvatarSchemaSnapshot schema = bridge.Schema.Current;

        Rung = AvatarPageRungs.Resolve(
            Settings.EnableBridge,
            bridge.IsRunning,
            received > 0 || !schema.IsEmpty,
            identity.IsKnown);

        RungMessage = AvatarPageRungs.Describe(Rung);

        RebuildGroups();
        RebuildRecent(bridge);
        RebuildReadiness(bridge, schema, identity.IsKnown);
        RebuildPresets();

        CanCapturePreset = !schema.IsEmpty && PresetKey.Length > 0;
        CanImportSavedState = CanCapturePreset && AvatarId.Length > 0 && _localAvatarData.Exists;
    }

    private void RebuildReadiness(Services.Vrc.VrcBridgeModule bridge, AvatarSchemaSnapshot schema, bool avatarKnown)
    {
        SpeechGate speech = bridge.Senses.Speech;

        SpeechText = speech.IsHotMic
            ? "Your microphone is live"
            : speech.IsMuted ? "Microphone muted" : "Not speaking";

        Ecosystems = schema.IsEmpty
            ? Array.Empty<EcosystemMarker>()
            : EcosystemSignature.Detect(schema.Parameters.Select(p => p.Name));

        Layout = LayoutDoctor.Inspect(schema, ExpectedControls);

        RebuildConfigChanges(bridge);

        var host = _modules.Value;
        IntegrationSettings integrations = _integrationsProvider.Value;

        var rows = new List<ReadinessRow>
        {
            AvatarReadiness.Evaluate(
                new ReadinessInput(
                    "Heart rate",
                    host.Pulsoid?.IsRunning == true,
                    host.Pulsoid?.PulsoidDeviceOnline == true,
                    integrations.IntgrHeartRate_OSC,
                    host.Pulsoid?.PulsoidAccessError == true ? host.Pulsoid.PulsoidAccessErrorTxt : null,
                    AvatarFeatureCatalog.NamesFor(AvatarFeatureCatalog.HeartRateKey)),
                schema,
                avatarKnown),

            AvatarReadiness.Evaluate(
                new ReadinessInput(
                    "Discord",
                    host.Discord?.IsRunning == true,
                    host.Discord?.IsRunning == true,
                    host.Discord?.Settings.SendMuteDeafenOsc == true || host.Discord?.Settings.SendVoiceStateOsc == true,
                    null,
                    AvatarFeatureCatalog.NamesFor(AvatarFeatureCatalog.DiscordKey)),
                schema,
                avatarKnown),

            AvatarReadiness.Evaluate(
                new ReadinessInput(
                    "Camera flash",
                    host.VrcRadar?.IsRadarRunning == true,
                    host.VrcRadar?.IsRadarRunning == true,
                    host.VrcRadar?.Settings.SendCameraFlashOsc == true,
                    null,
                    CameraFlashNames(host.VrcRadar?.Settings.OscCameraFlashParam)),
                schema,
                avatarKnown),
        };

        Readiness.Clear();
        foreach (ReadinessRow row in rows)
            Readiness.Add(row);
    }

    private string PresetKey => AvatarId.Length > 0 ? AvatarId : AvatarName;

    private void RebuildPresets()
    {
        string key = PresetKey;

        var mine = _presetsProvider.Value.Presets
            .Where(p => string.Equals(p.AvatarId, key, StringComparison.Ordinal))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (mine.Count == Presets.Count && mine.Zip(Presets).All(p => p.First == p.Second))
            return;

        Presets.Clear();
        foreach (AvatarPreset preset in mine)
            Presets.Add(preset);
    }

    [RelayCommand]
    private void CheckLayoutCode()
    {
        LayoutMatches.Clear();

        if (!_consent.IsApproved(PrivacyHook.SharedLayoutImport))
        {
            LayoutShareStatus =
                "Reading a layout somebody sent you is switched off. Turn on Shared Layouts under Options, Privacy.";
            return;
        }

        var bridge = _modules.Value.VrcBridge;
        AvatarSchemaSnapshot schema = bridge?.Schema.Current ?? AvatarSchemaSnapshot.Empty;

        LayoutParseResult parsed = LayoutCodec.FromCode(LayoutCode);

        if (!parsed.Ok)
        {
            LayoutShareStatus = parsed.Detail;
            return;
        }

        LayoutDocument document = parsed.Document!;

        if (schema.IsEmpty)
        {
            LayoutShareStatus = $"\"{document.Title}\" read. Waiting to see your avatar before checking it.";
            return;
        }

        LayoutMatchReport report = LayoutCodec.Match(document, schema);

        foreach (LayoutMatchRow row in report.Rows.Where(r => r.Match != LayoutMatch.Present))
            LayoutMatches.Add(row);

        LayoutShareStatus = report.Satisfied
            ? $"\"{document.Title}\": this avatar has everything it needs ({report.Present} of {report.Rows.Count})."
            : $"\"{document.Title}\": this avatar is missing {report.MissingRequired} of the {report.Rows.Count} it needs.";
    }

    [RelayCommand]
    private void CopyLayoutCode()
    {
        var document = new LayoutDocument
        {
            Title = "MagicChatbox controls",
            Description = "What an avatar needs for MagicChatbox to reach it.",
            Author = string.Empty,
            Tags = { "magicchatbox" },
        };

        foreach (AvatarParameter parameter in AvatarParameterContract.Parameters)
        {
            if (parameter.Tier is not (AvatarParameterTier.Control or AvatarParameterTier.Config))
                continue;

            document.Requires.Add(new LayoutRequirement
            {
                Name = parameter.Name,
                Type = parameter.Kind == AvatarParameterKind.Pulse ? "Bool" : parameter.Kind.ToString(),
                Optional = parameter.Tier == AvatarParameterTier.Config,
                Purpose = parameter.Source,
            });
        }

        LayoutCode = LayoutCodec.ToCode(document);
        LayoutShareStatus = $"A code for {document.Requires.Count} controls. Copy it to somebody who wants the same setup.";
    }

    private void DescribeLibraryAsync()
    {
        LocalAvatarDataReader reader = _localAvatarData;

        Task.Run(() =>
        {
            try
            {
                if (!reader.Exists)
                    return "VRChat has not saved anything on this PC yet.";

                IReadOnlyList<LocalAvatarState> all = reader.ReadAll();

                if (all.Count == 0)
                    return "VRChat has not saved anything on this PC yet.";

                int values = all.Sum(a => a.Count);

                return $"VRChat has saved {values:N0} settings across {all.Count:N0} avatars on this PC. "
                    + "It keeps them per machine, so they do not follow you to another one.";
            }
            catch (Exception ex)
            {
                Classes.DataAndSecurity.Logging.WriteException(ex, MSGBox: false);
                return string.Empty;
            }
        }).ContinueWith(
            task => LibraryText = task.Result,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            _uiScheduler);
    }

    [RelayCommand]
    private void ImportSavedState()
    {
        var bridge = _modules.Value.VrcBridge;
        if (bridge == null)
            return;

        AvatarSchemaSnapshot schema = bridge.Schema.Current;

        if (schema.IsEmpty || AvatarId.Length == 0)
        {
            PresetStatus = "Waiting to see your avatar.";
            return;
        }

        LocalAvatarState? saved = _localAvatarData.TryRead(AvatarId);

        if (saved == null)
        {
            PresetStatus = "VRChat has not saved anything for this avatar on this PC yet.";
            return;
        }

        string name = NewPresetName.Trim();
        if (name.Length == 0)
            name = NextPresetName();

        AvatarPreset preset = AvatarPresetPlanner.FromSavedState(
            name,
            new AvatarIdentity(PresetKey, AvatarName, bridge.Identity.Source),
            saved,
            schema);

        if (preset.Count == 0)
        {
            PresetStatus = $"VRChat saved {saved.Count} settings for this avatar, but none of them can be written back.";
            return;
        }

        _presetsProvider.Value.Presets.Add(preset);
        _presetsProvider.Save();

        NewPresetName = string.Empty;
        RebuildPresets();

        PresetStatus = preset.Count == saved.Count
            ? $"Took all {preset.Count} settings VRChat saved for this avatar as \"{preset.Name}\"."
            : $"Took {preset.Count} of the {saved.Count} settings VRChat saved for this avatar as \"{preset.Name}\". The rest cannot be written back.";
    }

    [RelayCommand]
    private void CapturePreset()
    {
        var bridge = _modules.Value.VrcBridge;
        if (bridge == null)
            return;

        AvatarSchemaSnapshot schema = bridge.Schema.Current;

        if (schema.IsEmpty || PresetKey.Length == 0)
        {
            PresetStatus = "Waiting to see your avatar.";
            return;
        }

        string name = NewPresetName.Trim();
        if (name.Length == 0)
            name = NextPresetName();

        AvatarPreset preset = AvatarPresetPlanner.Capture(
            name,
            new AvatarIdentity(PresetKey, AvatarName, bridge.Identity.Source),
            schema,
            bridge.Senses);

        _presetsProvider.Value.Presets.Add(preset);
        _presetsProvider.Save();

        NewPresetName = string.Empty;
        RebuildPresets();

        int writable = schema.WritableCount;

        PresetStatus = preset.Count == writable
            ? $"Saved {preset.Count} from this avatar as \"{preset.Name}\"."
            : $"Saved {preset.Count} of {writable} as \"{preset.Name}\". The rest had no value to read yet.";
    }

    [RelayCommand]
    private void ApplyPreset(AvatarPreset? preset)
    {
        var bridge = _modules.Value.VrcBridge;
        if (preset == null || bridge == null)
            return;

        PresetApplyPlan plan = AvatarPresetPlanner.Plan(preset, bridge.Schema.Current);

        PresetRefusals.Clear();
        foreach (PresetApplyRow row in plan.Rows.Where(r => r.Outcome != PresetOutcome.Carried))
            PresetRefusals.Add(row);

        if (plan.IsEmpty)
        {
            PresetStatus = $"\"{preset.Name}\" has nothing this avatar will take. {plan.Summary}.";
            return;
        }

        AvatarPresetPlanner.Publish(plan, bridge.Pump);

        string estimate = plan.Estimate > TimeSpan.FromSeconds(1)
            ? $" It takes about {plan.Estimate.TotalSeconds:0.#} seconds to send."
            : string.Empty;

        PresetStatus = $"\"{preset.Name}\": {plan.Summary}.{estimate}";
    }

    [RelayCommand]
    private void DeletePreset(AvatarPreset? preset)
    {
        if (preset == null)
            return;

        _presetsProvider.Value.Presets.Remove(preset);
        _presetsProvider.Save();

        RebuildPresets();
        PresetStatus = $"Deleted \"{preset.Name}\".";
    }

    private string NextPresetName()
    {
        for (int i = 1; i < 1000; i++)
        {
            string candidate = $"Preset {i}";

            if (!Presets.Any(p => string.Equals(p.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
                return candidate;
        }

        return "Preset";
    }

    private void RebuildConfigChanges(Services.Vrc.VrcBridgeModule bridge)
    {
        var rows = new List<AvatarConfigChange>();

        foreach (ConfigSeedRow row in bridge.LastConfigSeed)
        {
            if (row.Outcome is not (ConfigSeedOutcome.Applied or ConfigSeedOutcome.RefusedTurningOn))
                continue;

            rows.Add(new AvatarConfigChange(
                row.Parameter,
                row.Outcome == ConfigSeedOutcome.Applied
                    ? DescribeApplied(row)
                    : "This avatar asked to switch a feature back on. Only you can do that, so nothing changed."));
        }

        if (rows.Count == ConfigChanges.Count
            && rows.Zip(ConfigChanges).All(p => p.First == p.Second))
        {
            return;
        }

        ConfigChanges.Clear();
        foreach (AvatarConfigChange row in rows)
            ConfigChanges.Add(row);

        HasConfigChanges = ConfigChanges.Count > 0;
    }

    private static string DescribeApplied(ConfigSeedRow row)
    {
        string name = row.Parameter.StartsWith(AvatarConfigBinding.Prefix, StringComparison.Ordinal)
            ? row.Parameter[AvatarConfigBinding.Prefix.Length..]
            : row.Parameter;

        return row.Value
            ? $"{name} is on because this avatar holds it on."
            : $"{name} is switched off while you wear this avatar.";
    }

    private static IReadOnlyList<string> CameraFlashNames(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return AvatarFeatureCatalog.NamesFor(AvatarFeatureCatalog.CameraFlashKey);

        string trimmed = configured.Trim();

        return [trimmed.StartsWith(AvatarParameter.AddressPrefix, StringComparison.Ordinal)
            ? trimmed[AvatarParameter.AddressPrefix.Length..]
            : trimmed];
    }

    private void RebuildGroups()
    {
        var bridge = _modules.Value.VrcBridge;
        if (bridge == null)
            return;

        AvatarSchemaSnapshot schema = bridge.Schema.Current;

        AvatarControlView view = AvatarControlCatalog.Build(
            schema,
            bridge.Senses,
            Search,
            HideAdult,
            WritableOnly);

        ParameterSummary = schema.IsEmpty
            ? string.Empty
            : $"{view.CustomCount} custom · {view.BuiltInCount} built-in · {schema.WritableCount} you can drive";

        HiddenText = view.HiddenGroupCount == 0
            ? string.Empty
            : $"{view.HiddenGroupCount} group(s) hidden";

        string signature = string.Join(
            "|",
            view.Groups.Select(g => g.Name + ":" + string.Join(",", g.Rows.Select(r => r.Name))));

        if (!string.Equals(signature, _rowsSignature, StringComparison.Ordinal))
        {
            _rowsSignature = signature;
            _rows.Clear();
            Groups.Clear();

            foreach (AvatarControlGroup group in view.Groups)
            {
                var rows = new List<Avatar.AvatarControlRowViewModel>(group.Rows.Count);

                foreach (AvatarControlRow row in group.Rows)
                {
                    var rowViewModel = new Avatar.AvatarControlRowViewModel(row, _sink);
                    _rows[row.Name] = rowViewModel;
                    rows.Add(rowViewModel);
                }

                Groups.Add(new Avatar.AvatarControlGroupViewModel(group.Name, group.DisplayName, rows));
            }

            return;
        }

        foreach (AvatarControlGroup group in view.Groups)
        {
            foreach (AvatarControlRow row in group.Rows)
            {
                if (_rows.TryGetValue(row.Name, out Avatar.AvatarControlRowViewModel? existing))
                    existing.ObserveExternal(row.Value, row.HasValue);
            }
        }
    }

    private void RebuildRecent(Services.Vrc.VrcBridgeModule bridge)
    {
        IReadOnlyList<AvatarSense> recent = bridge.Senses.MostActiveParameters(8);

        var spelling = bridge.Schema.Current.Parameters
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        RecentlyChanged.Clear();

        foreach (AvatarSense sense in recent)
        {
            RecentlyChanged.Add(spelling.TryGetValue(sense.Key, out string? declared)
                ? sense with { Key = declared }
                : sense);
        }
    }
}
