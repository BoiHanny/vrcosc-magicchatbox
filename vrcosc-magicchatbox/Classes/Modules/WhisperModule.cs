using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Messaging;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;

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
        private int selectedDeviceIndex = -1;

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

        private WhisperModuleSettings()
        {
            availableDevices = new List<RecordingDeviceInfo>();
            RefreshSpeechToTextLanguages();
        }

        /// <summary>
        /// Refreshes the list of supported languages in the UI and preserves the current selection if still valid.
        /// </summary>
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
            AvailableDevices = GetAvailableDevicesSafe();
            NormalizeSelectedDeviceIndex();
        }

        public static List<RecordingDeviceInfo> GetAvailableDevicesSafe()
        {
            var devices = new List<RecordingDeviceInfo>();

            try
            {
                for (int n = 0; n < WaveIn.DeviceCount; n++)
                {
                    var caps = WaveIn.GetCapabilities(n);
                    devices.Add(new RecordingDeviceInfo(n, caps.ProductName));
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Failed to enumerate whisper recording devices: {ex.Message}");
            }

            return devices;
        }

        public void ApplyAvailableDevices(List<RecordingDeviceInfo> devices)
        {
            AvailableDevices = devices ?? new List<RecordingDeviceInfo>();
            NormalizeSelectedDeviceIndex();
        }

        private void NormalizeSelectedDeviceIndex()
        {
            if (!availableDevices.Any())
            {
                SelectedDeviceIndex = -1;
                return;
            }

            if (selectedDeviceIndex < 0 || selectedDeviceIndex >= availableDevices.Count)
                SelectedDeviceIndex = 0;
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
    public partial class WhisperModule : ObservableObject, IModule
    {
        private readonly IMessenger _messenger;
        private readonly IMenuNavigationService _navService;
        private readonly IUiDispatcher _dispatcher;
        private readonly ITranscriptionService _transcription;
        private readonly IToastService? _toast;

        // Thread-safe audio stream plus lock.
        private readonly MemoryStream audioStream = new MemoryStream();
        private readonly object _audioStreamLock = new object();

        private CancellationTokenSource _transcriptionCancellationTokenSource = new CancellationTokenSource();

        private bool isCurrentlySpeaking;
        private bool isProcessingShortPause;
        private DateTime lastSoundTimestamp = DateTime.Now;
        private TimeSpan speakingDuration;
        private DateTime speakingStartedTimestamp;

        private WaveInEvent waveIn;

        [ObservableProperty]
        private WhisperModuleSettings settings;

        public string Name => "Whisper";
        public bool IsEnabled { get; set; } = true;
        public bool IsRunning => waveIn != null;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) { Dispose(); return Task.CompletedTask; }
        public void SaveSettings() => Settings?.SaveSettings();

        /// <summary>
        /// Raised when transcription text is ready.
        /// </summary>
        public event Action<string> TranscriptionReceived;

        /// <summary>
        /// Raised after a final chunk is transcribed, if auto-sending is enabled.
        /// </summary>
        public event Action SentChatMessage;

        public WhisperModule(IMenuNavigationService navService, ITranscriptionService transcription, IUiDispatcher dispatcher, IMessenger messenger, IToastService? toast = null)
        {
            _navService = navService;
            _transcription = transcription;
            _dispatcher = dispatcher;
            _messenger = messenger;
            _toast = toast;
            settings = WhisperModuleSettings.LoadSettings();
            settings.PropertyChanged += Settings_PropertyChanged;
            InitializeWaveIn();
            _ = WarmUpRecordingDevicesAsync();
        }

        private async Task WarmUpRecordingDevicesAsync()
        {
            var devices = await Task.Run(WhisperModuleSettings.GetAvailableDevicesSafe);
            await _dispatcher.InvokeAsync(() =>
            {
                settings.ApplyAvailableDevices(devices);
                InitializeWaveIn();
            });
        }

        /// <summary>
        /// Calculates the maximum amplitude from raw audio data, normalized 0..1.
        /// Uses Span&lt;T&gt; cast to avoid allocating a short[] array every callback.
        /// </summary>
        private static float CalculateMaxAmplitude(byte[] buffer, int bytesRecorded)
        {
            var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(
                buffer.AsSpan(0, bytesRecorded));

            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Math.Abs(samples[i] / 32768f);
                if (abs > max) max = abs;
            }
            return max;
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

            _ = UpdateUI($"Speaking... {speakingDuration.TotalSeconds:0.0}s", true);
        }

        /// <summary>
        /// Sets up the WaveInEvent using the selected device index.
        /// </summary>
        private void InitializeWaveIn()
        {
            try
            {
                waveIn?.Dispose();

                if (settings.SelectedDeviceIndex == -1)
                {
                    _ = UpdateUI("No valid audio input device selected.", false);
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
            catch (Exception ex)
            {
                Logging.WriteInfo($"Failed to initialize whisper recording device: {ex.Message}");
                _ = UpdateUI("Audio input initialization failed.", false);
            }
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
                _toast?.Show("🎙 Recording Error", e.Exception.Message, ToastType.Error, key: "whisper-recording-error");
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
                _ = UpdateUI(
                    partial ? "Transcribing partial audio..." : "Transcribing final audio...",
                    showPermanently: true
                );

                _transcriptionCancellationTokenSource.Cancel();
                _transcriptionCancellationTokenSource.Dispose();
                _transcriptionCancellationTokenSource = new CancellationTokenSource();

                string transcription = await TranscribeAudioAsync(localCopyStream, _transcriptionCancellationTokenSource.Token);
                if (!string.IsNullOrEmpty(transcription))
                {
                    TranscriptionReceived?.Invoke(transcription);
                    _ = UpdateUI("Transcription done.", false);
                }
                else
                {
                    _ = UpdateUI("Transcription error or canceled.", false);
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
                _ = UpdateUI($"Silence > {settings.SilenceAutoTurnOffDuration / 1000.0}s, stopping STT...", false);
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
        /// Builds WAV entirely in memory and passes byte[] directly to the transcription service.
        /// Zero disk I/O — no temp files created or deleted.
        /// </summary>
        private async Task<string> TranscribeAudioAsync(Stream waveFileStream, CancellationToken cancellationToken)
        {
            try
            {
                using var wavMemory = new MemoryStream();
                // WaveFileWriter must be fully disposed before reading the stream
                // so the WAV header length fields are finalized.
                using (var writer = new WaveFileWriter(wavMemory, waveIn.WaveFormat))
                {
                    await waveFileStream.CopyToAsync(writer, 81920, cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }

                byte[] wavBytes = wavMemory.ToArray();

                string modelName = GetModelDescription(Settings.SpeechToTextModel);
                string languageCode = Settings.TranslateToCustomLanguage
                    ? Settings.SelectedSpeechToTextLanguage?.Code
                    : null;

                return await _transcription.TranscribeAsync(
                    wavBytes, "audio.wav", modelName, languageCode, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logging.WriteInfo("Transcription canceled by user or system.");
                return null;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Transcription error: {ex}");
                _ = UpdateUI($"Transcription error: {ex.Message}", false);
                return null;
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
        /// Sends a status message via IMessenger for the IntelliChat UI label.
        /// Replaces direct IntelliChatModule reference — IntelliChatModule subscribes.
        /// </summary>
        private Task UpdateUI(string message, bool showPermanently)
        {
            _messenger.Send(new IntelliChatUiStatusMessage(message, showPermanently));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts capturing audio from the selected device, if valid. Cancels if transcription service uninitialized.
        /// </summary>
        public void StartRecording()
        {
            if (!_transcription.IsReady)
            {
                _toast?.Show("🎙 Speech to Text", "OpenAI not initialized. Check your API key in settings.", ToastType.Warning,
                    new ToastAction("Settings", () => { _navService.ActivateSetting("Settings_OpenAI"); return Task.CompletedTask; }),
                    key: "whisper-openai-error");
                _navService.ActivateSetting("Settings_OpenAI");
                _ = UpdateUI("OpenAI not initialized. Please check settings.", false);
                return;
            }

            if (waveIn == null)
            {
                _ = UpdateUI("No audio device is ready.", false);
                return;
            }

            if (settings.IsRecording)
            {
                _ = UpdateUI("Already recording.", false);
                return;
            }

            try
            {
                waveIn.StartRecording();
                settings.IsRecording = true;
                _ = UpdateUI("Recording started. Speak now...", true);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StartRecording error: {ex}");
                _toast?.Show("🎙 Recording Error", $"Failed to start: {ex.Message}", ToastType.Error, key: "whisper-recording-error");
                _ = UpdateUI($"Error starting recording: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Stops audio capture and processes any remaining audio data, then triggers SentChatMessage as needed.
        /// </summary>
        public void StopRecording()
        {
            if (!_transcription.IsReady)
            {
                _navService.ActivateSetting("Settings_OpenAI");
                _ = UpdateUI("OpenAI not initialized. Please check settings.", false);
                return;
            }

            if (waveIn == null)
            {
                _ = UpdateUI("StopRecording failed: no audio device.", false);
                return;
            }

            if (!settings.IsRecording)
            {
                _ = UpdateUI("Not currently recording.", false);
                return;
            }

            try
            {
                waveIn.StopRecording();
                settings.IsRecording = false;
                _ = UpdateUI("Stopped. Processing final chunk...", false);

                // If leftover data is present, do final transcription. Then, optionally auto-send.
                if (GetAudioStreamLength() > 0)
                {
                    var finalTask = ProcessAudioStreamAsync(partial: false);
                    finalTask.ContinueWith(t =>
                    {
                        if (!t.IsFaulted && !t.IsCanceled && settings.SendAftersilence)
                        {
                            SentChatMessage?.Invoke();
                        }
                    });
                }
                else
                {
                    if (settings.SendAftersilence)
                    {
                        SentChatMessage?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StopRecording error: {ex}");
                _ = UpdateUI($"Error stopping recording: {ex.Message}", false);
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

            _ = UpdateUI("Disposed resources.", false);
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
