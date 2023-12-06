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
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1) // Sample rate and channel configuration
            };
            waveIn.DataAvailable += OnDataAvailable;
        }

        public void StartRecording()
        {
            waveIn.StartRecording();
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
                    TranscriptionReceived?.Invoke(response); // Notify subscribers
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
