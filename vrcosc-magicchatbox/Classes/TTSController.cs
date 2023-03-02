using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json.Linq;

namespace vrcosc_magicchatbox.Classes
{
    public class TTSController
    {
        private ViewModel _VM;
        public TTSController(ViewModel vm)
        {
            _VM = vm;
        }

        public async Task PlayTikTokTextAsSpeech(string text, string voice)
        {
            try
            {
                var url = "https://tiktok-tts.weilnet.workers.dev/api/generation";
                var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                var data = "{\"text\":\"" + text + "\",\"voice\":\"" + voice + "\"}";

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
                    using (var output = new WaveOutEvent())
                    {
                        output.DeviceNumber = -1; // set to default audio device
                        output.Init(audioReader);
                        output.Play();
                        while (output.PlaybackState == PlaybackState.Playing)
                        {
                            await Task.Delay(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            
        }


    }
}
