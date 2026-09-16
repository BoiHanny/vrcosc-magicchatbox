using CommunityToolkit.Mvvm.ComponentModel;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules;

public partial class ProxmoxSettings : VersionedSettings
{
    [ObservableProperty] private string _endpoint = "https://192.168.1.23:8006";
    [ObservableProperty] private string _apiToken = string.Empty;
    [ObservableProperty] private string _nodeName = "pve";
    [ObservableProperty] private string _nodeNames = "pve01,pve02";
    [ObservableProperty] private int _pollIntervalSeconds = 30;
    [ObservableProperty] private bool _allowInvalidCertificate = true;
}
