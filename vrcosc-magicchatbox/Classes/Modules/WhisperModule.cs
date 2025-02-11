using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using Newtonsoft.Json;
using vrcosc_magicchatbox.ViewModels;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

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

        // Private constructor to enforce use of LoadSettings method.
        private WhisperModuleSettings()
        {
            RefreshDevices();
            RefreshSpeechToTextLanguages();
        }

        /// <summary>
        /// Refreshes the list of available languages.
        /// </summary>
        private void RefreshSpeechToTextLanguages()
        {
            // Save the current selection so it can be re-applied after the list is refreshed.
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

            // Restore the previous selection if it still exists.
            var languageExists = SpeechToTextLanguages.Any(lang => lang.Code == currentSelectedLanguageCode);
            SelectedSpeechToTextLanguage = languageExists
                ? SpeechToTextLanguages.First(lang => lang.Code == currentSelectedLanguageCode)
                : SpeechToTextLanguages.FirstOrDefault();

            OnPropertyChanged(nameof(SelectedSpeechToTextLanguage));
        }

        /// <summary>
        /// Loads settings from disk, handling cases of empty or corrupted JSON.
        /// </summary>
        public static WhisperModuleSettings LoadSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
            if (File.Exists(path))
            {
                var settingsJson = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(settingsJson) || settingsJson.All(c => c == '\0'))
                {
                    Logging.WriteInfo("The settings JSON file is empty or corrupted.");
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
                        Logging.WriteInfo("Failed to deserialize the settings JSON.");
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
                Logging.WriteInfo("Settings file does not exist, returning new settings instance.");
                return new WhisperModuleSettings();
            }
        }

        /// <summary>
        /// Refreshes the list of available recording devices.
        /// </summary>
        public void RefreshDevices()
        {
            availableDevices = new List<RecordingDeviceInfo>();
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                availableDevices.Add(new RecordingDeviceInfo(n, capabilities.ProductName));
            }
            // If the currently selected device is no longer available, reset the selection.
            if (selectedDeviceIndex >= availableDevices.Count)
            {
                SelectedDeviceIndex = availableDevices.Any() ? 0 : -1;
            }
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public void SaveSettings()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vrcosc-MagicChatbox", SettingsFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); // Ensure directory exists
            var settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, settingsJson);
        }
    }

    /// <summary>
    /// Simple class representing a recording device.
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
    /// Main module that handles recording, detecting speech, and transcribing audio using OpenAI.
    /// </summary>
    public partial class WhisperModule : ObservableObject, IDisposable
    {
        // Shared audio buffer and associated lock for thread safety.
        private MemoryStream audioStream = new MemoryStream();
        private readonly object _audioStreamLock = new object();

        // Cancellation source for transcription tasks (optional).
        private CancellationTokenSource _transcriptionCancellationTokenSource = new CancellationTokenSource();

        // State variables for speaking detection.
        private bool isCurrentlySpeaking = false;
        private bool isProcessingShortPause = false;
        private DateTime lastSoundTimestamp = DateTime.Now;
        private TimeSpan speakingDuration = TimeSpan.Zero;
        private DateTime speakingStartedTimestamp = DateTime.Now;
        private WaveInEvent waveIn;

        [ObservableProperty]
        public WhisperModuleSettings settings;

        public WhisperModule()
        {
            settings = WhisperModuleSettings.LoadSettings();
            Settings.PropertyChanged += Settings_PropertyChanged;
            InitializeWaveIn();
        }

        /// <summary>
        /// Event raised when a transcription is received.
        /// </summary>
        public event Action<string> TranscriptionReceived;

        public event Action SentChatMessage;

        /// <summary>
        /// Calculate the maximum amplitude (normalized) from the provided audio buffer.
        /// </summary>
        /// <param name="buffer">Audio data</param>
        /// <param name="bytesRecorded">Number of bytes recorded</param>
        /// <returns>Maximum amplitude value</returns>
        private float CalculateMaxAmplitude(byte[] buffer, int bytesRecorded)
        {
            // Convert 16-bit samples to normalized float values.
            short[] samples = new short[bytesRecorded / 2];
            Buffer.BlockCopy(buffer, 0, samples, 0, bytesRecorded);
            return samples.Max(sample => Math.Abs(sample / 32768f));
        }

        /// <summary>
        /// Handles state when speaking is detected.
        /// </summary>
        /// <param name="e">Audio event args</param>
        private void HandleSpeakingState(WaveInEventArgs e)
        {
            if (!isCurrentlySpeaking)
            {
                // Start of a new speech segment.
                speakingStartedTimestamp = DateTime.Now;
                isCurrentlySpeaking = true;
                speakingDuration = TimeSpan.Zero;

                // If there is buffered audio from previous speech, process it as a partial transcription.
                if (GetAudioStreamLength() > 0)
                {
                    _ = ProcessAudioStreamAsync(partial: true);
                }
            }

            // Safely append new audio data.
            lock (_audioStreamLock)
            {
                audioStream.Write(e.Buffer, 0, e.BytesRecorded);
            }
            lastSoundTimestamp = DateTime.Now;
            UpdateSpeakingDuration();
            UpdateUI($"Speaking... Duration: {speakingDuration.TotalSeconds:0.0}s", true);
        }

        /// <summary>
        /// Initializes the WaveInEvent instance using the selected recording device.
        /// </summary>
        private void InitializeWaveIn()
        {
            waveIn?.Dispose(); // Dispose any existing instance

            if (Settings.SelectedDeviceIndex == -1)
            {
                UpdateUI("No valid audio input device selected.", false);
                // Disable recording functionality until a valid device is selected.
                return;
            }

            waveIn = new WaveInEvent
            {
                DeviceNumber = Settings.SelectedDeviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1), // Suitable for voice recognition
                BufferMilliseconds = 450 // Balance responsiveness and performance
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
        }

        /// <summary>
        /// Handles incoming audio data.
        /// </summary>
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

        /// <summary>
        /// Handles cleanup when recording stops.
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
        /// Processes the current audio buffer asynchronously.
        /// </summary>
        /// <param name="partial">Whether this is a partial (mid-speech) transcription</param>
        private async Task ProcessAudioStreamAsync(bool partial = false)
        {
            byte[] audioData;
            // Lock and copy the current audio buffer to avoid conflicts with new incoming audio.
            lock (_audioStreamLock)
            {
                if (audioStream.Length == 0)
                {
                    return;
                }
                audioData = audioStream.ToArray();
                ResetAudioStream();
            }

            using (var streamToProcess = new MemoryStream(audioData))
            {
                UpdateUI(partial ? "Transcribing part of your speech..." : "Transcribing with OpenAI...", true);

                // Optionally cancel any previous transcription if still running.
                _transcriptionCancellationTokenSource.Cancel();
                _transcriptionCancellationTokenSource = new CancellationTokenSource();

                string transcription = await TranscribeAudioAsync(streamToProcess, _transcriptionCancellationTokenSource.Token);
                if (!string.IsNullOrEmpty(transcription))
                {
                    TranscriptionReceived?.Invoke(transcription);
                    UpdateUI("Transcription complete.", false);
                }
                else
                {
                    UpdateUI("Error transcribing audio.", false);
                }
            }
        }

        /// <summary>
        /// Checks if silence or a short pause has occurred and processes the buffered audio accordingly.
        /// </summary>
        private void ProcessSilenceOrShortPause()
        {
            var silenceDuration = DateTime.Now.Subtract(lastSoundTimestamp).TotalMilliseconds;

            if (!isCurrentlySpeaking || silenceDuration < 500)
            {
                return; // Not enough silence to trigger processing.
            }

            // Process a short pause as a partial transcription.
            if (silenceDuration <= Settings.SilenceAutoTurnOffDuration && isCurrentlySpeaking)
            {
                if (!isProcessingShortPause)
                {
                    isProcessingShortPause = true;
                    _ = ProcessAudioStreamAsync(partial: true);
                    // Reset speaking timing without ending the current session.
                    speakingStartedTimestamp = DateTime.Now;
                    speakingDuration = TimeSpan.Zero;
                    // Allow further processing after a short delay.
                    Task.Delay(500).ContinueWith(_ => isProcessingShortPause = false);
                }
            }
            // If the silence is too long, end the speaking session.
            else if (silenceDuration > Settings.SilenceAutoTurnOffDuration && isCurrentlySpeaking)
            {
                isCurrentlySpeaking = false;
                StopRecording();
                UpdateUI($"Silence detected for more than {Settings.SilenceAutoTurnOffDuration / 1000.0} seconds, auto-disabling STT session...", false);
                if(Settings.SendAftersilence)
                    SentChatMessage?.Invoke();
            }
        }

        /// <summary>
        /// Handles settings changes – e.g. when the recording device is changed.
        /// </summary>
        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.SelectedDeviceIndex))
            {
                StopRecording();
                InitializeWaveIn();
            }
        }

        /// <summary>
        /// Transcribes the provided audio stream using OpenAI.
        /// </summary>
        /// <param name="audioStream">The audio stream to transcribe.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the transcription if needed.</param>
        /// <returns>The transcription text.</returns>
        private async Task<string> TranscribeAudioAsync(Stream audioStream, CancellationToken cancellationToken = default)
        {
            // Create a temporary file with a .wav extension. Some APIs require the extension.
            string tempFilePath = Path.GetTempFileName() + ".wav";
            try
            {
                // Write the audio data to a WAV file.
                using (var writer = new WaveFileWriter(tempFilePath, waveIn.WaveFormat))
                {
                    await audioStream.CopyToAsync(writer, 81920, cancellationToken);
                    writer.Flush();
                }

                // Call the OpenAI transcription endpoint.
                // If your OpenAI client supports cancellation tokens, pass it here.
                var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint.CreateTranscriptionTextAsync(
                    new AudioTranscriptionRequest(
                        tempFilePath,
                        language: Settings.TranslateToCustomLanguage ? Settings.SelectedSpeechToTextLanguage.Code : null
                    ),
                    cancellationToken
                );

                return response;
            }
            catch (OperationCanceledException)
            {
                Logging.WriteInfo("Transcription was canceled.");
                return null;
            }
            catch (Exception ex)
            {
                UpdateUI($"Error during transcription: {ex.Message}", false);
                Logging.WriteInfo($"Transcription error: {ex}");
                return null;
            }
            finally
            {
                // Clean up the temporary file.
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        /// <summary>
        /// Updates the speaking duration based on the current time.
        /// </summary>
        private void UpdateSpeakingDuration()
        {
            if (isCurrentlySpeaking)
            {
                speakingDuration = DateTime.Now - speakingStartedTimestamp;
            }
        }

        /// <summary>
        /// Resets the shared audio stream.
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
        /// Returns the current length of the audio stream in a thread-safe manner.
        /// </summary>
        private long GetAudioStreamLength()
        {
            lock (_audioStreamLock)
            {
                return audioStream.Length;
            }
        }

        /// <summary>
        /// Updates the UI message. Uses the dispatcher to update WPF UI elements.
        /// </summary>
        /// <param name="message">Message text</param>
        /// <param name="isVisible">Whether the message should be visible</param>
        private async void UpdateUI(string message, bool isVisible)
        {
            // Update the UI using your ViewModel.
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabelTxt = message;
            ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = isVisible;

            if (!isVisible)
            {
                // Show the message briefly before hiding it.
                ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = true;
                await Task.Delay(2500);
                App.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.Instance.IntelliChatModule.Settings.IntelliChatUILabel = false;
                });
            }
        }

        /// <summary>
        /// Starts the audio recording session.
        /// </summary>
        public void StartRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                UpdateUI("OpenAI not initialized. Please check your settings.", false);
                return;
            }
            if (waveIn == null)
            {
                UpdateUI("Starting recording failed: Device not initialized.", false);
                return;
            }
            if (Settings.IsRecording)
            {
                UpdateUI("Already recording.", false);
                return;
            }
            UpdateUI("Ready to speak?", true);
            try
            {
                waveIn.StartRecording();
                Settings.IsRecording = true;
            }
            catch (Exception ex)
            {
                UpdateUI($"Error starting recording: {ex.Message}", false);
                Logging.WriteInfo($"StartRecording error: {ex}");
            }
        }

        /// <summary>
        /// Stops the audio recording session.
        /// </summary>
        public void StopRecording()
        {
            if (!OpenAIModule.Instance.IsInitialized)
            {
                ViewModel.Instance.ActivateSetting("Settings_OpenAI");
                UpdateUI("OpenAI not initialized. Please check your settings.", false);
                return;
            }
            if (waveIn == null)
            {
                UpdateUI("Stopping recording failed: Device not initialized.", false);
                return;
            }
            if (!Settings.IsRecording)
            {
                UpdateUI("Not currently recording.", false);
                return;
            }
            try
            {
                waveIn.StopRecording();
                Settings.IsRecording = false;
                UpdateUI("Recording stopped. Processing last audio...", false);

                // Process any remaining buffered audio.
                if (GetAudioStreamLength() > 0)
                {
                    _ = ProcessAudioStreamAsync();
                }
            }
            catch (Exception ex)
            {
                UpdateUI($"Error stopping recording: {ex.Message}", false);
                Logging.WriteInfo($"StopRecording error: {ex}");
            }
        }

        /// <summary>
        /// Releases audio and other resources.
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
        /// Should be called on application shutdown to persist settings.
        /// </summary>
        public void OnApplicationClosing()
        {
            Settings.SaveSettings();
        }
    }
}
