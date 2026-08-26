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
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.Services.Voicemod;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.ViewModels.Sections;

public partial class VoicemodSectionViewModel : ObservableObject
{
    private readonly Lazy<IModuleHost> _moduleHost;
    private readonly IVoicemodClientKeyProvider _clientKeyProvider;
    private readonly ISettingsProvider<IntegrationSettings> _integrationSettingsProvider;
    private readonly ISettingsProvider<AppSettings> _appSettingsProvider;
    private readonly ISettingsProvider<VoicemodSettings> _settingsProvider;
    private readonly IPrivacyConsentService _consentService;
    private readonly IMenuNavigationService _menuNavigation;
    private readonly IToastService _toast;
    private readonly IVoicemodArtworkCache _artwork;

    public VoicemodDisplayState Display { get; }
    public IntegrationSettings IntegrationSettings => _integrationSettingsProvider.Value;
    public AppSettings AppSettings => _appSettingsProvider.Value;
    public VoicemodSettings Settings => _settingsProvider.Value;

    public ObservableCollection<VoicemodVoice> FilteredVoices { get; } = new();
    public ObservableCollection<VoicemodSoundItem> FilteredSounds { get; } = new();
    public ObservableCollection<VoicemodParameterEditor> ParameterEditors { get; } = new();

    public ObservableCollection<VoicemodRandomVoiceMode> RandomVoiceModes { get; } = new();

    [ObservableProperty] private string _voiceSearchText = string.Empty;
    [ObservableProperty] private string _soundSearchText = string.Empty;
    [ObservableProperty] private VoicemodVoice? _selectedVoice;
    [ObservableProperty] private VoicemodSoundboard? _selectedSoundboard;
    [ObservableProperty] private VoicemodSoundScope? _selectedScope;
    [ObservableProperty] private int _soundPageIndex;
    [ObservableProperty] private VoicemodSoundItem? _selectedSound;
    [ObservableProperty] private VoicemodRandomVoiceMode _selectedRandomMode =
        VoicemodRandomVoiceMode.AllVoices;

    public bool CanControl => Display.CanControl;
    public bool HasError => !string.IsNullOrWhiteSpace(Display.ErrorText);
    public bool NeedsPermission => Display.ConnectionState == VoicemodConnectionState.PermissionRequired;
    public bool MissingClientKey => Display.ConnectionState == VoicemodConnectionState.NotConfigured;
    public bool HasSavedLocalClientKey => _clientKeyProvider.HasLocalClientKey;
    public string LocalClientKeyStatus => HasSavedLocalClientKey
        ? "A local client key is saved for this Windows user."
        : "No local key is saved. A build-injected key will be used when available.";
    public bool VoiceChangerIsOff => Display.IsConnected && !Display.VoiceChangerEnabled;

    public bool ShowsRotatingVoiceCountdown
        => Display.IsConnected && Display.IsFreeLicense && Display.RotatingVoicesRefreshAt != null;

    public string RotatingVoiceCountdownText
    {
        get
        {
            if (Display.RotatingVoicesRefreshAt == null)
                return string.Empty;

            TimeSpan remaining = Display.RotatingVoicesRefreshAt.Value - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
                return "New free voices are available now.";

            return remaining.TotalHours >= 1
                ? $"New free voices in {(int)remaining.TotalHours}h {remaining.Minutes}m."
                : $"New free voices in {Math.Max(1, (int)remaining.TotalMinutes)}m.";
        }
    }

    public System.Windows.Media.ImageSource? SelectedVoiceArtwork
        => SelectedVoice == null ? null : _artwork.Get("voice", SelectedVoice.Id);

    public System.Windows.Media.ImageSource? SelectedSoundArtwork
        => SelectedSound == null ? null : _artwork.Get("sound", SelectedSound.Id);

    public int SoundsPerPage => Math.Clamp(
        Settings.SoundsPerPage,
        VoicemodSettings.MinimumSoundsPerPage,
        VoicemodSettings.MaximumSoundsPerPage);

    public double SoundBlobHeight => Settings.CompactSoundBlobs ? 21 : 26;

    public double SoundBlobMaxWidth => Settings.CompactSoundBlobs ? 132 : 188;

    private IReadOnlyList<VoicemodSoundItem> _matchingSounds = [];

    public string SoundSortButtonText
        => Settings.SoundSort == VoicemodSoundSort.Recent ? "Recent" : "A-Z";

    public string SoundCountText => _matchingSounds.Count switch
    {
        0 => string.Empty,
        1 => "1 sound",
        _ => $"{_matchingSounds.Count} sounds",
    };

    public int SoundPageCount
        => Math.Max(1, (int)Math.Ceiling(_matchingSounds.Count / (double)SoundsPerPage));

    public string SoundPageText => $"{SoundPageIndex + 1} / {SoundPageCount}";

    public bool HasMultipleSoundPages => SoundPageCount > 1;

    public bool CanGoToPreviousSoundPage => SoundPageIndex > 0;

    public bool CanGoToNextSoundPage => SoundPageIndex < SoundPageCount - 1;

    public bool ShowingFavorites => SelectedScope?.Id == FavoritesScopeId;

    public string EmptySoundsText => ShowingFavorites
        ? "No pinned sounds yet. Hover any sound and press its star to keep it here, whichever board it lives on."
        : "No sounds here. Connect Voicemod, pick another board, or clear the search.";

    public bool HasSelectedVoiceArtwork => SelectedVoiceArtwork != null;

    public bool HasSelectedSoundArtwork => SelectedSoundArtwork != null;

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

    public VoicemodSectionViewModel(
        Lazy<IModuleHost> moduleHost,
        IVoicemodClientKeyProvider clientKeyProvider,
        VoicemodDisplayState display,
        ISettingsProvider<IntegrationSettings> integrationSettingsProvider,
        ISettingsProvider<AppSettings> appSettingsProvider,
        ISettingsProvider<VoicemodSettings> settingsProvider,
        IPrivacyConsentService consentService,
        IMenuNavigationService menuNavigation,
        IToastService toast,
        IVoicemodArtworkCache artwork)
    {
        _artwork = artwork;
        _moduleHost = moduleHost;
        _clientKeyProvider = clientKeyProvider;
        Display = display;
        _integrationSettingsProvider = integrationSettingsProvider;
        _appSettingsProvider = appSettingsProvider;
        _settingsProvider = settingsProvider;
        _consentService = consentService;
        _menuNavigation = menuNavigation;
        _toast = toast;

        Display.PropertyChanged += OnDisplayPropertyChanged;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
        _artwork.ArtworkStored += OnArtworkStored;
        RebuildRandomVoiceModes();
        RebuildVoices();
        RebuildSoundScopes();
        RebuildParameters();
    }

    partial void OnVoiceSearchTextChanged(string value) => RebuildVoices();

    partial void OnSoundSearchTextChanged(string value) => RebuildSounds();

    partial void OnSelectedSoundboardChanged(VoicemodSoundboard? value) => RebuildSounds();

    partial void OnSelectedScopeChanged(VoicemodSoundScope? value)
    {
        foreach (VoicemodSoundScope scope in SoundScopes)
            scope.IsSelected = ReferenceEquals(scope, value);

        // Landing on page 4 of a board you just left would look like a bug, so every scope change
        // starts from the top.
        _soundPageIndex = 0;
        OnPropertyChanged(nameof(SoundPageIndex));
        OnPropertyChanged(nameof(ShowingFavorites));
        OnPropertyChanged(nameof(EmptySoundsText));
        RebuildSounds();
    }

    partial void OnSoundPageIndexChanged(int value) => RebuildCurrentPage();

    [RelayCommand]
    private void SelectSoundScope(VoicemodSoundScope? scope)
    {
        if (scope != null)
            SelectedScope = scope;
    }

    [RelayCommand]
    private void NextSoundPage()
    {
        if (CanGoToNextSoundPage)
            SoundPageIndex++;
    }

    [RelayCommand]
    private void PreviousSoundPage()
    {
        if (CanGoToPreviousSoundPage)
            SoundPageIndex--;
    }

    partial void OnSelectedVoiceChanged(VoicemodVoice? value)
    {
        OnPropertyChanged(nameof(SelectedVoiceArtwork));
        OnPropertyChanged(nameof(HasSelectedVoiceArtwork));

        if (value == null || !Display.CanControl || _artwork.Contains("voice", value.Id))
            return;

        _ = RunControlAsync(
            (module, cancellationToken) => module.RequestVoiceArtworkAsync(value.Id, cancellationToken),
            $"Could not load artwork for {value.FriendlyName}");
    }

    partial void OnSelectedSoundChanged(VoicemodSoundItem? value)
    {
        OnPropertyChanged(nameof(SelectedSoundArtwork));
        OnPropertyChanged(nameof(HasSelectedSoundArtwork));

        if (value == null || !Display.CanControl || _artwork.Contains("sound", value.Id))
            return;

        _ = RunControlAsync(
            (module, cancellationToken) => module.RequestSoundArtworkAsync(value.Id, cancellationToken),
            $"Could not load artwork for {value.Name}");
    }

    private void OnArtworkStored(object? sender, VoicemodArtworkStoredEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedVoiceArtwork));
        OnPropertyChanged(nameof(HasSelectedVoiceArtwork));
        OnPropertyChanged(nameof(SelectedSoundArtwork));
        OnPropertyChanged(nameof(HasSelectedSoundArtwork));

        if (!Settings.ShowSoundThumbnails)
            return;

        foreach (VoicemodSoundItem item in FilteredSounds)
        {
            if (item.Artwork == null
                && string.Equals(e.Key, VoicemodArtworkCache.BuildKey("sound", item.Id), StringComparison.OrdinalIgnoreCase))
            {
                item.Artwork = _artwork.Get("sound", item.Id);
            }
        }
    }

    [RelayCommand]
    private Task SaveLocalClientKey(string? clientKey) => SaveLocalClientKeyAsync(clientKey);

    public async Task<bool> SaveLocalClientKeyAsync(string? clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            _toast.Show(
                "🎙️ Voicemod",
                "Paste the Voicemod client key before saving.",
                ToastType.Warning,
                key: "voicemod-client-key-empty");
            return false;
        }

        try
        {
            _clientKeyProvider.SaveLocalClientKey(clientKey);
            OnPropertyChanged(nameof(HasSavedLocalClientKey));
            OnPropertyChanged(nameof(LocalClientKeyStatus));
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast.Show(
                "🎙️ Voicemod",
                "Windows could not save the client key securely.",
                ToastType.Warning,
                key: "voicemod-client-key-save-failed");
            return false;
        }

        _toast.Show(
            "🎙️ Voicemod",
            "Client key saved securely for this Windows user.",
            ToastType.Success,
            key: "voicemod-client-key-saved");

        if (IntegrationSettings.IntgrVoicemod && _consentService.IsApproved(PrivacyHook.VoicemodControl))
        {
            await RunControlAsync(
                (module, cancellationToken) => module.ReconnectAsync(cancellationToken),
                "Could not reconnect to Voicemod").ConfigureAwait(false);
        }

        return true;
    }

    [RelayCommand]
    private async Task ClearLocalClientKey()
    {
        _clientKeyProvider.ClearLocalClientKey();
        OnPropertyChanged(nameof(HasSavedLocalClientKey));
        OnPropertyChanged(nameof(LocalClientKeyStatus));

        _toast.Show(
            "🎙️ Voicemod",
            "The local client key was removed.",
            ToastType.Info,
            key: "voicemod-client-key-cleared");

        if (IntegrationSettings.IntgrVoicemod && _consentService.IsApproved(PrivacyHook.VoicemodControl))
        {
            await RunControlAsync(
                (module, cancellationToken) => module.ReconnectAsync(cancellationToken),
                "Could not reconnect to Voicemod").ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task Reconnect()
    {
        if (!_consentService.IsApproved(PrivacyHook.VoicemodControl))
        {
            OpenPrivacy();
            return;
        }

        await RunControlAsync(
            (module, cancellationToken) => module.ReconnectAsync(cancellationToken),
            "Could not connect to Voicemod").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task Refresh()
        => await RunControlAsync(
            (module, cancellationToken) => module.RefreshAsync(cancellationToken),
            "Could not refresh Voicemod").ConfigureAwait(false);

    [RelayCommand]
    private async Task ApplyVoice()
        => await ApplyVoiceAsync(SelectedVoice).ConfigureAwait(false);

    private async Task ApplyVoiceAsync(VoicemodVoice? voice)
    {
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
            (module, cancellationToken) => module.LoadVoiceAsync(voice.Id, cancellationToken),
            $"Could not load {voice.FriendlyName}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DisableVoice()
        => await RunControlAsync(
            (module, cancellationToken) => module.LoadVoiceAsync("nofx", cancellationToken),
            "Could not turn off the voice effect").ConfigureAwait(false);

    [RelayCommand]
    private async Task SelectRandomVoice()
        => await RunControlAsync(
            (module, cancellationToken) => module.SelectRandomVoiceAsync(SelectedRandomMode, cancellationToken),
            "Could not select a random voice").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleVoiceChanger()
        => await RunControlAsync(
            (module, cancellationToken) => module.ToggleVoiceChangerAsync(cancellationToken),
            "Could not toggle the voice changer").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleHearMyself()
        => await RunControlAsync(
            (module, cancellationToken) => module.ToggleHearMyselfAsync(cancellationToken),
            "Could not toggle Hear myself").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleBackground()
        => await RunControlAsync(
            (module, cancellationToken) => module.ToggleBackgroundEffectsAsync(cancellationToken),
            "Could not toggle background effects").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleMicrophoneMute()
        => await RunControlAsync(
            (module, cancellationToken) => module.ToggleMicrophoneMuteAsync(cancellationToken),
            "Could not toggle the Voicemod microphone").ConfigureAwait(false);

    [RelayCommand]
    private async Task ToggleSoundboardMute()
        => await RunControlAsync(
            (module, cancellationToken) => module.ToggleSoundboardMuteForMeAsync(cancellationToken),
            "Could not change soundboard monitoring").ConfigureAwait(false);

    [RelayCommand]
    private async Task PlaySound(VoicemodSoundItem? item)
        => await PlaySoundAsync(item).ConfigureAwait(false);

    [RelayCommand]
    private void ToggleSoundFavorite(VoicemodSoundItem? item)
    {
        if (item == null)
            return;

        Settings.SetFavoriteSound(item.Id, !item.IsFavorite);
        item.IsFavorite = Settings.IsFavoriteSound(item.Id);
        _settingsProvider.Save();
        RebuildSounds();
    }

    [RelayCommand]
    private void CycleSoundSort()
    {
        Settings.SoundSort = Settings.SoundSort == VoicemodSoundSort.Recent
            ? VoicemodSoundSort.Name
            : VoicemodSoundSort.Recent;
        _settingsProvider.Save();
        OnPropertyChanged(nameof(SoundSortButtonText));
        // Reset the page silently: the property's changed hook rebuilds the current page from the
        // still-unsorted list, which RebuildSounds() below then immediately rebuilds again.
        _soundPageIndex = 0;
        OnPropertyChanged(nameof(SoundPageIndex));
        RebuildSounds();
    }

    private async Task PlaySoundAsync(VoicemodSoundItem? item)
    {
        if (item == null)
            return;
        if (!item.Sound.Enabled)
        {
            _toast.Show(
                "🎙️ Voicemod",
                $"{item.Name} is not available for the current Voicemod license.",
                ToastType.Warning,
                durationMs: 5000,
                key: "voicemod-sound-unavailable");
            return;
        }

        Settings.RecordSoundUse(item.Id);
        _settingsProvider.Save();

        // Recording the play is not enough on its own - under Recent ordering the sound has just
        // become the most recent one, so the list has to be rebuilt for it to actually move.
        if (Settings.SoundSort == VoicemodSoundSort.Recent)
            RebuildSounds();

        await RunControlAsync(
            (module, cancellationToken) => module.PlaySoundAsync(item.Id, item.Name, cancellationToken),
            $"Could not play {item.Name}").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StopAllSounds()
        => await RunControlAsync(
            (module, cancellationToken) => module.StopAllSoundsAsync(cancellationToken),
            "Could not stop Voicemod sounds").ConfigureAwait(false);

    [RelayCommand]
    private async Task BeginBleep()
        => await RunControlAsync(
            (module, cancellationToken) => module.SetBleepAsync(true, cancellationToken),
            "Could not start the bleep").ConfigureAwait(false);

    [RelayCommand]
    private async Task EndBleep()
        => await RunControlAsync(
            (module, cancellationToken) => module.SetBleepAsync(false, cancellationToken),
            "Could not stop the bleep").ConfigureAwait(false);

    [RelayCommand]
    private async Task ApplyParameter(VoicemodParameterEditor? editor)
    {
        if (editor == null)
            return;

        await RunControlAsync(
            (module, cancellationToken) => module.SetVoiceParameterAsync(
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

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VoicemodDisplayState.HasVoices):
                RebuildVoices();
                break;
            case nameof(VoicemodDisplayState.HasSoundboards):
            case nameof(VoicemodDisplayState.HasAllSounds):
                RebuildSoundScopes();
                break;
            case nameof(VoicemodDisplayState.ActiveSoundboardId):
                FollowVoicemodsActiveBoard();
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
            OnPropertyChanged(nameof(VoiceChangerIsOff));
            OnPropertyChanged(nameof(ShowsRotatingVoiceCountdown));
        }

        if (e.PropertyName is nameof(VoicemodDisplayState.LicenseType)
            or nameof(VoicemodDisplayState.ConnectionState))
        {
            RebuildRandomVoiceModes();
        }

        if (e.PropertyName == nameof(VoicemodDisplayState.RotatingVoicesRefreshAt))
        {
            OnPropertyChanged(nameof(ShowsRotatingVoiceCountdown));
            OnPropertyChanged(nameof(RotatingVoiceCountdownText));
        }

        if (e.PropertyName == nameof(VoicemodDisplayState.VoiceChangerEnabled))
        {
            OnPropertyChanged(nameof(VoiceChangerButtonText));
            OnPropertyChanged(nameof(VoiceChangerIsOff));
        }
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

    private void RebuildRandomVoiceModes()
    {
        RandomVoiceModes.Clear();
        foreach (VoicemodRandomVoiceMode mode in Enum.GetValues<VoicemodRandomVoiceMode>())
        {
            if (mode == VoicemodRandomVoiceMode.AllVoices && Display.IsFreeLicense)
                continue;

            RandomVoiceModes.Add(mode);
        }

        if (!RandomVoiceModes.Contains(SelectedRandomMode))
            SelectedRandomMode = RandomVoiceModes.FirstOrDefault();
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

    public const string FavoritesScopeId = "magicchatbox:favorites";
    public const string AllSoundsScopeId = "magicchatbox:all-sounds";

    public ObservableCollection<VoicemodSoundScope> SoundScopes { get; } = new();

    public ObservableCollection<VoicemodSoundScope> BoardScopes { get; } = new();

    public VoicemodSoundScope? FavoritesScope => SoundScopes.FirstOrDefault();

    public VoicemodSoundScope? AllScope => SoundScopes.Skip(1).FirstOrDefault();

    /// <summary>Letters that actually start a sound in the current result set, with the page holding them.</summary>
    public ObservableCollection<VoicemodAlphabetJump> AlphabetJumps { get; } = new();

    public bool ShowsAlphabetJumps
        => Settings.SoundSort == VoicemodSoundSort.Name && AlphabetJumps.Count > 1;

    [RelayCommand]
    private void JumpToAlphabet(VoicemodAlphabetJump? jump)
    {
        if (jump != null)
            SoundPageIndex = jump.PageIndex;
    }

    private void RebuildAlphabetJumps()
    {
        AlphabetJumps.Clear();

        if (Settings.SoundSort == VoicemodSoundSort.Name)
        {
            var seen = new HashSet<char>();
            for (int index = 0; index < _matchingSounds.Count; index++)
            {
                string name = _matchingSounds[index].Name;
                char letter = name.Length > 0 ? char.ToUpperInvariant(name[0]) : '#';
                if (!char.IsLetter(letter))
                    letter = '#';

                if (seen.Add(letter))
                    AlphabetJumps.Add(new VoicemodAlphabetJump(letter.ToString(), index / SoundsPerPage));
            }
        }

        OnPropertyChanged(nameof(ShowsAlphabetJumps));
    }

    private void RebuildSoundScopes()
    {
        string? previousId = SelectedScope?.Id;

        SoundScopes.Clear();
        BoardScopes.Clear();
        SoundScopes.Add(new VoicemodSoundScope(FavoritesScopeId, "★", "Sounds you pinned, from every board"));
        SoundScopes.Add(new VoicemodSoundScope(AllSoundsScopeId, "All", "Every sound you own"));

        foreach (VoicemodSoundboard soundboard in Display.Soundboards)
        {
            var scope = new VoicemodSoundScope(soundboard.Id, soundboard.Name, soundboard.Name);
            SoundScopes.Add(scope);
            BoardScopes.Add(scope);
        }

        OnPropertyChanged(nameof(FavoritesScope));
        OnPropertyChanged(nameof(AllScope));

        // Landing on an empty Favourites tab on first connect would look broken, so it is only the
        // default once there is actually something pinned.
        VoicemodSoundScope? favorites = SoundScopes[0];
        VoicemodSoundScope? all = SoundScopes[1];

        SelectedScope = SoundScopes.FirstOrDefault(scope =>
                string.Equals(scope.Id, previousId, StringComparison.OrdinalIgnoreCase))
            ?? SoundScopes.FirstOrDefault(scope =>
                string.Equals(scope.Id, Display.ActiveSoundboardId, StringComparison.OrdinalIgnoreCase))
            ?? (Settings.FavoriteSoundIds.Count > 0 ? favorites : all);

        RebuildSounds();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VoicemodSettings.SoundsPerPage):
                OnPropertyChanged(nameof(SoundsPerPage));
                // Silent for the same reason as CycleSoundSort: the changed hook would rebuild the
                // page against the page size still in effect.
                _soundPageIndex = 0;
                OnPropertyChanged(nameof(SoundPageIndex));
                RebuildSounds();
                break;
            case nameof(VoicemodSettings.CompactSoundBlobs):
                OnPropertyChanged(nameof(SoundBlobHeight));
                OnPropertyChanged(nameof(SoundBlobMaxWidth));
                break;
            case nameof(VoicemodSettings.ShowSoundThumbnails):
                LoadArtworkForCurrentPage();
                break;
        }
    }

    private void FollowVoicemodsActiveBoard()
    {
        // The API has no way to set the active board, only to be told about it. Following along is
        // the only way the two stay in step - but not at the cost of yanking someone off Favourites
        // or All, which are deliberate choices rather than a board.
        if (SelectedScope != null
            && (SelectedScope.Id == FavoritesScopeId || SelectedScope.Id == AllSoundsScopeId))
        {
            return;
        }

        VoicemodSoundScope? active = SoundScopes.FirstOrDefault(scope =>
            string.Equals(scope.Id, Display.ActiveSoundboardId, StringComparison.OrdinalIgnoreCase));

        if (active != null)
            SelectedScope = active;
    }

    private IEnumerable<VoicemodSound> AllKnownSounds()
    {
        if (Display.AllSounds.Count > 0)
            return Display.AllSounds;

        // getMemes is the flat catalogue, but it is not guaranteed to have answered, so fall back to
        // stitching the boards together. Without this, favourites vanish on a partial sync.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stitched = new List<VoicemodSound>();
        foreach (VoicemodSoundboard board in Display.Soundboards)
        {
            foreach (VoicemodSound sound in board.Sounds)
            {
                if (seen.Add(sound.Id))
                    stitched.Add(sound);
            }
        }

        return stitched;
    }

    private IEnumerable<VoicemodSound> SoundsInScope()
    {
        if (SelectedScope == null)
            return Enumerable.Empty<VoicemodSound>();

        if (SelectedScope.Id == FavoritesScopeId)
            return AllKnownSounds().Where(sound => Settings.IsFavoriteSound(sound.Id));

        if (SelectedScope.Id == AllSoundsScopeId)
            return AllKnownSounds();

        return Display.Soundboards
            .FirstOrDefault(board =>
                string.Equals(board.Id, SelectedScope.Id, StringComparison.OrdinalIgnoreCase))
            ?.Sounds
            ?? Enumerable.Empty<VoicemodSound>();
    }


    private void RebuildSounds()
    {
        string search = SoundSearchText.Trim();
        IEnumerable<VoicemodSound> sounds = SoundsInScope();

        if (search.Length > 0)
        {
            sounds = sounds.Where(sound =>
                sound.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || sound.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _matchingSounds = SortSounds(sounds, Settings);
        RebuildAlphabetJumps();

        int lastPage = Math.Max(0, SoundPageCount - 1);
        if (SoundPageIndex > lastPage)
        {
            SoundPageIndex = lastPage;
            return;
        }

        RebuildCurrentPage();
    }

    private void RebuildCurrentPage()
    {
        VoicemodSoundItem? previous = SelectedSound;

        FilteredSounds.Clear();
        foreach (VoicemodSoundItem item in _matchingSounds.Skip(SoundPageIndex * SoundsPerPage).Take(SoundsPerPage))
            FilteredSounds.Add(item);

        SelectedSound = FilteredSounds.FirstOrDefault(item =>
                string.Equals(item.Id, previous?.Id, StringComparison.OrdinalIgnoreCase))
            ?? FilteredSounds.FirstOrDefault();

        OnPropertyChanged(nameof(HasFilteredSounds));
        OnPropertyChanged(nameof(SoundCountText));
        OnPropertyChanged(nameof(SoundPageCount));
        OnPropertyChanged(nameof(SoundPageText));
        OnPropertyChanged(nameof(HasMultipleSoundPages));
        OnPropertyChanged(nameof(CanGoToPreviousSoundPage));
        OnPropertyChanged(nameof(CanGoToNextSoundPage));

        LoadArtworkForCurrentPage();
    }

    /// <summary>
    /// Only ever the page in front of you. Asking for all 1600 icons at once would flood the socket
    /// for pictures nobody is looking at; a page is a couple of dozen and is naturally bounded.
    /// </summary>
    private void LoadArtworkForCurrentPage()
    {
        if (!Settings.ShowSoundThumbnails)
        {
            foreach (VoicemodSoundItem cleared in FilteredSounds)
                cleared.Artwork = null;
            return;
        }

        foreach (VoicemodSoundItem item in FilteredSounds)
        {
            item.Artwork = _artwork.Get("sound", item.Id);
            if (item.Artwork != null || !Display.CanControl)
                continue;

            VoicemodSoundItem pending = item;
            _ = RunControlAsync(
                (module, cancellationToken) => module.RequestSoundArtworkAsync(pending.Id, cancellationToken),
                $"Could not load artwork for {pending.Name}");
        }
    }

    public static IReadOnlyList<VoicemodSoundItem> SortSounds(
        IEnumerable<VoicemodSound> sounds,
        VoicemodSettings settings)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        ArgumentNullException.ThrowIfNull(settings);

        IEnumerable<VoicemodSoundItem> items = sounds.Select(sound =>
            new VoicemodSoundItem(sound, settings.IsFavoriteSound(sound.Id)));

        IOrderedEnumerable<VoicemodSoundItem> ordered = items.OrderByDescending(item => item.IsFavorite);

        ordered = settings.SoundSort == VoicemodSoundSort.Recent
            ? ordered
                .ThenBy(item => settings.RecentRank(item.Id))
                .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            : ordered.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase);

        return ordered.ToArray();
    }

    private void RebuildParameters()
    {
        ParameterEditors.Clear();
        foreach (VoicemodVoiceParameter parameter in Display.Parameters)
            ParameterEditors.Add(new VoicemodParameterEditor(parameter));

        OnPropertyChanged(nameof(HasParameterEditors));
    }

    private async Task RunControlAsync(
        Func<VoicemodModule, CancellationToken, Task> action,
        string failureMessage)
    {
        VoicemodModule? module = _moduleHost.Value.Voicemod;
        if (module == null)
        {
            _toast.Show(
                "🎙️ Voicemod",
                "Voicemod control is not available in this session. Restart MagicChatbox to try again.",
                ToastType.Warning,
                durationMs: 6000,
                key: "voicemod-module-unavailable");
            return;
        }

        try
        {
            await action(module, CancellationToken.None).ConfigureAwait(false);
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

public sealed class VoicemodAlphabetJump
{
    public string Letter { get; }
    public int PageIndex { get; }

    public VoicemodAlphabetJump(string letter, int pageIndex)
    {
        Letter = letter;
        PageIndex = pageIndex;
    }
}

public sealed partial class VoicemodSoundScope : ObservableObject
{
    public string Id { get; }
    public string Label { get; }
    public string Tooltip { get; }

    [ObservableProperty] private bool _isSelected;

    public VoicemodSoundScope(string id, string label, string tooltip)
    {
        Id = id;
        Label = label;
        Tooltip = tooltip;
    }
}

public partial class VoicemodSoundItem : ObservableObject
{
    public VoicemodSound Sound { get; }

    public string Id => Sound.Id;
    public string Name => Sound.Name;
    public bool IsAvailable => Sound.Enabled;

    [ObservableProperty] private bool _isFavorite;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtwork))]
    private System.Windows.Media.ImageSource? _artwork;

    public bool HasArtwork => Artwork != null;

    public VoicemodSoundItem(VoicemodSound sound, bool isFavorite)
    {
        Sound = sound;
        _isFavorite = isFavorite;
    }

    public string Tooltip => IsAvailable
        ? $"{Name} — click to play, star to pin"
        : $"{Name} — not available for the current Voicemod license";
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
