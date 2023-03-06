using NAudio.Wave;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using System.Linq;

namespace vrcosc_magicchatbox.Classes
{
    public class TTSController
    {
        private ViewModel _VM;
        public TTSController(ViewModel vm)
        {
            _VM = vm;
        }

        public async Task PlayTikTokTextAsSpeech(string text, CancellationToken cancellationToken)
        {
            try
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

                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                string audioInBase64 = "";
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    var dataHere = JObject.Parse(result.ToString()).SelectToken("data").ToString();
                    audioInBase64 = dataHere.ToString();
                }

                var audioBytes = Convert.FromBase64String(audioInBase64);
                using (var audioStream = new MemoryStream(audioBytes))
                {
                    audioStream.Position = 0;
                    var audioReader = new Mp3FileReader(audioStream);

                    var enumerator = new MMDeviceEnumerator();

                    // Initialize Playback output device
                    var playbackOutputDevice = enumerator.GetDevice(_VM.SelectedPlaybackOutputDevice.Id);
                    using (var playbackOutput = new WasapiOut(playbackOutputDevice, AudioClientShareMode.Shared, false, 50))
                    {
                        playbackOutput.Init(audioReader);
                        playbackOutput.Play();
                        while (playbackOutput.PlaybackState == PlaybackState.Playing)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                playbackOutput.Stop();
                                break;
                            }
                            await Task.Delay(100);
                        }
                    }

                    audioStream.Position = 0;
                    audioReader = new Mp3FileReader(audioStream);

                    // Initialize Aux output device
                    var auxOutputDevice = enumerator.GetDevice(_VM.SelectedAuxOutputDevice.Id);
                    using (var auxOutput = new WasapiOut(auxOutputDevice, AudioClientShareMode.Shared, false, 50))
                    {
                        auxOutput.Init(audioReader);
                        auxOutput.Play();
                        while (auxOutput.PlaybackState == PlaybackState.Playing)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                auxOutput.Stop();
                                break;
                            }
                            await Task.Delay(100);
                        }
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
