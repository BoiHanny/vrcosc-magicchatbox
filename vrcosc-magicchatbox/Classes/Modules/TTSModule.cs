using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;
using vrcosc_magicchatbox.ViewModels.State;

namespace vrcosc_magicchatbox.Classes.Modules;

/// <summary>
/// Text-to-speech service using TikTok TTS API. Registered as DI singleton.
/// </summary>
public class TTSModule
{
    private readonly TtsSettings _ttsSettings;
    private readonly TtsAudioDisplayState _ttsAudio;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOscSender _oscSender;
    private readonly IToastService? _toast;

    public TTSModule(TtsSettings ttsSettings, TtsAudioDisplayState ttsAudio, IHttpClientFactory httpClientFactory, IOscSender oscSender, IToastService? toast = null)
    {
        _ttsSettings = ttsSettings;
        _ttsAudio = ttsAudio;
        _httpClientFactory = httpClientFactory;
        _oscSender = oscSender;
        _toast = toast;
    }

    public async Task<byte[]> GetAudioBytesFromTikTokAPI(string text)
    {
        byte[] audioBytes = null;
        try
        {
            var client = _httpClientFactory.CreateClient("TTS");
            var url = Core.Constants.TikTokTtsApiUrl;
            var payload = JsonConvert.SerializeObject(new
            {
                text = text,
                voice = _ttsAudio.SelectedTikTokTTSVoice.ApiName
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(result);
            var audioToken = json.SelectToken("audioUrl") ?? json.SelectToken("data");
            if (audioToken == null)
                return audioBytes;

            string audioInBase64 = audioToken.ToString();
            var commaIndex = audioInBase64.IndexOf(',');
            if (commaIndex >= 0)
                audioInBase64 = audioInBase64.Substring(commaIndex + 1);

            audioBytes = Convert.FromBase64String(audioInBase64);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", "Failed to generate speech audio. Check your internet connection.", ToastType.Warning, key: "tts-generation-failed");
            return audioBytes;
        }

        return audioBytes;
    }

    public async Task PlayTikTokAudioAsSpeechAsync(
        byte[] audioData,
        string deviceId,
        CancellationToken cancelToken)
    {
        if (audioData == null || audioData.Length == 0)
            return;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                        .FirstOrDefault(d => d.ID == deviceId);

            if (device == null)
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            using var mp3Stream = new MemoryStream(audioData);
            using var mp3Reader = new Mp3FileReader(mp3Stream);
            using var wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);

            wasapiOut.Init(mp3Reader);
            wasapiOut.Volume = _ttsSettings.TtsVolume;

            // Zero-CPU wait: TaskCompletionSource completes when NAudio fires PlaybackStopped.
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            wasapiOut.PlaybackStopped += (_, _) => tcs.TrySetResult();

            _oscSender.ToggleVoice();
            await Task.Delay(175);

            wasapiOut.Play();

            // Cancellation stops playback → triggers PlaybackStopped → completes tcs.
            using var reg = cancelToken.Register(() => wasapiOut.Stop());

            await tcs.Task;

            _oscSender.ToggleVoice();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", "Audio playback failed. Check your audio device settings.", ToastType.Warning, key: "tts-playback-failed");
        }
    }
}
