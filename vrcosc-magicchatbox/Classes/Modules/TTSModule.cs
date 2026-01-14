using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules;

public static class TTSModule
{

    private static void UpdateVolume(WaveOutEvent waveOut) { waveOut.Volume = ViewModel.Instance.TTSVolume; }

    public static async Task<byte[]> GetAudioBytesFromTikTokAPI(string text)
    {
        byte[] audioBytes = null;
        try
        {
            var url = "https://gesserit.co/api/tiktok-tts";
            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.Method = "POST";
            httpRequest.ContentType = "application/json";
            var data = "{\"text\":\"" +
                text +
                "\",\"voice\":\"" +
                ViewModel.Instance.SelectedTikTokTTSVoice.ApiName +
                "\"}";

            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
                streamWriter.Write(data);

            var httpResponse = (HttpWebResponse)await httpRequest.GetResponseAsync();
            string audioInBase64 = string.Empty;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = await streamReader.ReadToEndAsync();
                var json = JObject.Parse(result);
                var audioToken = json.SelectToken("audioUrl") ?? json.SelectToken("data");
                if (audioToken == null)
                {
                    return audioBytes;
                }

                audioInBase64 = audioToken.ToString();
            }

            var commaIndex = audioInBase64.IndexOf(',');
            if (commaIndex >= 0)
            {
                audioInBase64 = audioInBase64.Substring(commaIndex + 1);
            }

            audioBytes = Convert.FromBase64String(audioInBase64);
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
            return audioBytes;
        }
        return audioBytes;
    }

    public static async Task PlayTikTokAudioAsSpeechAsync(
        byte[] audioData,
        string deviceId,
        CancellationToken cancelToken
    )
    {
        if (audioData == null || audioData.Length == 0)
            return; 

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                                        .FirstOrDefault(d => d.ID == deviceId);

            if (device == null)
            {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            using var mp3Stream = new MemoryStream(audioData);
            using var mp3Reader = new Mp3FileReader(mp3Stream);

            using var wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);

            wasapiOut.Init(mp3Reader);

            wasapiOut.Volume = ViewModel.Instance.TTSVolume;

            OSCSender.ToggleVoice();
            await Task.Delay(175);

            wasapiOut.Play();

            while (wasapiOut.PlaybackState == PlaybackState.Playing)
            {
                wasapiOut.Volume = ViewModel.Instance.TTSVolume;

                if (cancelToken.IsCancellationRequested)
                {
                    wasapiOut.Stop();
                    break;
                }
                await Task.Delay(100, cancelToken);
            }

            OSCSender.ToggleVoice();
        }
        catch (Exception ex)
        {
            Logging.WriteException(ex, MSGBox: false);
        }
    }
}
