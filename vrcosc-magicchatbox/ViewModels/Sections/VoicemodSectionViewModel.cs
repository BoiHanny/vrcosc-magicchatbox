using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Privacy;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class VoicemodSectionViewModel : ObservableObject
{
    private readonly VoicemodModule _module;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly IPrivacyConsentService _consentService;
    private readonly IMenuNavigationService _menuNavigation;
    private readonly INavigationService _navigation;
    private readonly IToastService _toast;

    public VoicemodDisplayState Display { get; }
    public IntegrationSettings IntegrationSettings => _integrationSettingsProvider.Value;

    public ObservableCollection<VoicemodVoice> FilteredVoices { get; } = new();
    public ObservableCollection<VoicemodSound> FilteredSounds { get; } = new();
    public ObservableCollection<VoicemodParameterEditor> ParameterEditors { get; } = new();

    public IReadOnlyList<VoicemodRandomVoiceMode> RandomVoiceModes { get; } =
        Enum.GetValues<VoicemodRandomVoiceMode>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelButtonText))]
    private bool _isExpanded;
    [ObservableProperty] private string _voiceSearchText = string.Empty;
    [ObservableProperty] private string _soundSearchText = string.Empty;
    [ObservableProperty] private VoicemodVoice? _selectedVoice;
    [ObservableProperty] private VoicemodSoundboard? _selectedSoundboard;
    [ObservableProperty] private VoicemodSound? _selectedSound;
    [ObservableProperty] private VoicemodRandomVoiceMode _selectedRandomMode =
        VoicemodRandomVoiceMode.AllVoices;

    public bool CanControl => Display.CanControl;
    public bool HasError => !string.IsNullOrWhiteSpace(Display.ErrorText);
    public bool NeedsPermission => Display.ConnectionState == VoicemodConnectionState.PermissionRequired;
    public bool MissingClientKey => Display.ConnectionState == VoicemodConnectionState.NotConfigured;
    public bool HasFilteredVoices => FilteredVoices.Count > 0;
    public bool HasFilteredSounds => FilteredSounds.Count > 0;
    public bool HasParameterEditors => ParameterEditors.Count > 0;

    public string ConnectionDetails
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Display.LicenseType))
                parts.Add($"{Display.LicenseType} license");
            if (!string.IsNullOrWhiteSpace(Display.AppVersion))
                parts.Add($"Voicemod {Display.AppVersion}");
            if (Display.LastSynchronizedAt != null)
                parts.Add($"synced {Display.LastSynchronizedAt:HH:mm:ss}");
            return parts.Count == 0 ? "Waiting for Voicemod state" : string.Join(" · ", parts);
        }
    }

    public string VoiceChangerButtonText =>
        $"Voice changer: {(Display.VoiceChangerEnabled ? "ON" : "OFF")}";

    public string HearMyselfButtonText =>
        $"Hear myself: {(Display.HearMyselfEnabled ? "ON" : "OFF")}";

    public string BackgroundButtonText =>
        $"Background: {(Display.BackgroundEffectsEnabled ? "ON" : "OFF")}";

    public string MicrophoneMuteButtonText =>
        $"Microphone: {(Display.MicrophoneMuted ? "MUTED" : "LIVE")}";

    public string SoundboardMuteButtonText =>
        $"Hear sounds: {(Display.SoundboardMutedForMe ? "OFF" : "ON")}";

    public string BleepButtonText => Display.IsBleeping ? "BLEEPING — release" : "Hold to bleep";
    public string PanelButtonText => IsExpanded ? "CLOSE ↑" : "CONTROLS ↓";

    public VoicemodSectionViewModel(
        VoicemodModule module,
        VoicemodDisplayState display,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        IPrivacyConsentService consentService,
        IMenuNavigationService menuNavigation,
        INavigationService navigation,
        IToastService toast)
    {
        _module = module;
        Display = display;
        _integrationSettingsProvider = integrationSettingsProvider;
        _consentService = consentService;
        _menuNavigation = menuNavigation;
        _navigation = navigation;
        _toast = toast;

        Display.PropertyChanged += OnDisplayPropertyChanged;
        RebuildVoices();
        RebuildSoundboards();
        RebuildParameters();
    }

    partial void OnVoiceSearchTextChanged(string value) => RebuildVoices();

    partial void OnSoundSearchTextChanged(string value) => RebuildSounds();

    partial void OnSelectedSoundboardChanged(VoicemodSoundboard? value) => RebuildSounds();

    [RelayCommand]
    private async Task Reconnect()
    {
        if (!_consentService.IsApproved(PrivacyHook.VoicemodControl))
        {
            OpenPrivacy();
            return;
        }

        await RunControlAsync(
            cancellationToken => _module.ReconnectAsync(cancellationToken),
            "Could not connect to Voicemod").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task Refresh()
        => await RunControlAsync(
            cancellationToken => _module.RefreshAsync(cancellationToken),
            "Could not refresh Voicemod").ConfigureAwait(false);

    [RelayCommand]
    private async Task ApplyVoice()
    {
        VoicemodVoice? voice = SelectedVoice;
        if (voice == null)
            return;
        if (!voice.Enabled)
        {
            _toast.Show(
                "🎙️ Voicemod",
                $"{voice.FriendlyName} is not available for the current Voicemod license.",
                ToastType.Warning,
                durationMs: 5000,
                key: "voicemod-voice-unavailable");
            return;
        }

        await RunControlAsync(
            cancellationToken => _module.LoadVoiceAsync(voice.Id, cancellationToken),
            $"Could not load {voice.FriendlyName}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DisableVoice()
        => await RunControlAsync(
            cancellationToken => _module.LoadVoiceAsync("nofx", cancellationToken),
            "Could not turn off the voice effect").ConfigureAwait(false);

    [RelayCommand]
    private async Task SelectRandomVoice()
        => await RunControlAsync(
            cancellationToken => _module.SelectRandomVoiceAsync(SelectedRandomMode, cancellationToken),
            "Could not select a random voice").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleVoiceChanger()
        => await RunControlAsync(
            cancellationToken => _module.ToggleVoiceChangerAsync(cancellationToken),
            "Could not toggle the voice changer").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleHearMyself()
        => await RunControlAsync(
            cancellationToken => _module.ToggleHearMyselfAsync(cancellationToken),
            "Could not toggle Hear myself").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleBackground()
        => await RunControlAsync(
            cancellationToken => _module.ToggleBackgroundEffectsAsync(cancellationToken),
            "Could not toggle background effects").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleMicrophoneMute()
        => await RunControlAsync(
            cancellationToken => _module.ToggleMicrophoneMuteAsync(cancellationToken),
            "Could not toggle the Voicemod microphone").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleSoundboardMute()
        => await RunControlAsync(
            cancellationToken => _module.ToggleSoundboardMuteForMeAsync(cancellationToken),
            "Could not change soundboard monitoring").ConfigureAwait(false);

    [RelayCommand]
    private async Task PlaySelectedSound()
    {
        VoicemodSound? sound = SelectedSound;
        if (sound == null)
            return;
        if (!sound.Enabled)
        {
            _toast.Show(
                "🎙️ Voicemod",
                $"{sound.Name} is not available for the current Voicemod license.",
                ToastType.Warning,
                durationMs: 5000,
                key: "voicemod-sound-unavailable");
            return;
        }

        await RunControlAsync(
            cancellationToken => _module.PlaySoundAsync(sound.Id, cancellationToken),
            $"Could not play {sound.Name}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StopAllSounds()
        => await RunControlAsync(
            cancellationToken => _module.StopAllSoundsAsync(cancellationToken),
            "Could not stop Voicemod sounds").ConfigureAwait(false);

    [RelayCommand]
    private async Task BeginBleep()
        => await RunControlAsync(
            cancellationToken => _module.SetBleepAsync(true, cancellationToken),
            "Could not start the bleep").ConfigureAwait(false);

    [RelayCommand]
    private async Task EndBleep()
        => await RunControlAsync(
            cancellationToken => _module.SetBleepAsync(false, cancellationToken),
            "Could not stop the bleep").ConfigureAwait(false);

    [RelayCommand]
    private async Task ApplyParameter(VoicemodParameterEditor? editor)
    {
        if (editor == null)
            return;

        await RunControlAsync(
            cancellationToken => _module.SetVoiceParameterAsync(
                editor.Definition,
                editor.DraftValue,
                cancellationToken),
            $"Could not update {editor.Name}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ResetParameter(VoicemodParameterEditor? editor)
    {
        if (editor == null)
            return;

        editor.DraftValue = editor.DefaultValue;
        await ApplyParameter(editor).ConfigureAwait(false);
    }

    [RelayCommand]
    private void OpenPrivacy() => _menuNavigation.NavigateToPrivacy();

    [RelayCommand]
    private void OpenDocumentation()
        => _navigation.OpenUrl("https://control-api.voicemod.net/");

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VoicemodDisplayState.HasVoices):
                RebuildVoices();
                break;
            case nameof(VoicemodDisplayState.HasSoundboards):
                RebuildSoundboards();
                break;
            case nameof(VoicemodDisplayState.ParametersRevision):
                RebuildParameters();
                break;
            case nameof(VoicemodDisplayState.CurrentVoiceId):
                SyncSelectedVoice();
                break;
        }

        if (e.PropertyName is nameof(VoicemodDisplayState.ConnectionState)
            or nameof(VoicemodDisplayState.StatusText)
            or nameof(VoicemodDisplayState.ErrorText)
            or nameof(VoicemodDisplayState.ClientKeyConfigured)
            or nameof(VoicemodDisplayState.AppVersion)
            or nameof(VoicemodDisplayState.LicenseType)
            or nameof(VoicemodDisplayState.LastSynchronizedAt))
        {
            OnPropertyChanged(nameof(CanControl));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(NeedsPermission));
            OnPropertyChanged(nameof(MissingClientKey));
            OnPropertyChanged(nameof(ConnectionDetails));
        }

        if (e.PropertyName == nameof(VoicemodDisplayState.VoiceChangerEnabled))
            OnPropertyChanged(nameof(VoiceChangerButtonText));
        if (e.PropertyName == nameof(VoicemodDisplayState.HearMyselfEnabled))
            OnPropertyChanged(nameof(HearMyselfButtonText));
        if (e.PropertyName == nameof(VoicemodDisplayState.BackgroundEffectsEnabled))
            OnPropertyChanged(nameof(BackgroundButtonText));
        if (e.PropertyName == nameof(VoicemodDisplayState.MicrophoneMuted))
            OnPropertyChanged(nameof(MicrophoneMuteButtonText));
        if (e.PropertyName == nameof(VoicemodDisplayState.SoundboardMutedForMe))
            OnPropertyChanged(nameof(SoundboardMuteButtonText));
        if (e.PropertyName == nameof(VoicemodDisplayState.IsBleeping))
            OnPropertyChanged(nameof(BleepButtonText));
    }

    private void RebuildVoices()
    {
        string search = VoiceSearchText.Trim();
        IEnumerable<VoicemodVoice> voices = Display.Voices;
        if (search.Length > 0)
        {
            voices = voices.Where(voice =>
                voice.FriendlyName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || voice.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        FilteredVoices.Clear();
        foreach (VoicemodVoice voice in voices)
            FilteredVoices.Add(voice);

        SyncSelectedVoice();
        OnPropertyChanged(nameof(HasFilteredVoices));
    }

    private void SyncSelectedVoice()
    {
        VoicemodVoice? current = FilteredVoices.FirstOrDefault(voice =>
            string.Equals(voice.Id, Display.CurrentVoiceId, StringComparison.OrdinalIgnoreCase));
        if (current != null)
            SelectedVoice = current;
        else if (SelectedVoice == null || !FilteredVoices.Contains(SelectedVoice))
            SelectedVoice = FilteredVoices.FirstOrDefault();
    }

    private void RebuildSoundboards()
    {
        VoicemodSoundboard? previous = SelectedSoundboard;
        SelectedSoundboard = Display.Soundboards.FirstOrDefault(soundboard =>
                string.Equals(soundboard.Id, previous?.Id, StringComparison.OrdinalIgnoreCase))
            ?? Display.Soundboards.FirstOrDefault(soundboard =>
                string.Equals(soundboard.Id, Display.ActiveSoundboardId, StringComparison.OrdinalIgnoreCase))
            ?? Display.Soundboards.FirstOrDefault(soundboard => soundboard.Enabled)
            ?? Display.Soundboards.FirstOrDefault();

        RebuildSounds();
    }

    private void RebuildSounds()
    {
        string search = SoundSearchText.Trim();
        IEnumerable<VoicemodSound> sounds = SelectedSoundboard?.Sounds
            ?? Enumerable.Empty<VoicemodSound>();

        if (search.Length > 0)
        {
            sounds = sounds.Where(sound =>
                sound.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || sound.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        VoicemodSound? previous = SelectedSound;
        FilteredSounds.Clear();
        foreach (VoicemodSound sound in sounds.OrderBy(
                     sound => sound.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            FilteredSounds.Add(sound);
        }

        SelectedSound = FilteredSounds.FirstOrDefault(sound =>
                string.Equals(sound.Id, previous?.Id, StringComparison.OrdinalIgnoreCase))
            ?? FilteredSounds.FirstOrDefault();
        OnPropertyChanged(nameof(HasFilteredSounds));
    }

    private void RebuildParameters()
    {
        ParameterEditors.Clear();
        foreach (VoicemodVoiceParameter parameter in Display.Parameters)
            ParameterEditors.Add(new VoicemodParameterEditor(parameter));

        OnPropertyChanged(nameof(HasParameterEditors));
    }

    private async Task RunControlAsync(
        Func<CancellationToken, Task> action,
        string failureMessage)
    {
        try
        {
            await action(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show(
                "🎙️ Voicemod",
                $"{failureMessage}: {ex.Message}",
                ToastType.Warning,
                durationMs: 6000,
                key: "voicemod-control-error");
        }
    }
}

public partial class VoicemodParameterEditor : ObservableObject
{
    public VoicemodVoiceParameter Definition { get; }
    public string Name => Definition.Name;
    public double Minimum => Definition.Minimum;
    public double Maximum => Definition.Maximum;
    public double DefaultValue => Definition.DefaultValue;
    public bool CanChange => Maximum > Minimum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private double _draftValue;

    public string ValueText => DraftValue.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    public VoicemodParameterEditor(VoicemodVoiceParameter definition)
    {
        Definition = definition;
        _draftValue = definition.Value;
    }
}
