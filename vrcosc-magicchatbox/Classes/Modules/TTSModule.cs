using NAudio.Wave;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public static class TTSModule
    {
        public static async Task<byte[]> GetAudioBytesFromTikTokAPI(string text)
        {
            byte[] audioBytes = null;
            try
            {
                var url = "https://tiktok-tts.weilnet.workers.dev/api/generation";
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
                    var dataHere = JObject.Parse(result.ToString()).SelectToken("data").ToString();
                    audioInBase64 = dataHere.ToString();
                }

                audioBytes = Convert.FromBase64String(audioInBase64);
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: true, MSGBox: false);
                return audioBytes;
            }
            return audioBytes;
        }

        private static void UpdateVolume(WaveOutEvent waveOut) { waveOut.Volume = ViewModel.Instance.TTSVolume; }

        public static async Task PlayTikTokAudioAsSpeech(
            CancellationToken cancellationToken,
            byte[] audio,
            int outputDeviceNumber)
        {
            try
            {
                using (var audioStream = new MemoryStream(audio))
                {
                    audioStream.Position = 0;
                    var audioReader = new Mp3FileReader(audioStream);

                    var waveOut = new WaveOutEvent();
                    if (outputDeviceNumber >= 0 && outputDeviceNumber < WaveOut.DeviceCount)
                    {
                        waveOut.DeviceNumber = outputDeviceNumber;
                    }

                    waveOut.Init(audioReader);

                    OSCSender.ToggleVoice();
                    Thread.Sleep(175);

                    waveOut.Play();

                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        UpdateVolume(waveOut); // Add this line to update the volume

                        if (cancellationToken.IsCancellationRequested)
                        {
                            waveOut.Stop();
                            OSCSender.ToggleVoice();
                            break;
                        }
                        await Task.Delay(100);
                    }
                    OSCSender.ToggleVoice();
                }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex, makeVMDump: false, MSGBox: false);
            }
        }
    }
}