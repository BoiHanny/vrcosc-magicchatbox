using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class ProxmoxSectionViewModel : ObservableObject
{
    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public ProxmoxModule Proxmox { get; }

    [ObservableProperty] private string _previewLine = string.Empty;

    public ProxmoxSectionViewModel(
        ISettingsProvider<AppSettings> appSettingsProvider,
         ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ProxmoxModule proxmox)
    {
        AppSettings = appSettingsProvider.Value;
         IntegrationSettings = integrationSettingsProvider.Value;
        Proxmox = proxmox;
        Proxmox.PropertyChanged += OnProxmoxChanged;
        RefreshPreview();
    }

    private void OnProxmoxChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreview();
    private void RefreshPreview() => PreviewLine = Proxmox.StatusLine;
}
