using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
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

    public AppSettings AppSettings { get; }
    public IntegrationSettings IntegrationSettings { get; }
    public IModuleHost Modules => _moduleHost.Value;

    [ObservableProperty] private bool _hasSavedToken;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string _outputPreview = string.Empty;
    [ObservableProperty] private string _statusText = "Not connected";
    [ObservableProperty] private string? _selectedPresetName;

    /// <summary>Template preset names for UI combo box.</summary>
    public string[] PresetNames { get; } = DiscordSettings.TemplatePresets
        .Select(p => p.Name).ToArray();

    public DiscordSectionViewModel(
        Lazy<IModuleHost> moduleHost,
        Lazy<DiscordOAuthHandler> oAuth,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        INavigationService nav)
    {
        _moduleHost = moduleHost;
        _oAuth = oAuth;
        AppSettings = appSettingsProvider.Value;
        IntegrationSettings = integrationSettingsProvider.Value;
        _nav = nav;

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

    [RelayCommand]
    private async Task ConnectDiscordAsync()
    {
        if (IsConnecting) return;
        IsConnecting = true;

        try
        {
            var discord = _moduleHost.Value.Discord;
            if (discord == null) return;

            await discord.StopAsync();

            var result = await _oAuth.Value.AuthenticateAsync();
            if (result == null || string.IsNullOrWhiteSpace(result.AccessToken)) return;

            discord.Settings.AccessToken = result.AccessToken;
            if (!string.IsNullOrEmpty(result.RefreshToken))
                discord.Settings.RefreshToken = result.RefreshToken;
            if (result.ExpiresIn > 0)
                discord.Settings.TokenExpiresAtUtcTicks = DateTime.UtcNow.AddSeconds(result.ExpiresIn).Ticks;
            discord.SaveSettings();
            HasSavedToken = true;

            await discord.StartAsync();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
        finally
        {
            IsConnecting = false;
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
            discord.SaveSettings();
            HasSavedToken = false;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
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
        else if (!discord.IsAuthenticated)
        {
            StatusText = "Connecting...";
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
            return;
        }

        OutputPreview = discord.GetOutputString();
    }
}
