using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Vrc;

namespace vrcosc_magicchatbox.ViewModels;

public partial class AvatarPageViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(750);

    private readonly Lazy<IModuleHost> _modules;
    private readonly ISettingsProvider<VrcBridgeSettings> _settingsProvider;
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

    public ObservableCollection<AvatarControlGroup> Groups { get; } = new();

    public ObservableCollection<AvatarSense> RecentlyChanged { get; } = new();

    public AvatarPageViewModel(
        ISettingsProvider<VrcBridgeSettings> settingsProvider,
        Lazy<IModuleHost> modules)
    {
        _settingsProvider = settingsProvider;
        _modules = modules;
    }

    public void Activate()
    {
        if (_timer != null)
            return;

        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
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

        Groups.Clear();
        foreach (AvatarControlGroup group in view.Groups)
            Groups.Add(group);
    }

    private void RebuildRecent(Services.Vrc.VrcBridgeModule bridge)
    {
        IReadOnlyList<AvatarSense> recent = bridge.Senses.MostActive(8);

        RecentlyChanged.Clear();
        foreach (AvatarSense sense in recent)
            RecentlyChanged.Add(sense);
    }
}
