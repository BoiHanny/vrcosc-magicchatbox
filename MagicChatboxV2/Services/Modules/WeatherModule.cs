using MagicChatboxV2.UIVM.Models;
using System;
using System.Timers;
using MagicChatboxV2.Services;
using Timer = System.Timers.Timer;

namespace MagicChatboxV2.Services.Modules
{
    public class WeatherModuleSettings : ISettings
    {
        public string Location { get; set; } = "New York"; // Default location
        public int UpdateIntervalMinutes { get; set; } = 1; // Default update interval to 1 minute
        public string ApiKey { get; set; } = "532ccf9caa1465cf1a7d180c3f1cafdc";

        public WeatherModuleSettings(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or whitespace.", nameof(apiKey));

            ApiKey = apiKey;
        }

        public void Dispose()
        {
            // If there's anything to dispose, do it here
        }
    }



    public class WeatherModule : IModule
    {
        private readonly WeatherService _weatherService;
        private Timer _updateTimer;
        private WeatherResponse _currentWeather;
        private WeatherModuleSettings _settings;

        public string ModuleName => "Local Weather";
        public ISettings Settings
        {
            get => _settings;
            set
            {
                if (value is WeatherModuleSettings settings)
                {
                    _settings = settings;
                    RestartTimer(); // Restart timer with new settings
                }
            }
        }
        public bool IsActive { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public bool IsEnabled_VR { get; set; } = true;
        public bool IsEnabled_DESKTOP { get; set; } = true;
        public int ModulePosition { get; set; }
        public int ModuleMemberGroupNumbers { get; set; }
        public DateTime LastUpdated { get; private set; }

        public event EventHandler DataUpdated;

        public WeatherModule(WeatherService weatherService, WeatherModuleSettings settings)
        {
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Initialize();
        }

        public void Initialize()
        {
            InitializeUpdateTimer();
        }

        private void InitializeUpdateTimer()
        {
            _updateTimer = new Timer(_settings.UpdateIntervalMinutes * 60 * 1000);
            _updateTimer.Elapsed += async (sender, e) => await UpdateDataAsync();
            _updateTimer.AutoReset = true;
            _updateTimer.Enabled = IsEnabled; // Enable timer based on IsEnabled property
        }

        private void RestartTimer()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            InitializeUpdateTimer(); // Create a new timer instance with the updated interval
        }

        public void UpdateData()
        {
            UpdateDataAsync().Wait();
        }

        public async Task UpdateDataAsync()
        {
            if (!IsEnabled || !IsActive) return;

            try
            {
                _weatherService.ApiKey = _settings.ApiKey; // Ensure the service uses the current API key
                _currentWeather = await _weatherService.GetWeatherAsync(_settings.Location);
                LastUpdated = DateTime.Now;
                DataUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // Log this exception with an error logging framework
            }
        }




        public string GetFormattedOutput()
        {
            if (_currentWeather == null) return "Weather data is not available.";

            var weatherDesc = _currentWeather.Weather.Count > 0 ? _currentWeather.Weather[0].Description : "N/A";
            return $"Weather in {_currentWeather.Name}: {_currentWeather.Main.Temp}°C, {weatherDesc}";
        }

        public string UpdateAndGetOutput()
        {
            UpdateDataAsync().Wait();
            return GetFormattedOutput();
        }

        public async Task<string> UpdateAndGetOutputAsync()
        {
            await UpdateDataAsync();
            return GetFormattedOutput();
        }

        public void StartUpdates()
        {
            _updateTimer?.Start();
        }

        public void StopUpdates()
        {
            _updateTimer?.Stop();
        }

        public void SaveState()
        {
            // Implement state saving logic, possibly saving to a file or user settings
        }

        public void LoadState()
        {
            // Implement state loading logic, possibly reading from a file or user settings
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
        }
    }
}
