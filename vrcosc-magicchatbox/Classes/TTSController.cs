using NAudio.Wave;
using NAudio.CoreAudioApi;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace vrcosc_magicchatbox.Classes
{
    public class TTSController
    {
        private ViewModel _VM;
        public TTSController(ViewModel vm)
        {
            _VM = vm;
        }

        public async Task<byte[]> GetAudioBytesFromTikTokAPI(string text)
        {
            var url = "https://tiktok-tts.weilnet.workers.dev/api/generation";
            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.Method = "POST";
            httpRequest.ContentType = "application/json";
            var data = "{\"text\":\"" + text + "\",\"voice\":\"" + _VM.SelectedTikTokTTSVoice.ApiName + "\"}";

            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
            {
                streamWriter.Write(data);
            }

            var httpResponse = (HttpWebResponse)await httpRequest.GetResponseAsync();
            string audioInBase64 = "";
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = await streamReader.ReadToEndAsync();
                var dataHere = JObject.Parse(result.ToString()).SelectToken("data").ToString();
                audioInBase64 = dataHere.ToString();
            }

            var audioBytes = Convert.FromBase64String(audioInBase64);
            return audioBytes;
        }

        public async Task PlayTikTokAudioAsSpeech(CancellationToken cancellationToken, byte[] audio, int outputDeviceNumber)
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
                    waveOut.Play();

                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            waveOut.Stop();
                            break;
                        }
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                // handle the exception here
            }
        }


    }
}