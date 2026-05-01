using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
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

    public async Task<byte[]?> TryGetAudioBytesFromTikTokAPI(string text)
    {
        try
        {
            if (_ttsAudio.SelectedTikTokTTSVoice == null)
            {
                const string message = "No TikTok TTS voice is selected.";
                Logging.WriteInfo($"TTS generation skipped: {message}");
                _toast?.Show("🔊 TTS", message, ToastType.Warning, key: "tts-no-voice-selected");
                return null;
            }

            var client = _httpClientFactory.CreateClient(Core.Constants.HttpClients.Tts);
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
            {
                Logging.WriteInfo("TTS generation failed: API response did not contain audioUrl or data.");
                _toast?.Show("🔊 TTS", "TTS server response did not include audio data.", ToastType.Warning, key: "tts-missing-audio");
                return null;
            }

            string audioInBase64 = audioToken.ToString();
            var commaIndex = audioInBase64.IndexOf(',');
            if (commaIndex >= 0)
                audioInBase64 = audioInBase64.Substring(commaIndex + 1);

            return Convert.FromBase64String(audioInBase64);
        }
        catch (TaskCanceledException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", "TTS request timed out. The online TTS service may be unavailable.", ToastType.Warning, key: "tts-generation-timeout");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", BuildHttpErrorMessage(ex), ToastType.Warning, key: "tts-generation-http");
            return null;
        }
        catch (JsonException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", "TTS server returned an unreadable response.", ToastType.Warning, key: "tts-generation-json");
            return null;
        }
        catch (FormatException ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            _toast?.Show("🔊 TTS", "TTS server returned invalid audio data.", ToastType.Warning, key: "tts-generation-format");
            return null;
        }
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
            MMDevice? device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                         .FirstOrDefault(d => d.ID == deviceId);

            if (device == null)
            {
                Logging.WriteInfo($"TTS playback device '{deviceId}' was not found. Falling back to the default multimedia output.");
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

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

    private static string BuildHttpErrorMessage(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                "TTS service rejected the request. The online service may have changed access rules.",
            HttpStatusCode.NotFound =>
                "TTS service endpoint was not found. The online service may have moved or shut down.",
            HttpStatusCode.TooManyRequests =>
                "TTS service rate-limited the request. Please wait and try again.",
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout =>
                "TTS service is temporarily unavailable.",
            { } status =>
                $"TTS service returned HTTP {(int)status} ({status}).",
            null =>
                "Could not reach the TTS service. Check your internet connection."
        };
    }
}
