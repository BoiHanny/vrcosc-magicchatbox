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
using vrcosc_magicchatbox.Classes.Modules.SpeechToText;
using vrcosc_magicchatbox.Core.Configuration;
using vrcosc_magicchatbox.Core.Messaging;
using vrcosc_magicchatbox.Core.State;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class SpeechToTextLanguage : ObservableObject
    {
        public string Code { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }

    public partial class WhisperModuleSettings : ObservableObject
    {
        private const string SettingsFileName = "WhisperModuleSettings.json";

        [ObservableProperty]
        private List<RecordingDeviceInfo> availableDevices;

        [ObservableProperty]
        private IntelliGPTModel speechToTextModel = SpeechToTextModels.Recommended;

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
        private SpeechToTextLanguage? selectedSpeechToTextLanguage;

        [ObservableProperty]
        private int silenceAutoTurnOffDuration = 3000;

        [ObservableProperty]
        private List<SpeechToTextLanguage> speechToTextLanguages = new();

        [ObservableProperty]
        private bool translateToCustomLanguage = false;

        [JsonIgnore]
        public IEnumerable<IntelliGPTModel> AvailableSTTModels => SpeechToTextModels.Ordered;

        private WhisperModuleSettings()
        {
            AvailableDevices = new List<RecordingDeviceInfo>();
            RefreshSpeechToTextLanguages();
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

            bool languageExists = SpeechToTextLanguages.Any(lang => lang.Code == currentSelectedLanguageCode);
            SelectedSpeechToTextLanguage = languageExists
                ? SpeechToTextLanguages.First(lang => lang.Code == currentSelectedLanguageCode)
                : SpeechToTextLanguages.FirstOrDefault();

            OnPropertyChanged(nameof(SelectedSpeechToTextLanguage));
        }

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

                        settings.SpeechToTextModel = SpeechToTextModels.Resolve(settings.SpeechToTextModel);
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
            if (!AvailableDevices.Any())
            {
                SelectedDeviceIndex = -1;
                return;
            }

            if (SelectedDeviceIndex < 0 || SelectedDeviceIndex >= AvailableDevices.Count)
                SelectedDeviceIndex = 0;
        }

        public void SaveSettings()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsFolder = Path.Combine(appDataPath, "Vrcosc-MagicChatbox");
            var path = Path.Combine(settingsFolder, SettingsFileName);

            string settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented);
            if (!AtomicFileWriter.WriteAllText(path, settingsJson))
            {
                Logging.WriteInfo("Failed to save Whisper module settings.");
            }
        }
    }

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

    public partial class WhisperModule : ObservableObject, IModule
    {
        private readonly IMessenger _messenger;
        private readonly IMenuNavigationService _navService;
        private readonly IUiDispatcher _dispatcher;
        private readonly ITranscriptionService _transcription;
        private readonly IToastService? _toast;

        private readonly MemoryStream audioStream = new MemoryStream();
        private readonly object _audioStreamLock = new object();

        private CancellationTokenSource _transcriptionCancellationTokenSource = new CancellationTokenSource();

        private bool isCurrentlySpeaking;
        private bool isProcessingShortPause;
        private DateTime lastSoundTimestamp = DateTime.Now;
        private TimeSpan speakingDuration;
        private DateTime speakingStartedTimestamp;

        private WaveIn? waveIn;

        [ObservableProperty]
        private WhisperModuleSettings settings;

        public string Name => "Whisper";
        public bool IsEnabled { get; set; } = true;
        public bool IsRunning => waveIn != null;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) { Dispose(); return Task.CompletedTask; }
        public void SaveSettings() => Settings?.SaveSettings();

        public event Action<string>? TranscriptionReceived;

        public event Action? SentChatMessage;

        public WhisperModule(IMenuNavigationService navService, ITranscriptionService transcription, IUiDispatcher dispatcher, IMessenger messenger, IToastService? toast = null)
        {
            _navService = navService;
            _transcription = transcription;
            _dispatcher = dispatcher;
            _messenger = messenger;
            _toast = toast;
            Settings = WhisperModuleSettings.LoadSettings();
            Settings.PropertyChanged += Settings_PropertyChanged;
            _ = WarmUpRecordingDevicesAsync();
        }

        private async Task WarmUpRecordingDevicesAsync()
        {
            var devices = await Task.Run(WhisperModuleSettings.GetAvailableDevicesSafe);
            await _dispatcher.InvokeAsync(() =>
            {
                Settings.ApplyAvailableDevices(devices);
            });
        }

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

        private void HandleSpeakingState(WaveInEventArgs e)
        {
            if (!isCurrentlySpeaking)
            {
                speakingStartedTimestamp = DateTime.Now;
                isCurrentlySpeaking = true;
                speakingDuration = TimeSpan.Zero;

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

        private void InitializeWaveIn()
        {
            try
            {
                if (waveIn != null)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.RecordingStopped -= OnRecordingStopped;
                    waveIn.Dispose();
                    waveIn = null;
                }

                if (Settings.SelectedDeviceIndex == -1)
                {
                    _ = UpdateUI("No valid audio input device selected.", false);
                    return;
                }

                waveIn = new WaveIn
                {
                    DeviceNumber = Settings.SelectedDeviceIndex,
                    WaveFormat = new WaveFormat(16000, 16, 1),                    BufferMilliseconds = 350                };

                waveIn.DataAvailable += OnDataAvailable;
                waveIn.RecordingStopped += OnRecordingStopped;
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Failed to initialize whisper recording device: {ex.Message}");
                _ = UpdateUI("Audio input initialization failed.", false);
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
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

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
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

                string? transcription = await TranscribeAudioAsync(localCopyStream, _transcriptionCancellationTokenSource.Token);
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

        private void ProcessSilenceOrShortPause()
        {
            double silenceMs = (DateTime.Now - lastSoundTimestamp).TotalMilliseconds;
            if (!isCurrentlySpeaking || silenceMs < 500)
                return;

            if (silenceMs <= Settings.SilenceAutoTurnOffDuration)
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
            else
            {
                isCurrentlySpeaking = false;
                StopRecording();                _ = UpdateUI($"Silence > {Settings.SilenceAutoTurnOffDuration / 1000.0}s, stopping STT...", false);
            }
        }

        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.SelectedDeviceIndex))
            {
                StopRecording();
                Settings.IsRecording = false;
                if (waveIn != null)
                    InitializeWaveIn();
            }
        }

        private async Task<string?> TranscribeAudioAsync(Stream waveFileStream, CancellationToken cancellationToken)
        {
            try
            {
                using var wavMemory = new MemoryStream();
                // A recording session must be active for audio to have reached here, so waveIn is populated.
                using (var writer = new WaveFileWriter(wavMemory, waveIn!.WaveFormat))
                {
                    await waveFileStream.CopyToAsync(writer, 81920, cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }

                byte[] wavBytes = wavMemory.ToArray();

                string modelName = GetModelDescription(Settings.SpeechToTextModel);
                string? languageCode = Settings.TranslateToCustomLanguage
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

        private void UpdateSpeakingDuration()
        {
            if (isCurrentlySpeaking)
            {
                speakingDuration = DateTime.Now - speakingStartedTimestamp;
            }
        }

        private void ResetAudioStream()
        {
            lock (_audioStreamLock)
            {
                audioStream.SetLength(0);
                audioStream.Position = 0;
            }
        }

        private long GetAudioStreamLength()
        {
            lock (_audioStreamLock)
            {
                return audioStream.Length;
            }
        }

        private Task UpdateUI(string message, bool showPermanently)
        {
            _messenger.Send(new IntelliChatUiStatusMessage(message, showPermanently));
            return Task.CompletedTask;
        }

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
                InitializeWaveIn();

            if (waveIn == null)
            {
                _ = UpdateUI("No audio device is ready.", false);
                return;
            }

            if (Settings.IsRecording)
            {
                _ = UpdateUI("Already recording.", false);
                return;
            }

            try
            {
                waveIn.StartRecording();
                Settings.IsRecording = true;
                _ = UpdateUI("Recording started. Speak now...", true);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StartRecording error: {ex}");
                _toast?.Show("🎙 Recording Error", $"Failed to start: {ex.Message}", ToastType.Error, key: "whisper-recording-error");
                _ = UpdateUI($"Error starting recording: {ex.Message}", false);
            }
        }

        public void StopRecording()
        {
            if (waveIn == null)
            {
                _ = UpdateUI("StopRecording failed: no audio device.", false);
                return;
            }

            if (!Settings.IsRecording)
            {
                _ = UpdateUI("Not currently recording.", false);
                return;
            }

            try
            {
                waveIn.StopRecording();
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"StopRecording error: {ex}");
                _ = UpdateUI($"Error stopping recording: {ex.Message}", false);
                return;
            }
            finally
            {
                Settings.IsRecording = false;
            }

            if (!_transcription.IsReady)
            {
                ResetAudioStream();
                _toast?.Show("🎙 Speech to Text", "OpenAI not initialized. Check your API key in settings.", ToastType.Warning,
                    new ToastAction("Settings", () => { _navService.ActivateSetting("Settings_OpenAI"); return Task.CompletedTask; }),
                    key: "whisper-openai-error");
                _navService.ActivateSetting("Settings_OpenAI");
                _ = UpdateUI("OpenAI not initialized. Please check settings.", false);
                return;
            }

            _ = UpdateUI("Stopped. Processing final chunk...", false);

            if (GetAudioStreamLength() > 0)
            {
                var finalTask = ProcessAudioStreamAsync(partial: false);
                finalTask.ContinueWith(t =>
                {
                    if (!t.IsFaulted && !t.IsCanceled && Settings.SendAftersilence)
                    {
                        SentChatMessage?.Invoke();
                    }
                });
            }
            else
            {
                if (Settings.SendAftersilence)
                {
                    SentChatMessage?.Invoke();
                }
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (waveIn != null)
            {
                waveIn.DataAvailable -= OnDataAvailable;
                waveIn.RecordingStopped -= OnRecordingStopped;

                try
                {
                    waveIn.StopRecording();
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"Failed to stop whisper recording during dispose: {ex.Message}");
                }

                waveIn.Dispose();
                waveIn = null;
            }

            audioStream?.Dispose();

            _transcriptionCancellationTokenSource?.Cancel();
            _transcriptionCancellationTokenSource?.Dispose();

            _ = UpdateUI("Disposed resources.", false);
        }

        public void OnApplicationClosing()
        {
            Settings.SaveSettings();
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
