using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class SpeechToTextLanguage : ObservableObject
    {
        public string Language { get; set; }
        public string Code { get; set; }
    }
    public partial class WhisperModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "WhisperModuleSettings.json";

        [ObservableProperty]
        private List<RecordingDeviceInfo> availableDevices;

        [ObservableProperty]
        private int selectedDeviceIndex;

        [ObservableProperty]
        private float noiseGateThreshold = 0.12f;

        [ObservableProperty]
        private bool isNoiseGateOpen = false;

        [ObservableProperty]
        private bool isRecording = false;

        [ObservableProperty]
        private List<SpeechToTextLanguage> speechToTextLanguages;

        [ObservableProperty]
        private SpeechToTextLanguage selectedSpeechToTextLanguage;

        [ObservableProperty]
        private bool translateToCustomLanguage = false;

        [ObservableProperty]
        private int silenceAutoTurnOffDuration = 3000;

        private WhisperModuleSettings()
        {
            RefreshDevices();
            RefreshSpeechToTextLanguages();
        }



        public static WhisperModuleSettings LoadSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
            if (File.Exists(path))
            {
                var settingsJson = File.ReadAllText(path);
                var settings = JsonConvert.DeserializeObject<WhisperModuleSettings>(settingsJson);
                if (settings != null)
                {
                    settings.RefreshDevices(); // Ensure the device list is refreshed upon loading
                    settings.RefreshSpeechToTextLanguages(); // Refresh languages
                    return settings;
                }
            }
            return new WhisperModuleSettings();
        }

        public void SaveSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); // Ensure directory exists
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, settingsJson);
        }

        public void RefreshDevices()
        {
            availableDevices = new List<RecordingDeviceInfo>();

            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                availableDevices.Add(new RecordingDeviceInfo(n, capabilities.ProductName));
            }

            // Optionally, reset the selected device if it's no longer available
            if (selectedDeviceIndex >= availableDevices.Count)
            {
                SelectedDeviceIndex = availableDevices.Any() ? 0 : -1;
            }
        }

        private void RefreshSpeechToTextLanguages()
        {
            var currentSelectedLanguageCode = SelectedSpeechToTextLanguage?.Code;

            SpeechToTextLanguages = new List<SpeechToTextLanguage>
{
    new SpeechToTextLanguage { Language = "English", Code = "en" },
    new SpeechToTextLanguage { Language = "Chinese", Code = "zh" },
    new SpeechToTextLanguage { Language = "Spanish", Code = "es" },
    new SpeechToTextLanguage { Language = "Hindi", Code = "hi" },
    new SpeechToTextLanguage { Language = "Arabic", Code = "ar" },
    new SpeechToTextLanguage { Language = "Portuguese", Code = "pt" },
    new SpeechToTextLanguage { Language = "Bengali", Code = "bn" },
    new SpeechToTextLanguage { Language = "Russian", Code = "ru" },
    new SpeechToTextLanguage { Language = "Japanese", Code = "ja" },
    new SpeechToTextLanguage { Language = "French", Code = "fr" },
    new SpeechToTextLanguage { Language = "German", Code = "de" },
    new SpeechToTextLanguage { Language = "Korean", Code = "ko" },
    new SpeechToTextLanguage { Language = "Italian", Code = "it" },
    new SpeechToTextLanguage { Language = "Turkish", Code = "tr" },
    new SpeechToTextLanguage { Language = "Polish", Code = "pl" },
    new SpeechToTextLanguage { Language = "Dutch", Code = "nl" },
    new SpeechToTextLanguage { Language = "Indonesian", Code = "id" },
    new SpeechToTextLanguage { Language = "Thai", Code = "th" },
    new SpeechToTextLanguage { Language = "Swedish", Code = "sv" },
    new SpeechToTextLanguage { Language = "Danish", Code = "da" },
    new SpeechToTextLanguage { Language = "Norwegian", Code = "no" },
    new SpeechToTextLanguage { Language = "Finnish", Code = "fi" },
    new SpeechToTextLanguage { Language = "Vietnamese", Code = "vi" },
    new SpeechToTextLanguage { Language = "Czech", Code = "cs" },
    new SpeechToTextLanguage { Language = "Greek", Code = "el" },
    new SpeechToTextLanguage { Language = "Romanian", Code = "ro" },
    new SpeechToTextLanguage { Language = "Hungarian", Code = "hu" },
    new SpeechToTextLanguage { Language = "Slovak", Code = "sk" },
    new SpeechToTextLanguage { Language = "Ukrainian", Code = "uk" },
    new SpeechToTextLanguage { Language = "Bulgarian", Code = "bg" },
    new SpeechToTextLanguage { Language = "Croatian", Code = "hr" },
    new SpeechToTextLanguage { Language = "Serbian", Code = "sr" },
    new SpeechToTextLanguage { Language = "Lithuanian", Code = "lt" },
    new SpeechToTextLanguage { Language = "Latvian", Code = "lv" },
    new SpeechToTextLanguage { Language = "Estonian", Code = "et" },
    new SpeechToTextLanguage { Language = "Slovenian", Code = "sl" },
    new SpeechToTextLanguage { Language = "Hebrew", Code = "he" },
    new SpeechToTextLanguage { Language = "Persian", Code = "fa" },
    new SpeechToTextLanguage { Language = "Armenian", Code = "hy" },
    new SpeechToTextLanguage { Language = "Azerbaijani", Code = "az" },
    new SpeechToTextLanguage { Language = "Kazakh", Code = "kk" },
    new SpeechToTextLanguage { Language = "Uzbek", Code = "uz" },
    new SpeechToTextLanguage { Language = "Tajik", Code = "tg" },
    new SpeechToTextLanguage { Language = "Georgian", Code = "ka" },
    new SpeechToTextLanguage { Language = "Mongolian", Code = "mn" },
    new SpeechToTextLanguage { Language = "Afrikaans", Code = "af" },
    new SpeechToTextLanguage { Language = "Swahili", Code = "sw" },
    new SpeechToTextLanguage { Language = "Maori", Code = "mi" },
    new SpeechToTextLanguage { Language = "Nepali", Code = "ne" },
    new SpeechToTextLanguage { Language = "Marathi", Code = "mr" },
    new SpeechToTextLanguage { Language = "Kannada", Code = "kn" },
    new SpeechToTextLanguage { Language = "Tamil", Code = "ta" },
    new SpeechToTextLanguage { Language = "Telugu", Code = "te" },
    new SpeechToTextLanguage { Language = "Malay", Code = "ms" },
    new SpeechToTextLanguage { Language = "Malayalam", Code = "ml" },
    new SpeechToTextLanguage { Language = "Bosnian", Code = "bs" },
    new SpeechToTextLanguage { Language = "Macedonian", Code = "mk" },
    new SpeechToTextLanguage { Language = "Albanian", Code = "sq" },
    new SpeechToTextLanguage { Language = "Filipino", Code = "fil" },
    new SpeechToTextLanguage { Language = "Tagalog", Code = "tl" },
    new SpeechToTextLanguage { Language = "Urdu", Code = "ur" },
    new SpeechToTextLanguage { Language = "Welsh", Code = "cy" },
    new SpeechToTextLanguage { Language = "Icelandic", Code = "is" },
    new SpeechToTextLanguage { Language = "Maltese", Code = "mt" },
    new SpeechToTextLanguage { Language = "Galician", Code = "gl" },
    new SpeechToTextLanguage { Language = "Belarusian", Code = "be" },
    new SpeechToTextLanguage { Language = "Catalan", Code = "ca" },
};

            // Check if the previously selected language still exists in the list
            var languageExists = SpeechToTextLanguages.Any(lang => lang.Code == currentSelectedLanguageCode);

            if (languageExists)
            {
                // If the previously selected language exists, select it again
                SelectedSpeechToTextLanguage = SpeechToTextLanguages.FirstOrDefault(lang => lang.Code == currentSelectedLanguageCode);
            }
            else
            {
                // If it doesn't exist or if there was no selection, default to the first language in the list
                SelectedSpeechToTextLanguage = SpeechToTextLanguages.FirstOrDefault();
            }

            OnPropertyChanged(nameof(SelectedSpeechToTextLanguage));
        }
    }
    public class RecordingDeviceInfo
    {
        public int DeviceIndex { get; }
        public string DeviceName { get; }

        public RecordingDeviceInfo(int deviceIndex, string deviceName)
        {
            DeviceIndex = deviceIndex;
            DeviceName = deviceName;
        }

        public override string ToString()
        {
            return $"{DeviceName} (Index: {DeviceIndex})";
        }
    }
    public partial class WhisperModule : ObservableObject
    {
        private WaveInEvent waveIn;
        private MemoryStream audioStream = new MemoryStream();
        private DateTime lastSoundTimestamp = DateTime.Now;
        private bool isCurrentlySpeaking = false;
        private DateTime speakingStartedTimestamp = DateTime.Now;
        private bool isProcessingShortPause = false;

        public event Action<string> TranscriptionReceived;

        [ObservableProperty]
        public WhisperModuleSettings settings;

        public WhisperModule()
        {
            settings = WhisperModuleSettings.LoadSettings();
            settings.PropertyChanged += Settings_PropertyChanged;
            InitializeWaveIn();
        }

        public void OnApplicationClosing()
        {
            Settings.SaveSettings();
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.SelectedDeviceIndex))
            {
                StopRecording();
                InitializeWaveIn();
            }
        }

        private void InitializeWaveIn()
        {
            waveIn?.Dispose(); // Dispose any existing instance

            if (settings.SelectedDeviceIndex == -1)
            {
                UpdateUI("No valid audio input device selected.", false);
                // Consider handling this scenario without throwing an exception,
                // perhaps by disabling recording functionality until a valid device is selected.
                return;
            }

            waveIn = new WaveInEvent
            {
                DeviceNumber = settings.SelectedDeviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1), // Suitable for voice recognition
                BufferMilliseconds = 300 // Adjust for responsiveness vs performance
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
        }


        public void StartRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }
            if (waveIn == null)
            {
                UpdateUI("Starting recording failed: Device not initialized.", false);
                return;
            }
            if(settings.IsRecording) 
            {
                UpdateUI("Already recording.", false);
                return;
            }
            UpdateUI("Ready to speak?", true);
            waveIn.StartRecording();
            //PlaySound("start.wav");
            settings.IsRecording = true;
        }

        public void StopRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
            }
            if (waveIn == null)
            {
                UpdateUI("Stopping recording failed: Device not initialized.", false);
                return;
            }
            if(settings.IsRecording == false) 
            {
                UpdateUI("Not currently recording.", false);
                return;
            }
            waveIn.StopRecording();
            settings.IsRecording = false;
            UpdateUI("Recording stopped. Processing last audio...", false);
            if (audioStream.Length > 0)
            {
                ProcessAudioStreamAsync(audioStream);
            }
            audioStream = new MemoryStream();
        }

        //private void PlaySound(string soundFileName)
        //{
        //    var assembly = Assembly.GetExecutingAssembly();
        //    string resourceName = assembly.GetName().Name + ".Sounds." + soundFileName;

        //    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        //    {
        //        if (stream == null)
        //        {
        //            throw new InvalidOperationException("Could not find resource sound file: " + resourceName);
        //        }

        //        SoundPlayer player = new SoundPlayer(stream);
        //        player.Play();
        //    }
        //}



        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            float maxAmplitude = CalculateMaxAmplitude(e.Buffer, e.BytesRecorded);
            bool isLoudEnough = maxAmplitude > Settings.NoiseGateThreshold;
            Settings.IsNoiseGateOpen = isLoudEnough;

            if (isLoudEnough)
            {
                HandleSpeakingState(e);
            }
            else
            {
                ProcessSilenceOrShortPause();
            }
        }

        private void HandleSpeakingState(WaveInEventArgs e)
        {
            if (!isCurrentlySpeaking)
            {
                speakingStartedTimestamp = DateTime.Now;
                isCurrentlySpeaking = true;
                if (audioStream.Length > 0)
                {
                    // If there's residual audio from a previous pause, start fresh without losing the continuity
                    ProcessAudioStreamAsync(audioStream, partial: true);
                    audioStream = new MemoryStream();
                }
            }

            audioStream.Write(e.Buffer, 0, e.BytesRecorded);
            lastSoundTimestamp = DateTime.Now;
            var speakingDuration = (DateTime.Now - speakingStartedTimestamp).TotalSeconds;
            UpdateUI($"Speaking... Duration: {speakingDuration:0.0}s", true);
        }

        private void ProcessSilenceOrShortPause()
        {
            var silenceDuration = DateTime.Now.Subtract(lastSoundTimestamp).TotalMilliseconds;

            if (!isCurrentlySpeaking || silenceDuration < 500) return; // Ignore very short silences or if not speaking

            if (silenceDuration <= Settings.SilenceAutoTurnOffDuration)
            {
                if (!isProcessingShortPause)
                {
                    // Handle short pause: Transcribe partial speech without stopping the recording session
                    isProcessingShortPause = true;
                    ProcessAudioStreamAsync(audioStream, partial: true);
                    audioStream = new MemoryStream(); // Prepare for more speech, ensuring continuity
                    Task.Delay(500).ContinueWith(_ => isProcessingShortPause = false);
                }
            }
            else if (silenceDuration > Settings.SilenceAutoTurnOffDuration && isCurrentlySpeaking)
            {
                // Long silence detected, auto-disable the STT session by stopping recording
                isCurrentlySpeaking = false;
                StopRecording(); // Auto-disable the STT session due to prolonged silence
                audioStream = new MemoryStream(); // Reset for a new session
                UpdateUI($"Silence detected for more than {Settings.SilenceAutoTurnOffDuration / 1000.0} seconds, auto-disabling STT session...", false);
            }
        }






        private async void UpdateUI(string message, bool isVisible)
        {
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabelTxt = message;
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = isVisible;

            if (!isVisible)
            {
                ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = true;
                await Task.Delay(2500); 
                App.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = false;
                });
            }
        }


        private float CalculateMaxAmplitude(byte[] buffer, int bytesRecorded)
        {
            short[] samples = new short[bytesRecorded / 2];
            Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);
            return samples.Max(sample => Math.Abs(sample / 32768f));
        }

        private async Task ProcessAudioStreamAsync(MemoryStream stream, bool partial = false)
        {
            if (stream.Length == 0) return;

            stream.Position = 0;
            UpdateUI(partial ? "Transcribing part of your speech..." : "Transcribing with OpenAI...", true);
            string transcription = await TranscribeAudioAsync(stream);
            if (!string.IsNullOrEmpty(transcription))
            {
                TranscriptionReceived?.Invoke(transcription);
                UpdateUI("Transcription complete.", false);
            }
            else
            {
                UpdateUI("Error transcribing audio.", false);
            }

            if (!partial)
            {
                // Reset the stream only if processing the final segment
                audioStream = new MemoryStream();
            }
        }



        private async Task<string> TranscribeAudioAsync(Stream audioStream)
        {
            string tempFilePath = Path.GetTempFileName() + ".wav"; // Adding the .wav extension is crucial
            try
            {
                using (var writer = new WaveFileWriter(tempFilePath, waveIn.WaveFormat))
                {
                    await audioStream.CopyToAsync(writer);
                }

                var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint.CreateTranscriptionAsync(new AudioTranscriptionRequest(tempFilePath, language: Settings.TranslateToCustomLanguage ? Settings.SelectedSpeechToTextLanguage.Code : null));


                return response;
            }
            catch (Exception ex)
            {
                UpdateUI($"Error transcription: {ex.Message}", false);
                return null;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }


        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine($"Recording stopped due to error: {e.Exception.Message}");
            }
            else
            {
                Console.WriteLine("Recording stopped successfully.");
            }
        }

        public void Dispose()
        {
            waveIn?.Dispose();
            audioStream?.Dispose();
            UpdateUI("Disposed resources.", false);
        }
    }

}
