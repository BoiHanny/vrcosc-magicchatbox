using NAudio.Wave;
using OpenAI.Audio;
using OpenAI;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace vrcosc_magicchatbox.Classes.Modules
{
    public class WhisperModule : IDisposable
    {
        private readonly WaveInEvent waveIn;
        private readonly ConcurrentQueue<byte[]> audioQueue = new ConcurrentQueue<byte[]>();
        private readonly MemoryStream audioBuffer = new MemoryStream();
        private readonly object bufferLock = new object();
        private bool isProcessing;
        private const int BufferThreshold = 10000; // Set a threshold for the buffer size

        public event Action<string> TranscriptionReceived;

        public WhisperModule()
        {
            // Initialize waveIn with a specific device or the default device
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1), // Sample rate and channel configuration
                DeviceNumber = GetDefaultAudioDeviceNumber() // Gets the default audio device number
            };
            waveIn.DataAvailable += OnDataAvailable;
        }

        private int GetDefaultAudioDeviceNumber()
        {
            // This method attempts to find a valid audio input device
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var deviceInfo = WaveIn.GetCapabilities(n);
                // You can add more checks here if necessary, e.g., device name
                if (deviceInfo.Channels > 0) // Check if the device has at least one channel
                {
                    return n; // Return the device number of the first valid device found
                }
            }

            return -1; // Return -1 if no valid devices are found
        }

        public void StartRecording()
        {
            try
            {
                // Before starting recording, check if the device number is valid
                if (waveIn.DeviceNumber < 0 || waveIn.DeviceNumber >= WaveIn.DeviceCount)
                {
                    throw new InvalidOperationException("No valid audio input device found.");
                }

                waveIn.StartRecording();
            }
            catch (NAudio.MmException ex)
            {
                Console.WriteLine($"Error starting recording: {ex.Message}");
                // Handle the error accordingly, maybe prompt the user to check their audio device
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                // Handle other types of exceptions
            }
        }


        public void StopRecording()
        {
            waveIn.StopRecording();
            ProcessBuffer(); // Ensure any remaining audio is processed
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            lock (bufferLock)
            {
                audioBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                if (audioBuffer.Length > BufferThreshold)
                {
                    audioQueue.Enqueue(audioBuffer.ToArray());
                    audioBuffer.SetLength(0); // Clear the buffer
                }
            }

            if (!isProcessing)
            {
                isProcessing = true;
                Task.Run(() => ProcessQueue());
            }
        }

        private async Task ProcessQueue()
        {
            while (audioQueue.TryDequeue(out var buffer))
            {
                using var memoryStream = new MemoryStream(buffer);
                var request = new AudioTranscriptionRequest(memoryStream, "audio.wav");

                try
                {
                    var response = await OpenAIModule.Instance.OpenAIClient.AudioEndpoint.CreateTranscriptionAsync(request);
                    TranscriptionReceived?.Invoke(response);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during transcription: {ex.Message}");
                }
            }

            isProcessing = false;
        }

        private void ProcessBuffer()
        {
            if (audioBuffer.Length > 0)
            {
                audioQueue.Enqueue(audioBuffer.ToArray());
                audioBuffer.SetLength(0);
            }

            if (!isProcessing && audioQueue.Count > 0)
            {
                isProcessing = true;
                Task.Run(() => ProcessQueue());
            }
        }

        public void Dispose()
        {
            waveIn.Dispose();
            audioBuffer.Dispose();
        }
    }

}
