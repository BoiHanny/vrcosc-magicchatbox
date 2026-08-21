using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using vrcosc_magicchatbox.Core.Configuration;

namespace vrcosc_magicchatbox.Classes.Modules.Voicemod;

public enum VoicemodSoundSort
{
    [Description("Recent")]
    Recent,

    [Description("A-Z")]
    Name,
}

public partial class VoicemodSettings : VersionedSettings
{
    public const int MinimumSoundAnnouncementDurationSeconds = 2;
    public const int MaximumSoundAnnouncementDurationSeconds = 15;
    public const int MaximumRecentSounds = 40;
    public const int MinimumSoundsPerPage = 8;
    public const int MaximumSoundsPerPage = 96;

    [ObservableProperty] private bool _voiceControlEnabled;
    [ObservableProperty] private bool _soundboardControlEnabled = true;
    [ObservableProperty] private bool _micControlEnabled;

    [ObservableProperty] private bool _announceSoundboardToChat = true;
    [ObservableProperty] private int _soundAnnouncementDurationSeconds = 8;
    [ObservableProperty] private bool _announceVoiceToChat;

    [ObservableProperty] private VoicemodSoundSort _soundSort = VoicemodSoundSort.Recent;

    [ObservableProperty] private int _soundsPerPage = 24;
    [ObservableProperty] private bool _compactSoundBlobs;
    [ObservableProperty] private bool _showSoundThumbnails = true;
    [ObservableProperty] private bool _showSoundboardStrip = true;

    partial void OnSoundsPerPageChanged(int value)
    {
        if (value < MinimumSoundsPerPage)
        {
            SoundsPerPage = MinimumSoundsPerPage;
            return;
        }

        if (value > MaximumSoundsPerPage)
            SoundsPerPage = MaximumSoundsPerPage;
    }

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<string> _favoriteSoundIds = new();

    [ObservableProperty]
    [property: JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private ObservableCollection<string> _recentSoundIds = new();

    [ObservableProperty] private string _localClientKeyEncrypted = string.Empty;

    public bool AnyFeatureEnabled
        => VoiceControlEnabled || SoundboardControlEnabled || MicControlEnabled;

    public bool LiveSwitchesEnabled => VoiceControlEnabled || MicControlEnabled;

    public bool IsFavoriteSound(string soundId)
        => !string.IsNullOrWhiteSpace(soundId)
           && FavoriteSoundIds.Contains(soundId, StringComparer.OrdinalIgnoreCase);

    public void SetFavoriteSound(string soundId, bool favorite)
    {
        if (string.IsNullOrWhiteSpace(soundId))
            return;

        string? existing = FavoriteSoundIds.FirstOrDefault(
            id => string.Equals(id, soundId, StringComparison.OrdinalIgnoreCase));

        if (favorite)
        {
            if (existing == null)
                FavoriteSoundIds.Add(soundId);
            return;
        }

        if (existing != null)
            FavoriteSoundIds.Remove(existing);
    }

    public void RecordSoundUse(string soundId)
    {
        if (string.IsNullOrWhiteSpace(soundId))
            return;

        string? existing = RecentSoundIds.FirstOrDefault(
            id => string.Equals(id, soundId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            RecentSoundIds.Remove(existing);

        RecentSoundIds.Insert(0, soundId);

        while (RecentSoundIds.Count > MaximumRecentSounds)
            RecentSoundIds.RemoveAt(RecentSoundIds.Count - 1);
    }

    public int RecentRank(string soundId)
    {
        for (int index = 0; index < RecentSoundIds.Count; index++)
        {
            if (string.Equals(RecentSoundIds[index], soundId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return int.MaxValue;
    }

    partial void OnVoiceControlEnabledChanged(bool value) => RaiseFeatureAggregates();

    partial void OnSoundboardControlEnabledChanged(bool value) => RaiseFeatureAggregates();

    partial void OnMicControlEnabledChanged(bool value) => RaiseFeatureAggregates();

    private void RaiseFeatureAggregates()
    {
        OnPropertyChanged(nameof(AnyFeatureEnabled));
        OnPropertyChanged(nameof(LiveSwitchesEnabled));
    }

    partial void OnSoundAnnouncementDurationSecondsChanged(int value)
    {
        if (value < MinimumSoundAnnouncementDurationSeconds)
        {
            SoundAnnouncementDurationSeconds = MinimumSoundAnnouncementDurationSeconds;
            return;
        }

        if (value > MaximumSoundAnnouncementDurationSeconds)
            SoundAnnouncementDurationSeconds = MaximumSoundAnnouncementDurationSeconds;
    }
}
