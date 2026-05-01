using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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

namespace vrcosc_magicchatbox.ViewModels.Sections;

/// <summary>
/// Section ViewModel for Discord Voice integration options.
/// Handles OAuth connect/disconnect, exposes settings, and provides live output preview.
/// </summary>
public partial class DiscordSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly Lazy<DiscordOAuthHandler> _oAuth;
    private readonly INavigationService _nav;
    private readonly DiscordRichPresenceService _richPresence;
    private readonly IToastService _toast;

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    /// <summary>Direct access to Discord settings for XAML binding (Rich Presence toggle, etc.).</summary>
    public DiscordSettings? DiscordModuleSettings => _moduleHost.Value.Discord?.Settings;

    [ObservableProperty] private bool _hasSavedToken;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _outputPreview = string.Empty;
    [ObservableProperty] private string _previewLengthText = $"0/{Constants.OscMaxMessageLength}";
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string _redirectPortStatus = string.Empty;
    [ObservableProperty] private string? _selectedPresetName;
    [ObservableProperty] private string? _selectedRpPresetName;

    /// <summary>Template preset names for UI combo box.</summary>
    public string[] PresetNames { get; } = DiscordSettings.TemplatePresets
        .Select(p => p.Name).ToArray();

    /// <summary>Rich Presence preset names for RP combo box.</summary>
    public string[] RpPresetNames { get; } = DiscordSettings.RichPresencePresets
        .Select(p => p.Name).ToArray();

    public string RedirectUri => Constants.DiscordOAuthRedirectUri;

    public DiscordSectionViewModel(
        Lazy<IModuleHost> moduleHost,
        Lazy<DiscordOAuthHandler> oAuth,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        INavigationService nav,
        DiscordRichPresenceService richPresence,
        IToastService toast)
    {
        _moduleHost = moduleHost;
        _oAuth = oAuth;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        _nav = nav;
        _richPresence = richPresence;
        _toast = toast;

        // Check if we already have a saved token
        var discord = _moduleHost.Value.Discord;
        if (discord != null)
        {
            HasSavedToken = !string.IsNullOrWhiteSpace(discord.Settings.AccessToken);

            // Subscribe to module state changes for live status + preview
            discord.PropertyChanged += OnDiscordModulePropertyChanged;
            discord.Settings.PropertyChanged += OnDiscordSettingsPropertyChanged;
        }

        RefreshStatus();
        RefreshPreview();
        CheckRedirectPort();
    }

    partial void OnSelectedPresetNameChanged(string? value)
    {
        if (value == null) return;
        var preset = DiscordSettings.TemplatePresets.FirstOrDefault(p => p.Name == value);
        if (preset != default)
        {
            var discord = _moduleHost.Value.Discord;
            if (discord != null)
                discord.Settings.Template = preset.Value;
        }
    }

    partial void OnSelectedRpPresetNameChanged(string? value)
    {
        if (value == null) return;
        var preset = DiscordSettings.RichPresencePresets.FirstOrDefault(p => p.Name == value);
        if (preset != default)
        {
            var discord = _moduleHost.Value.Discord;
            if (discord != null)
            {
                discord.Settings.RichPresenceDetails = preset.Details;
                discord.Settings.RichPresenceState = preset.State;
            }
        }
    }

    [RelayCommand]
    private void OpenDiscordDeveloperPortal()
        => _nav.OpenUrl("https://discord.com/developers/applications");

    [RelayCommand]
    private void CopyRedirectUri()
    {
        if (CopyText(RedirectUri, "Redirect URI copied to clipboard.", "discord-redirect-copied"))
            RedirectPortStatus = $"Copied {RedirectUri}";
        else
            RedirectPortStatus = "Could not copy redirect URI. Copy it manually from the field.";
    }

    [RelayCommand]
    private void CopyTemplateTokens()
        => CopyText(
            "{channel}, {count}, {speaking}, {speaking_count}, {mute_emoji}, {mute_state}, {voice_state}",
            "Template token list copied to clipboard.",
            "discord-tokens-copied");

    [RelayCommand]
    private void CopyOutputPreview()
    {
        if (string.IsNullOrWhiteSpace(OutputPreview))
            return;

        CopyText(OutputPreview, "Preview copied to clipboard.", "discord-preview-copied");
    }

    [RelayCommand]
    private void CheckRedirectPort()
        => _ = CheckRedirectPortAvailability();

    [RelayCommand]
    private async Task ConnectDiscordAsync()
    {
        if (IsConnecting) return;
        IsConnecting = true;

        try
        {
            var discord = _moduleHost.Value.Discord;
            if (discord == null)
            {
                RedirectPortStatus = "Discord module is not ready yet. Try again after startup finishes.";
                return;
            }

            await discord.StopAsync();
            if (!CheckRedirectPortAvailability())
                return;

            Logging.WriteInfo($"Discord voice: starting v0.9.181 implicit-grant flow (clientId={MaskClientId(discord.EffectiveVoiceClientId)}, richPresenceRunning={_richPresence.IsRunning}).");
            var (result, hasRpcScope) = await _oAuth.Value.AuthenticateImplicitAsync(discord.EffectiveVoiceClientId);
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                RedirectPortStatus = "Discord authorization did not complete. Check the Application ID, redirect URI, and Discord's rpc scope approval.";
                return;
            }

            discord.Settings.AccessToken = result.AccessToken;
            discord.Settings.HasRpcScope = hasRpcScope;
            discord.Settings.RefreshToken = string.Empty;
            discord.Settings.TokenExpiresAtUtcTicks = 0;
            discord.SaveSettings();
            HasSavedToken = true;

            await discord.StartAsync();
            RedirectPortStatus = hasRpcScope
                ? "Discord authorized. MagicChatbox is now authenticating through the local Discord client."
                : "Discord connected, but the token did not include rpc voice scope. Speaking detection may be unavailable.";
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            IsConnecting = false;
            RefreshStatus();
        }
    }

    [RelayCommand]
    private async Task DisconnectDiscordAsync()
    {
        try
        {
            var discord = _moduleHost.Value.Discord;
            if (discord == null) return;

            await discord.StopAsync();
            discord.Settings.AccessToken = string.Empty;
            discord.Settings.RefreshToken = string.Empty;
            discord.Settings.TokenExpiresAtUtcTicks = 0;
            discord.Settings.HasRpcScope = false;
            discord.SaveSettings();
            HasSavedToken = false;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }

    private bool CheckRedirectPortAvailability()
    {
        var discord = _moduleHost.Value.Discord;
        string applicationIdStatus = string.Empty;
        if (discord != null
            && !DiscordOAuthHandler.TryNormalizeClientId(discord.Settings.VoiceClientId, out _, out applicationIdStatus))
        {
            RedirectPortStatus = applicationIdStatus;
            return false;
        }

        bool available = DiscordOAuthHandler.IsRedirectPortAvailable(out string status);
        RedirectPortStatus = discord == null || !available
            ? status
            : $"{applicationIdStatus} {status}";
        return available;
    }

    /// <summary>
    /// Called by ModuleBootstrapper after all modules are created.
    /// If AutoConnectOnStartup is enabled and we have a saved token, start the module.
    /// </summary>
    public async Task TryAutoConnectAsync()
    {
        var discord = _moduleHost.Value.Discord;
        if (discord == null) return;

        if (discord.Settings.AutoConnectOnStartup &&
            !string.IsNullOrWhiteSpace(discord.Settings.AccessToken))
        {
            Logging.WriteInfo("Discord: Auto-connecting on startup...");
            await discord.StartAsync();
        }
    }

    // --- Live refresh from module property changes ---

    private void OnDiscordModulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DiscordModule.IsRunning):
            case nameof(DiscordModule.IsAuthenticated):
            case nameof(DiscordModule.IsReady):
            case nameof(DiscordModule.IsInVoiceChannel):
            case nameof(DiscordModule.CurrentChannelName):
            case nameof(DiscordModule.VoiceChannelCount):
            case nameof(DiscordModule.IsSelfMuted):
            case nameof(DiscordModule.IsSelfDeafened):
                RefreshStatus();
                RefreshPreview();
                break;
        }
    }

    private void OnDiscordSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscordSettings.VoiceClientId))
            CheckRedirectPortAvailability();

        RefreshPreview();
    }

    private void RefreshStatus()
    {
        var discord = _moduleHost.Value.Discord;
        if (discord == null || !HasSavedToken)
        {
            StatusText = "Not connected";
            return;
        }

        if (!discord.IsRunning)
        {
            StatusText = "Disconnected (IPC not running)";
        }
        else if (!discord.IsReady)
        {
            StatusText = "Connecting...";
        }
        else if (!discord.IsAuthenticated && !string.IsNullOrWhiteSpace(discord.Settings.AccessToken))
        {
            StatusText = "✅ Connected (Rich Presence active, authenticating...)";
        }
        else if (!discord.IsInVoiceChannel)
        {
            StatusText = "✅ Connected — not in a voice channel";
        }
        else
        {
            StatusText = $"✅ In #{discord.CurrentChannelName} ({discord.VoiceChannelCount} users)";
        }
    }

    private void RefreshPreview()
    {
        var discord = _moduleHost.Value.Discord;
        if (discord == null)
        {
            OutputPreview = string.Empty;
            PreviewLengthText = $"0/{Constants.OscMaxMessageLength}";
            return;
        }

        OutputPreview = discord.GetOutputString();
        PreviewLengthText = $"{OutputPreview.Length}/{Constants.OscMaxMessageLength}";
    }

    private bool CopyText(string text, string successMessage, string key)
    {
        try
        {
            Clipboard.SetText(text);
            _toast.Show("Discord", successMessage, ToastType.Success, key: key);
            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show("Discord", "Could not copy to clipboard.", ToastType.Warning, key: key + "-failed");
            return false;
        }
    }

    private static string MaskClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return "empty";

        string trimmed = clientId.Trim();
        return trimmed.Length <= 6
            ? "***"
            : $"{trimmed[..3]}***{trimmed[^3..]}";
    }
}
