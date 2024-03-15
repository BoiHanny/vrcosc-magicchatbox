using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using OpenAI.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using vrcosc_magicchatbox.ViewModels;
using Newtonsoft.Json;
using System.Media;
using System.Reflection;
using System.Windows;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class WhisperModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "WhisperModuleSettings.json";

        [ObservableProperty]
        private List<RecordingDeviceInfo> availableDevices;

        [ObservableProperty]
        private int selectedDeviceIndex;

        [ObservableProperty]
        private float noiseGateThreshold = 0.20f;

        [ObservableProperty]
        private bool isNoiseGateOpen = false;

        [ObservableProperty]
        private bool isRecording = false;

        [ObservableProperty]
        private List<string> speechToTextLanguages = new List<string>();

        [ObservableProperty]
        private string selectedSpeechToTextLanguage;

        [ObservableProperty]
        private bool autoLanguageDetection = true;

        [ObservableProperty]
        private int silenceAutoTurnOffDuration = 3000;

        public WhisperModuleSettings()
        {
            RefreshDevices();
            RefreshSpeechToTextLanguages();
        }

        public void SaveSettings()
        {
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsFileName, settingsJson);
        }

        public static WhisperModuleSettings LoadSettings()
        {
            if (File.Exists(SettingsFileName))
            {
                var settingsJson = File.ReadAllText(SettingsFileName);
                return JsonConvert.DeserializeObject<WhisperModuleSettings>(settingsJson);
            }

            return new WhisperModuleSettings();
        }

        private void RefreshSpeechToTextLanguages()
        {
            // Ordered by a hypothetical "most commonly used" metric, adjust as needed
            SpeechToTextLanguages = new List<string>
        {
            "English",
            "Chinese",
            "Spanish",
            "Hindi",
            "Arabic",
            "Portuguese",
            "Bengali",
            "Russian",
            "Japanese",
            "French",
            "German",
            "Korean",
            "Italian",
            "Turkish",
            "Polish",
            "Dutch",
            "Indonesian",
            "Thai",
            "Swedish",
            "Danish",
            "Norwegian",
            "Finnish",
            "Vietnamese",
            "Czech",
            "Greek",
            "Romanian",
            "Hungarian",
            "Slovak",
            "Ukrainian",
            "Bulgarian",
            "Croatian",
            "Serbian",
            "Lithuanian",
            "Latvian",
            "Estonian",
            "Slovenian",
            "Hebrew",
            "Persian",
            "Armenian",
            "Azerbaijani",
            "Kazakh",
            "Uzbek",
            "Tajik",
            "Georgian",
            "Mongolian",
            "Afrikaans",
            "Swahili",
            "Maori",
            "Nepali",
            "Marathi",
            "Kannada",
            "Tamil",
            "Telugu",
            "Malay",
            "Malayalam",
            "Bosnian",
            "Macedonian",
            "Albanian",
            "Filipino",
            "Tagalog",
            "Urdu",
            "Welsh",
            "Icelandic",
            "Maltese",
            "Galician",
            "Belarusian",
            "Catalan"
        };

            // Assuming SelectedSpeechToTextLanguage should be set to the most common language initially
            if (string.IsNullOrWhiteSpace(SelectedSpeechToTextLanguage))
                SelectedSpeechToTextLanguage = SpeechToTextLanguages.FirstOrDefault();
        }

        public string GetSelectedDeviceName()
        {
            if (SelectedDeviceIndex >= 0 && SelectedDeviceIndex < AvailableDevices.Count)
            {
                return AvailableDevices[SelectedDeviceIndex].DeviceName;
            }
            else
            {
                return "No device selected";
            }
        }

        



        public void RefreshDevices()
        {
            AvailableDevices = Enumerable.Range(0, WaveIn.DeviceCount)
                .Select(n => new RecordingDeviceInfo(n, WaveIn.GetCapabilities(n).ProductName))
                .ToList();
            SelectedDeviceIndex = AvailableDevices.Any() ? 0 : -1;
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
        public WhisperModuleSettings settings = new WhisperModuleSettings();

        public WhisperModule()
        {
            Settings = WhisperModuleSettings.LoadSettings();
            Settings.PropertyChanged += Settings_PropertyChanged;
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
                if (!isCurrentlySpeaking)
                {
                    speakingStartedTimestamp = DateTime.Now;
                    isCurrentlySpeaking = true;
                }

                var speakingDuration = (DateTime.Now - speakingStartedTimestamp).TotalSeconds;
                UpdateUI($"Speaking detected, recording... (Duration: {speakingDuration:0.0}s)", true);
                audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                lastSoundTimestamp = DateTime.Now;
            }
            else if (isCurrentlySpeaking)
            {
                var silenceDuration = DateTime.Now.Subtract(lastSoundTimestamp).TotalMilliseconds;

                if (silenceDuration > 500 && silenceDuration <= Settings.SilenceAutoTurnOffDuration)
                {
                    if (!isProcessingShortPause)
                    {
                        isProcessingShortPause = true;
                        // Offload to a background task since we can't await in this event handler
                        Task.Run(() => ProcessShortPauseAsync()).ContinueWith(_ =>
                        {
                            // Use Dispatcher.Invoke to ensure that the following actions are performed on the UI thread.
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // Actions to take after processing the short pause, ensuring thread safety for UI operations
                                isProcessingShortPause = false;
                                // Any other UI updates or state changes that need to be made safely on the UI thread
                            });
                        });

                    }
                }
                else if (silenceDuration > Settings.SilenceAutoTurnOffDuration)
                {
                    isCurrentlySpeaking = false;
                    UpdateUI($"Silence detected for more than {Settings.SilenceAutoTurnOffDuration / 1000.0} seconds, stopping recording...", true);
                    StopRecording();
                }
            }
        }

        private async Task ProcessShortPauseAsync()
        {
            await ProcessAudioStreamAsync(audioStream);
            // Ensure the continuation logic here is thread-safe, especially if updating the UI
            App.Current.Dispatcher.Invoke(() =>
            {
                isProcessingShortPause = false;
                audioStream = new MemoryStream(); // Reset for new data
                lastSoundTimestamp = DateTime.Now; // Reset timestamp
                                                   // Optionally update the UI or reset flags
            });
        }





        private async void UpdateUI(string message, bool isVisible)
        {
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabelTxt = message;
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = isVisible;

            if (!isVisible)
            {
                await Task.Delay(1200); 
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

        private async Task ProcessAudioStreamAsync(MemoryStream stream)
        {
            if (stream.Length == 0)
            {
                UpdateUI("No audio detected.", false);
                return;
            }

            stream.Position = 0;
            UpdateUI("Transcribing with OpenAI...", true);
            string transcription = await TranscribeAudioAsync(stream);
            if (transcription != null) {
                TranscriptionReceived?.Invoke(transcription);
                UpdateUI("Transcription complete.", false);
            }
            else
                {
                UpdateUI("Error transcribing audio.", false);
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

                var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint.CreateTranscriptionAsync(new AudioTranscriptionRequest(tempFilePath, language: Settings.AutoLanguageDetection?null:Settings.SelectedSpeechToTextLanguage));

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
