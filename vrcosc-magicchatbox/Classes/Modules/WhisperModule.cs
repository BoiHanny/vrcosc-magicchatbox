using CommunityToolkit.Mvvm.ComponentModel;
using NAudio.Wave;
using Newtonsoft.Json;
using OpenAI.Audio;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.ViewModels;

namespace vrcosc_magicchatbox.Classes.Modules
{
    /// <summary>
    /// Represents a language supported by the Speech-to-Text module.
    /// </summary>
    public partial class SpeechToTextLanguage : ObservableObject
    {
        public string Code { get; set; }
        public string Language { get; set; }
    }

    /// <summary>
    /// Holds settings for the Whisper (STT) module.
    /// </summary>
    public partial class WhisperModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "WhisperModuleSettings.json";

        [ObservableProperty]
        private List<RecordingDeviceInfo> availableDevices;

        [ObservableProperty]
        private IntelliGPTModel speechToTextModel = IntelliGPTModel.whisper1;

        [ObservableProperty]
        private bool isNoiseGateOpen = false;

        [ObservableProperty]
        private bool isRecording = false;

        [ObservableProperty]
        private float noiseGateThreshold = 0.12f;

        [ObservableProperty]
        private bool sendAftersilence = true;

        [ObservableProperty]
        private int selectedDeviceIndex;

        [ObservableProperty]
        private SpeechToTextLanguage selectedSpeechToTextLanguage;

        [ObservableProperty]
        private int silenceAutoTurnOffDuration = 3000;

        [ObservableProperty]
        private List<SpeechToTextLanguage> speechToTextLanguages;

        [ObservableProperty]
        private bool translateToCustomLanguage = false;

        /// <summary>
        /// Shows only models with ModelType == "STT" in IntelliGPTModel.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<IntelliGPTModel> AvailableSTTModels =>
            Enum.GetValues(typeof(IntelliGPTModel))
                .Cast<IntelliGPTModel>()
                .Where(m => WhisperModule.GetModelType(m) == "STT");

        // Private constructor (use LoadSettings).
        private WhisperModuleSettings()
        {
            RefreshDevices();
            RefreshSpeechToTextLanguages();
        }

        /// <summary>
        /// Refreshes the list of supported languages in the UI and preserves the current selection if still valid.
        /// </summary>
        private void RefreshSpeechToTextLanguages()
        {
            var currentSelectedLanguageCode = SelectedSpeechToTextLanguage?.Code;

            // This set includes most major languages for the speech-to-text functionality.
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

            bool languageExists = SpeechToTextLanguages.Any(lang => lang.Code == currentSelectedLanguageCode);
            SelectedSpeechToTextLanguage = languageExists
                ? SpeechToTextLanguages.First(lang => lang.Code == currentSelectedLanguageCode)
                : SpeechToTextLanguages.FirstOrDefault();

            OnPropertyChanged(nameof(SelectedSpeechToTextLanguage));
        }

        /// <summary>
        /// Loads settings from disk, handling empty or corrupted JSON gracefully.
        /// </summary>
        public static WhisperModuleSettings LoadSettings()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsFolder = Path.Combine(appDataPath, "Vrcosc-MagicChatbox");
            var path = Path.Combine(settingsFolder, SettingsFileName);

            if (File.Exists(path))
            {
                string settingsJson = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("Settings file is empty or corrupted.");
                    return new WhisperModuleSettings();
                }

                try
                {
                    var settings = JsonConvert.DeserializeObject<WhisperModuleSettings>(settingsJson);
                    if (settings != null)
                    {
                        settings.RefreshDevices();
                        settings.RefreshSpeechToTextLanguages();
                        return settings;
                    }
                    else
                    {
                        Logging.WriteInfo("Deserialization of settings failed.");
                        return new WhisperModuleSettings();
                    }
                }
                catch (JsonException ex)
                {
                    Logging.WriteInfo($"Error parsing settings JSON: {ex.Message}");
                    return new WhisperModuleSettings();
                }
            }
            else
            {
                Logging.WriteInfo("Settings file not found, returning new instance.");
                return new WhisperModuleSettings();
            }
        }

        /// <summary>
        /// Refreshes the list of recording devices on the system.
        /// </summary>
        public void RefreshDevices()
        {
            availableDevices = new List<RecordingDeviceInfo>();
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var caps = WaveIn.GetCapabilities(n);
                availableDevices.Add(new RecordingDeviceInfo(n, caps.ProductName));
            }

            if (selectedDeviceIndex >= availableDevices.Count)
            {
                SelectedDeviceIndex = availableDevices.Any() ? 0 : -1;
            }
        }

        /// <summary>
        /// Saves the STT settings to disk.
        /// </summary>
        public void SaveSettings()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsFolder = Path.Combine(appDataPath, "Vrcosc-MagicChatbox");
            var path = Path.Combine(settingsFolder, SettingsFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, settingsJson);
        }
    }

    /// <summary>
    /// Holds device index and name for an audio input device.
    /// </summary>
    public class RecordingDeviceInfo
    {
        public RecordingDeviceInfo(int deviceIndex, string deviceName)
        {
            DeviceIndex = deviceIndex;
            DeviceName = deviceName;
        }

        public override string ToString() => $"{DeviceName} (Index: {DeviceIndex})";

        public int DeviceIndex { get; }
        public string DeviceName { get; }
    }

    /// <summary>
    /// Manages audio recording, detecting speech, and transcribing with OpenAI.
    /// </summary>
    public partial class WhisperModule : ObservableObject, IDisposable
    {
        // Thread-safe audio stream plus lock.
        private readonly MemoryStream audioStream = new MemoryStream();
        private readonly object _audioStreamLock = new object();

        // Manages transcription cancellation.
        private CancellationTokenSource _transcriptionCancellationTokenSource = new CancellationTokenSource();

        // Speech detection states.
        private bool isCurrentlySpeaking;
        private bool isProcessingShortPause;
        private DateTime lastSoundTimestamp = DateTime.Now;
        private TimeSpan speakingDuration;
        private DateTime speakingStartedTimestamp;

        private WaveInEvent waveIn;

        [ObservableProperty]
        private WhisperModuleSettings settings;

        /// <summary>
        /// Raised when transcription text is ready.
        /// </summary>
        public event Action<string> TranscriptionReceived;

        /// <summary>
        /// Raised after a final chunk is transcribed, if auto-sending is enabled.
        /// </summary>
        public event Action SentChatMessage;

        /// <summary>
        /// Constructor: load settings, subscribe to changes, set up wave device.
        /// </summary>
        public WhisperModule()
        {
            settings = WhisperModuleSettings.LoadSettings();
            settings.PropertyChanged += Settings_PropertyChanged;
            InitializeWaveIn();
        }

        /// <summary>
        /// Calculates the maximum amplitude from raw audio data, normalized 0..1.
        /// </summary>
        private float CalculateMaxAmplitude(byte[] buffer, int bytesRecorded)
        {
            short[] samples = new short[bytesRecorded / 2];
            Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);
            return samples.Max(sample => Math.Abs(sample / 32768f));
        }

        /// <summary>
        /// Enters the "speaking" state if not already speaking, and accumulates audio.
        /// </summary>
        private void HandleSpeakingState(WaveInEventArgs e)
        {
            if (!isCurrentlySpeaking)
            {
                speakingStartedTimestamp = DateTime.Now;
                isCurrentlySpeaking = true;
                speakingDuration = TimeSpan.Zero;

                // If the buffer already has data, transcribe partially.
                if (GetAudioStreamLength() > 0)
                {
                    _ = ProcessAudioStreamAsync(partial: true);
                }
            }

            lock (_audioStreamLock)
            {
                audioStream.Write(e.Buffer, 0, e.BytesRecorded);
            }

            lastSoundTimestamp = DateTime.Now;
            UpdateSpeakingDuration();

            UpdateUI($"Speaking... {speakingDuration.TotalSeconds:0.0}s", true);
        }

        /// <summary>
        /// Sets up the WaveInEvent using the selected device index.
        /// </summary>
        private void InitializeWaveIn()
        {
            waveIn?.Dispose();  // Clean up existing device if any.

            if (settings.SelectedDeviceIndex == -1)
            {
                UpdateUI("No valid audio input device selected.", false);
                return;
            }

            waveIn = new WaveInEvent
            {
                DeviceNumber = settings.SelectedDeviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1), // best for speech
                BufferMilliseconds = 350  // shorter buffer => faster partial updates
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
        }

        /// <summary>
        /// Receives audio from the wave device and checks amplitude vs noise gate.
        /// </summary>
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            float maxAmplitude = CalculateMaxAmplitude(e.Buffer, e.BytesRecorded);
            bool isLoudEnough = maxAmplitude > settings.NoiseGateThreshold;
            settings.IsNoiseGateOpen = isLoudEnough;

            if (isLoudEnough)
            {
                HandleSpeakingState(e);
            }
            else
            {
                ProcessSilenceOrShortPause();
            }
        }

        /// <summary>
        /// Called when the recording stops (manually or due to an error).
        /// </summary>
        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Logging.WriteInfo($"Recording stopped due to error: {e.Exception.Message}");
            }
            else
            {
                Logging.WriteInfo("Recording stopped successfully.");
            }
        }

        /// <summary>
        /// Sends the current audio buffer to OpenAI for transcription.
        /// </summary>
        private async Task ProcessAudioStreamAsync(bool partial)
        {
            byte[] audioData;
            lock (_audioStreamLock)
            {
                if (audioStream.Length == 0)
                    return;

                audioData = audioStream.ToArray();
                ResetAudioStream();
            }

            using (var localCopyStream = new MemoryStream(audioData))
            {
                UpdateUI(
                    partial ? "Transcribing partial audio..." : "Transcribing final audio...",
                    showMessage: true
                );

                // Cancel any older transcription in progress.
                _transcriptionCancellationTokenSource.Cancel();
                _transcriptionCancellationTokenSource.Dispose();
                _transcriptionCancellationTokenSource = new CancellationTokenSource();

                string transcription = await TranscribeAudioAsync(localCopyStream, _transcriptionCancellationTokenSource.Token);
                if (!string.IsNullOrEmpty(transcription))
                {
                    TranscriptionReceived?.Invoke(transcription);
                    UpdateUI("Transcription done.", false);
                }
                else
                {
                    UpdateUI("Transcription error or canceled.", false);
                }
            }
        }

        /// <summary>
        /// Checks how long we have been silent; triggers partial or final stop accordingly.
        /// </summary>
        private void ProcessSilenceOrShortPause()
        {
            double silenceMs = (DateTime.Now - lastSoundTimestamp).TotalMilliseconds;
            if (!isCurrentlySpeaking || silenceMs < 500)
                return;

            // Short pause => partial transcription, continue capturing.
            if (silenceMs <= settings.SilenceAutoTurnOffDuration)
            {
                if (!isProcessingShortPause)
                {
                    isProcessingShortPause = true;
                    _ = ProcessAudioStreamAsync(true);
                    speakingStartedTimestamp = DateTime.Now;
                    speakingDuration = TimeSpan.Zero;

                    Task.Delay(500).ContinueWith(_ => isProcessingShortPause = false);
                }
            }
            // If silence is too long => auto-stop.
            else
            {
                isCurrentlySpeaking = false;
                StopRecording(); // final chunk processed in StopRecording
                UpdateUI($"Silence > {settings.SilenceAutoTurnOffDuration / 1000.0}s, stopping STT...", false);
            }
        }

        /// <summary>
        /// If user changes the device in settings, re-initialize the waveIn.
        /// </summary>
        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(settings.SelectedDeviceIndex))
            {
                StopRecording();
                InitializeWaveIn();
            }
        }

        /// <summary>
        /// Actual transcription call to OpenAI, returning the recognized text if successful.
        /// </summary>
        private async Task<string> TranscribeAudioAsync(Stream waveFileStream, CancellationToken cancellationToken)
        {
            // We'll store the wave data on disk in a temporary file for the request.
            string tempFilePath = Path.GetTempFileName() + ".wav";
            try
            {
                using (var writer = new WaveFileWriter(tempFilePath, waveIn.WaveFormat))
                {
                    await waveFileStream.CopyToAsync(writer, 81920, cancellationToken);
                    writer.Flush();
                }

                string modelName = GetModelDescription(Settings.SpeechToTextModel);
                string languageCode = Settings.TranslateToCustomLanguage
                    ? Settings.SelectedSpeechToTextLanguage?.Code
                    : null;

                var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint
                    .CreateTranscriptionTextAsync(
                        new AudioTranscriptionRequest(
                            audioPath: tempFilePath,
                            model: modelName,
                            language: languageCode
                        ),
                        cancellationToken
                    );

                return response;
            }
            catch (OperationCanceledException)
            {
                Logging.WriteInfo("Transcription canceled by user or system.");
                return null;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Transcription error: {ex}");
                UpdateUI($"Transcription error: {ex.Message}", false);
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

        /// <summary>
        /// Updates how long we have been speaking.
        /// </summary>
        private void UpdateSpeakingDuration()
        {
            if (isCurrentlySpeaking)
            {
                speakingDuration = DateTime.Now - speakingStartedTimestamp;
            }
        }

        /// <summary>
        /// Clears the shared audio buffer in a thread-safe way.
        /// </summary>
        private void ResetAudioStream()
        {
            lock (_audioStreamLock)
            {
                audioStream.SetLength(0);
                audioStream.Position = 0;
            }
        }

        /// <summary>
        /// Gets the length of the buffer (in bytes) in a thread-safe way.
        /// </summary>
        private long GetAudioStreamLength()
        {
            lock (_audioStreamLock)
            {
                return audioStream.Length;
            }
        }

        /// <summary>
        /// Temporarily shows or hides a status message in your UI.
        /// </summary>
        private async void UpdateUI(string message, bool showMessage)
        {
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabelTxt = message;
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = showMessage;

            if (!showMessage)
            {
                ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = true;
                await Task.Delay(2500);
                App.Current?.Dispatcher?.Invoke(() =>
                {
                    ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = false;
                });
            }
        }

        /// <summary>
        /// Starts capturing audio from the selected device, if valid. Cancels if OpenAI uninitialized.
        /// </summary>
        public void StartRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                UpdateUI("OpenAI not initialized. Please check settings.", false);
                return;
            }

            if (waveIn == null)
            {
                UpdateUI("No audio device is ready.", false);
                return;
            }

            if (settings.IsRecording)
            {
                UpdateUI("Already recording.", false);
                return;
            }

            try
            {
                waveIn.StartRecording();
                settings.IsRecording = true;
                UpdateUI("Recording started. Speak now...", true);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StartRecording error: {ex}");
                UpdateUI($"Error starting recording: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Stops audio capture and processes any remaining audio data, then triggers SentChatMessage as needed.
        /// </summary>
        public void StopRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                UpdateUI("OpenAI not initialized. Please check settings.", false);
                return;
            }

            if (waveIn == null)
            {
                UpdateUI("StopRecording failed: no audio device.", false);
                return;
            }

            if (!settings.IsRecording)
            {
                UpdateUI("Not currently recording.", false);
                return;
            }

            try
            {
                waveIn.StopRecording();
                settings.IsRecording = false;
                UpdateUI("Stopped. Processing final chunk...", false);

                // If leftover data is present, do final transcription. Then, optionally auto-send.
                if (GetAudioStreamLength() > 0)
                {
                    var finalTask = ProcessAudioStreamAsync(partial: false);
                    finalTask.ContinueWith(t =>
                    {
                        // If the transcription task ran successfully, raise SentChatMessage if enabled.
                        if (!t.IsFaulted && !t.IsCanceled && settings.SendAftersilence)
                        {
                            SentChatMessage?.Invoke();
                        }
                    });
                }
                else
                {
                    // If no leftover data, we can still auto-send if you want. 
                    // Usually no data => no transcription => no reason to send. 
                    if (settings.SendAftersilence)
                    {
                        SentChatMessage?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StopRecording error: {ex}");
                UpdateUI($"Error stopping recording: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Dispose waveIn, audio stream, and transcription tasks.
        /// </summary>
        public void Dispose()
        {
            waveIn?.Dispose();
            audioStream?.Dispose();

            _transcriptionCancellationTokenSource?.Cancel();
            _transcriptionCancellationTokenSource?.Dispose();

            UpdateUI("Disposed resources.", false);
        }

        /// <summary>
        /// Saves current settings when closing the application.
        /// </summary>
        public void OnApplicationClosing()
        {
            settings.SaveSettings();
        }

        #region Helper Methods for Model Selection

        private static string GetModelDescription(IntelliGPTModel model)
        {
            var type = model.GetType();
            var memberInfo = type.GetMember(model.ToString());
            if (memberInfo.Length > 0)
            {
                var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attrs.Length > 0)
                    return ((DescriptionAttribute)attrs[0]).Description;
            }
            return model.ToString();
        }

        internal static string GetModelType(IntelliGPTModel model)
        {
            var type = model.GetType();
            var memberInfo = type.GetMember(model.ToString());
            if (memberInfo.Length > 0)
            {
                var attrs = memberInfo[0].GetCustomAttributes(typeof(ModelTypeInfoAttribute), false);
                if (attrs.Length > 0)
                    return ((ModelTypeInfoAttribute)attrs[0]).ModelType;
            }
            return "Unknown";
        }

        #endregion
    }
}
