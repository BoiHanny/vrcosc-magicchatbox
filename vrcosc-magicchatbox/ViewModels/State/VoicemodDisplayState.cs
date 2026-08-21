using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using vrcosc_magicchatbox.Classes.Modules.Voicemod;

namespace vrcosc_magicchatbox.ViewModels.State;

public partial class VoicemodDisplayState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(CanControl))]
    private VoicemodConnectionState _connectionState = VoicemodConnectionState.Disabled;

    partial void OnLicenseTypeChanged(string value) => OnPropertyChanged(nameof(IsFreeLicense));

    [ObservableProperty] private string _statusText = "Voicemod control is off";
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _clientKeyConfigured;
    [ObservableProperty] private int? _connectedPort;
    [ObservableProperty] private string _appVersion = string.Empty;
    [ObservableProperty] private string _licenseType = string.Empty;
    [ObservableProperty] private DateTime? _lastSynchronizedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFreeLicense))]
    private DateTime? _rotatingVoicesRefreshAt;

    [ObservableProperty] private string _userId = string.Empty;

    public bool IsFreeLicense
        => string.Equals(LicenseType, "free", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty] private bool _voiceChangerEnabled;
    [ObservableProperty] private bool _hearMyselfEnabled;
    [ObservableProperty] private bool _backgroundEffectsEnabled;
    [ObservableProperty] private bool _microphoneMuted;
    [ObservableProperty] private bool _soundboardMutedForMe;
    [ObservableProperty] private bool _isBleeping;

    [ObservableProperty]
    private string _currentVoiceId = "nofx";

    [ObservableProperty] private string _currentVoiceName = "No effect";

    [ObservableProperty] private string _activeSoundboardId = string.Empty;
    [ObservableProperty] private int _parametersRevision;
    [ObservableProperty] private string _lastPlayedSoundName = string.Empty;
    [ObservableProperty] private DateTime _lastSoundPlaybackStartedUtc;

    public ObservableCollection<VoicemodVoice> Voices { get; } = new();
    public ObservableCollection<VoicemodSoundboard> Soundboards { get; } = new();
    public ObservableCollection<VoicemodVoiceParameter> Parameters { get; } = new();
    public ObservableCollection<VoicemodSound> AllSounds { get; } = new();

    public bool HasAllSounds => AllSounds.Count > 0;

    public void ReplaceAllSounds(IEnumerable<VoicemodSound> sounds)
    {
        AllSounds.Clear();
        foreach (VoicemodSound sound in sounds.OrderBy(
                     sound => sound.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            AllSounds.Add(sound);
        }

        OnPropertyChanged(nameof(HasAllSounds));
    }

    public bool IsConnected => ConnectionState == VoicemodConnectionState.Connected;
    public bool CanControl => IsConnected;
    public bool HasVoices => Voices.Count > 0;
    public bool HasSoundboards => Soundboards.Count > 0;
    public bool HasParameters => Parameters.Count > 0;

    public void ReplaceVoices(IEnumerable<VoicemodVoice> voices, string currentVoiceId)
    {
        Voices.Clear();
        foreach (VoicemodVoice voice in voices.OrderBy(voice => voice.FriendlyName, StringComparer.CurrentCultureIgnoreCase))
            Voices.Add(voice);

        CurrentVoiceId = string.IsNullOrWhiteSpace(currentVoiceId) ? "nofx" : currentVoiceId;
        CurrentVoiceName = ResolveCurrentVoiceName();
        OnPropertyChanged(nameof(HasVoices));
    }

    public void ReplaceSoundboards(IEnumerable<VoicemodSoundboard> soundboards)
    {
        Soundboards.Clear();
        foreach (VoicemodSoundboard soundboard in soundboards.OrderBy(
                     soundboard => soundboard.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            Soundboards.Add(soundboard);
        }

        OnPropertyChanged(nameof(HasSoundboards));
    }

    public void ReplaceParameters(
        string voiceId,
        IEnumerable<VoicemodVoiceParameter> parameters)
    {
        CurrentVoiceId = string.IsNullOrWhiteSpace(voiceId) ? "nofx" : voiceId;
        CurrentVoiceName = ResolveCurrentVoiceName();
        Parameters.Clear();
        foreach (VoicemodVoiceParameter parameter in parameters.OrderBy(
                     parameter => parameter.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            Parameters.Add(parameter);
        }

        OnPropertyChanged(nameof(HasParameters));
        ParametersRevision++;
    }

    public void UpdateVoiceParameter(string parameterName, double value)
    {
        for (int index = 0; index < Parameters.Count; index++)
        {
            VoicemodVoiceParameter current = Parameters[index];
            if (!string.Equals(current.Key, parameterName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(current.Name, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Parameters[index] = current with { Value = value };
            ParametersRevision++;
            return;
        }
    }

    public void MarkSynchronized(string? appVersion = null)
    {
        if (!string.IsNullOrWhiteSpace(appVersion))
            AppVersion = appVersion;

        LastSynchronizedAt = DateTime.Now;
    }

    public void ClearCatalog()
    {
        Voices.Clear();
        Soundboards.Clear();
        Parameters.Clear();
        AllSounds.Clear();
        OnPropertyChanged(nameof(HasAllSounds));
        CurrentVoiceId = "nofx";
        CurrentVoiceName = "No effect";
        ActiveSoundboardId = string.Empty;
        LicenseType = string.Empty;
        RotatingVoicesRefreshAt = null;
        ParametersRevision++;
        OnPropertyChanged(nameof(HasVoices));
        OnPropertyChanged(nameof(HasSoundboards));
        OnPropertyChanged(nameof(HasParameters));
    }

    public void ResetSwitches()
    {
        VoiceChangerEnabled = false;
        HearMyselfEnabled = false;
        BackgroundEffectsEnabled = false;
        MicrophoneMuted = false;
        SoundboardMutedForMe = false;
        IsBleeping = false;
    }

    public void RecordSoundPlayback(string soundName)
    {
        LastPlayedSoundName = soundName?.Trim() ?? string.Empty;
        LastSoundPlaybackStartedUtc = DateTime.UtcNow;
    }

    public void ClearSoundPlayback()
    {
        LastPlayedSoundName = string.Empty;
        LastSoundPlaybackStartedUtc = default;
    }

    partial void OnCurrentVoiceIdChanged(string value)
        => CurrentVoiceName = ResolveCurrentVoiceName();

    private string ResolveCurrentVoiceName()
        => Voices.FirstOrDefault(voice =>
               string.Equals(voice.Id, CurrentVoiceId, StringComparison.OrdinalIgnoreCase))
           ?.FriendlyName
           ?? (string.Equals(CurrentVoiceId, "nofx", StringComparison.OrdinalIgnoreCase)
               ? "No effect"
               : CurrentVoiceId);
}
