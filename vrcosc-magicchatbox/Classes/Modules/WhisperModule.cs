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

namespace vrcosc_magicchatbox.Classes.Modules
{
    public partial class WhisperModuleSettings : ObservableObject
    {
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

        public WhisperModuleSettings()
        {
            RefreshDevices();
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

        public event Action<string> TranscriptionReceived;

        [ObservableProperty]
        public WhisperModuleSettings settings = new WhisperModuleSettings();

        public WhisperModule()
        {
            InitializeWaveIn();
        }

        private void InitializeWaveIn()
        {
            waveIn?.Dispose();

            if (settings.SelectedDeviceIndex == -1)
            {
                UpdateUI("No valid audio input device selected.", false);
                throw new InvalidOperationException("No valid audio input device selected.");
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
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            float maxAmplitude = CalculateMaxAmplitude(e.Buffer, e.BytesRecorded);
            bool isLoudEnough = maxAmplitude > settings.NoiseGateThreshold;

            settings.IsNoiseGateOpen = isLoudEnough;

            if (isLoudEnough)
            {
                if (!isCurrentlySpeaking)
                {
                    speakingStartedTimestamp = DateTime.Now; // Mark the start of speaking
                    isCurrentlySpeaking = true;
                }

                // Update elapsed speaking time continuously while speaking
                var speakingDuration = (DateTime.Now - speakingStartedTimestamp).TotalSeconds;
                UpdateUI($"Speaking detected, recording... (Duration: {speakingDuration:0.0}s)", true);

                audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                lastSoundTimestamp = DateTime.Now;
            }
            else if (isCurrentlySpeaking && DateTime.Now.Subtract(lastSoundTimestamp).TotalMilliseconds > 500)
            {
                var speakingDuration = DateTime.Now.Subtract(speakingStartedTimestamp).TotalSeconds;
                isCurrentlySpeaking = false;
                UpdateUI($"Processing audio... (Duration: {speakingDuration:0.0}s)", true);
                ProcessAudioStreamAsync(audioStream);
                audioStream = new MemoryStream(); // Reset the stream for new data after processing
            }
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

        private async void ProcessAudioStreamAsync(MemoryStream stream)
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

                var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint.CreateTranscriptionAsync(new AudioTranscriptionRequest(tempFilePath));

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
