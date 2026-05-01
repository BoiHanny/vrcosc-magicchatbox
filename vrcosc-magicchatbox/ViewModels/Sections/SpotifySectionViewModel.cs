using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;
using static vrcosc_magicchatbox.Classes.Modules.MediaLinkModule;

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for the first-class Spotify integration.
/// </summary>
public partial class SpotifySectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly ISettingsProvider<SpotifySettings> _settingsProvider;
    private readonly IToastService _toast;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public INavigationService Navigation { get; }
    public SpotifyDisplayState Display { get; }
    public MediaLinkDisplayState MediaLinkDisplay { get; }
    public IModuleHost Modules => _moduleHost.Value;
    public SpotifySettings Settings => _settingsProvider.Value;

    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _outputPreview = string.Empty;
    [ObservableProperty] private string _previewLengthText = $"0/{Constants.OscMaxMessageLength}";
    [ObservableProperty] private string? _selectedPresetName;

    public string[] PresetNames { get; } = SpotifySettings.TemplatePresets
        .Select(p => p.Name).ToArray();

    public SpotifyProgressDisplayMode[] ProgressDisplayModes { get; } = Enum.GetValues<SpotifyProgressDisplayMode>();

    /// <summary>True when the user has selected Seekbar mode — controls seekbar style dropdown visibility.</summary>
    public bool IsSeekbarMode => Settings.ProgressDisplayMode == SpotifyProgressDisplayMode.Seekbar;

    public string RedirectUri => Constants.SpotifyOAuthRedirectUri;

    /// <summary>Available seekbar styles from MediaLink (built-in + custom). Bound directly.</summary>
    public ObservableCollection<MediaLinkStyle>? SeekbarStyles => MediaLinkDisplay.MediaLinkSeekbarStyles;

    /// <summary>Currently selected seekbar style for Spotify output.</summary>
    public MediaLinkStyle? SelectedSeekbarStyle
    {
        get
        {
            var styles = SeekbarStyles;
            if (styles == null || styles.Count == 0) return null;
            int targetId = Settings.SelectedSeekbarStyleId;
            return styles.FirstOrDefault(s => s.ID == targetId) ?? styles.FirstOrDefault();
        }
        set
        {
            if (value != null)
            {
                Settings.SelectedSeekbarStyleId = value.ID;
                OnPropertyChanged();
                RefreshPreview();
            }
        }
    }

    public SpotifySectionViewModel(
        Lazy<IModuleHost> moduleHost,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<SpotifySettings> settingsProvider,
        SpotifyDisplayState display,
        MediaLinkDisplayState mediaLinkDisplay,
        INavigationService nav,
        IToastService toast)
    {
        _moduleHost = moduleHost;
        _settingsProvider = settingsProvider;
        Display = display;
        MediaLinkDisplay = mediaLinkDisplay;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        Navigation = nav;
        _toast = toast;

        Display.PropertyChanged += OnDisplayChanged;
        Settings.PropertyChanged += OnSettingsChanged;
        MediaLinkDisplay.PropertyChanged += OnMediaLinkDisplayChanged;

        RefreshStatus();
        RefreshPreview();
    }

    partial void OnSelectedPresetNameChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var preset = SpotifySettings.TemplatePresets.FirstOrDefault(p => p.Name == value);
        if (preset != default)
        {
            Settings.OutputTemplate = preset.Template;
            if (preset.Template.Contains("{seekbar}", StringComparison.OrdinalIgnoreCase))
            {
                Settings.ShowProgress = true;
                Settings.ProgressDisplayMode = SpotifyProgressDisplayMode.Seekbar;
            }
        }
    }

    public async Task ConnectWithClientIdAsync(string clientId)
    {
        var spotify = Modules.Spotify;
        if (spotify == null)
            return;

        IsConnecting = true;
        Display.IsConnecting = true;
        try
        {
            Settings.ClientId = clientId.Trim();
            _settingsProvider.Save();

            var token = await spotify.AuthenticateAsync();
            if (token == null)
            {
                _toast.Show("Spotify", "Connection cancelled or timed out. Check the Client ID and redirect URI.", ToastType.Warning, key: "spotify-auth-failed");
                return;
            }

            await spotify.ApplyTokenResultAsync(token);
            IntegrationSettings.IntgrSpotify = true;
            await spotify.StartAsync();
            _toast.Show("Spotify", "Connected securely with PKCE. You're ready to show Spotify in chatbox.", ToastType.Success, key: "spotify-connected");
        }
        finally
        {
            Display.IsConnecting = false;
            IsConnecting = false;
            RefreshStatus();
            RefreshPreview();
        }
    }

    [RelayCommand]
    private async Task DisconnectSpotifyAsync()
    {
        var spotify = Modules.Spotify;
        if (spotify == null)
            return;

        await spotify.DisconnectAsync();
        IntegrationSettings.IntgrSpotify = false;
        RefreshStatus();
        RefreshPreview();
    }

    [RelayCommand]
    private async Task RefreshSpotifyAsync()
    {
        if (Modules.Spotify != null)
            await Modules.Spotify.TriggerManualRefreshAsync();
    }

    [RelayCommand]
    private void OpenDeveloperDashboard()
        => Navigation.OpenUrl(Constants.SpotifyDeveloperDashboardUrl);

    [RelayCommand]
    private void CopyRedirectUri()
        => CopyText(RedirectUri, "Redirect URI copied to clipboard.", "spotify-redirect-copied");

    [RelayCommand]
    private void CopyTemplateTokens()
        => CopyText(
            "{play_icon}, {title}, {artist}, {album}, {device}, {progress}, {seekbar}, {elapsed}, {duration}, {remaining}, {percent}, {liked_icon}, {explicit_icon}, {shuffle_icon}, {repeat_icon}, {queue}",
            "Template token list copied to clipboard.",
            "spotify-tokens-copied");

    [RelayCommand]
    private void OpenCurrentTrack()
    {
        if (Display.CanOpenSpotify)
            Navigation.OpenUrl(Display.ExternalUrl);
    }

    [RelayCommand]
    private void CopyOutputPreview()
    {
        if (string.IsNullOrWhiteSpace(OutputPreview))
            return;

        CopyText(OutputPreview, "Preview copied to clipboard.", "spotify-preview-copied");
    }

    private void OnDisplayChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshStatus();
        if (e.PropertyName is nameof(SpotifyDisplayState.OutputPreview) or nameof(SpotifyDisplayState.Title) or nameof(SpotifyDisplayState.Artist)
            or nameof(SpotifyDisplayState.IsConnected))
            RefreshPreview();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpotifySettings.ProgressDisplayMode))
            OnPropertyChanged(nameof(IsSeekbarMode));

        if (e.PropertyName == nameof(SpotifySettings.OutputTemplate) ||
            e.PropertyName == nameof(SpotifySettings.PartyTemplate) ||
            e.PropertyName == nameof(SpotifySettings.SelectedSeekbarStyleId) ||
            e.PropertyName?.Contains("Progress", StringComparison.Ordinal) == true ||
            e.PropertyName?.StartsWith("Show", StringComparison.Ordinal) == true ||
            e.PropertyName?.StartsWith("Allow", StringComparison.Ordinal) == true ||
            e.PropertyName?.StartsWith("Icon", StringComparison.Ordinal) == true)
        {
            RefreshPreview();
        }
    }

    private void OnMediaLinkDisplayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaLinkDisplayState.MediaLinkSeekbarStyles))
        {
            OnPropertyChanged(nameof(SeekbarStyles));
            OnPropertyChanged(nameof(SelectedSeekbarStyle));
            RefreshPreview();
        }
    }

    private void RefreshStatus()
    {
        if (Display.IsConnecting || IsConnecting)
            StatusText = "Waiting for Spotify authorization in your browser...";
        else if (Display.NeedsReconnect)
            StatusText = "Reconnect Spotify";
        else if (Display.IsConnected)
            StatusText = string.IsNullOrWhiteSpace(Display.ProfileName)
                ? Display.StatusText
                : $"Connected as {Display.ProfileName} · {Display.StatusText}";
        else
            StatusText = "Not connected";
    }

    private void RefreshPreview()
    {
        OutputPreview = Modules.Spotify?.BuildOutputString(useSample: true) ?? string.Empty;
        PreviewLengthText = $"{OutputPreview.Length}/{Constants.OscMaxMessageLength}";
    }

    private void CopyText(string text, string successMessage, string key)
    {
        try
        {
            Clipboard.SetText(text);
            _toast.Show("Spotify", successMessage, ToastType.Success, key: key);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("Spotify", "Could not copy to clipboard.", ToastType.Warning, key: key + "-failed");
        }
    }
}
