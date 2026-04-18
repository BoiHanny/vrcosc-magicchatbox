using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Classes.Modules;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Services;
using vrcosc_magicchatbox.ViewModels.Models;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Manages audio device enumeration and TikTok TTS voice loading.
/// </summary>
public sealed class AudioService : IAudioService
{
    private readonly TtsAudioDisplayState _ttsAudio;
    private readonly ISettingsProvider<TtsSettings> _ttsSettingsProvider;

    public AudioService(TtsAudioDisplayState ttsAudio, ISettingsProvider<TtsSettings> ttsSettingsProvider)
    {
        _ttsAudio = ttsAudio;
        _ttsSettingsProvider = ttsSettingsProvider;
    }

    public bool PopulateOutputDevices()
    {
        try
        {
            _ttsAudio.PlaybackOutputDevices.Clear();

            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                    .OrderBy(mmDevice => mmDevice.FriendlyName);

            int index = 0;
            foreach (var mmDevice in devices)
            {
                var audioDevice = new AudioDevice(
                    mmDevice.FriendlyName,
                    mmDevice.ID,
                    index++
                );
                _ttsAudio.PlaybackOutputDevices.Add(audioDevice);
            }

            var defaultMMDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var defaultAudioDevice = new AudioDevice(
                defaultMMDevice.FriendlyName,
                defaultMMDevice.ID,
                -1
            );

            var ttsSettings = _ttsSettingsProvider.Value;

            if (string.IsNullOrEmpty(ttsSettings.RecentPlayBackOutput))
            {
                _ttsAudio.SelectedPlaybackOutputDevice = defaultAudioDevice;
                ttsSettings.RecentPlayBackOutput = defaultAudioDevice.FriendlyName;
            }
            else
            {
                var matching = _ttsAudio.PlaybackOutputDevices
                    .FirstOrDefault(dev => dev.FriendlyName == ttsSettings.RecentPlayBackOutput);

                if (matching != null)
                {
                    _ttsAudio.SelectedPlaybackOutputDevice = matching;
                }
                else
                {
                    _ttsAudio.SelectedPlaybackOutputDevice = defaultAudioDevice;
                    ttsSettings.RecentPlayBackOutput = defaultAudioDevice.FriendlyName;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return false;
        }
    }

    public List<Voice> ReadTikTokTTSVoices()
    {
        try
        {
            string currentrunningAppdir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string voicesFilePath = Path.Combine(currentrunningAppdir, "Json", "voices.json");
            string json = File.ReadAllText(voicesFilePath);
            List<Voice> ConfirmList = JsonConvert.DeserializeObject<List<Voice>>(json);

            var ttsSettings = _ttsSettingsProvider.Value;

            if (string.IsNullOrEmpty(ttsSettings.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
            {
                ttsSettings.RecentTikTokTTSVoice = "en_us_001";
            }
            if (!string.IsNullOrEmpty(ttsSettings.RecentTikTokTTSVoice) || ConfirmList.Count == 0)
            {
                Voice selectedVoice = ConfirmList.FirstOrDefault(v => v.ApiName == ttsSettings.RecentTikTokTTSVoice);
                if (selectedVoice != null)
                {
                    _ttsAudio.SelectedTikTokTTSVoice = selectedVoice;
                }
            }

            return ConfirmList;
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return null;
        }
    }

    public void EnsureLogDirectoryExists(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
