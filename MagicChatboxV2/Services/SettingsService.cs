using MagicChatboxV2.Models;
using Serilog.Events;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MagicChatboxV2.Services
{
    public interface ISettingsService
    {
        Task SaveSettingsAsync<T>(T settings) where T : ISettings;
        Task<T> LoadSettingsAsync<T>() where T : ISettings;
    }

    public class SettingsService : ISettingsService
    {
        private readonly string settingsDirectory;
        private readonly IAppOutputService _appOutputService;

        public SettingsService(IAppOutputService appOutputService)
        {
            settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MagicChatboxV2");
            Directory.CreateDirectory(settingsDirectory);
            _appOutputService = appOutputService;
        }

        public async Task SaveSettingsAsync<T>(T settings) where T : ISettings
        {
            try
            {
                if (settings == null)
                {
                    Exception ex = new Exception($"Failed to save settings for {typeof(T).Name} because the settings object was null.");
                    _appOutputService.LogExceptionWithDialog(ex, "Failed to save settings", LogEventLevel.Error, false, true, 5000, false);
                    return;
                }

                string settingsFilePath = GetSettingsFilePath(typeof(T).Name);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Exception ex = new Exception($"Failed to serialize settings for {typeof(T).Name} because the JSON string was empty.");
                    _appOutputService.LogExceptionWithDialog(ex, "Failed to serialize settings", LogEventLevel.Error, false, true, 5000, false);
                    return;
                }

                await File.WriteAllTextAsync(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                _appOutputService.LogExceptionWithDialog(new Exception($"Failed to save settings for {typeof(T).Name}: {ex.Message}\n{ex.StackTrace}", ex), "Error saving settings", LogEventLevel.Error, false, true, 5000, false);
            }
        }

        public async Task<T> LoadSettingsAsync<T>() where T : ISettings
        {
            try
            {
                string settingsFilePath = GetSettingsFilePath(typeof(T).Name);
                if (File.Exists(settingsFilePath))
                {
                    string json = await File.ReadAllTextAsync(settingsFilePath);
                    if (string.IsNullOrWhiteSpace(json) || json.Contains("null"))
                    {
                        return await GetNewSettingsAsync<T>(typeof(T).Name);
                    }
                    return JsonSerializer.Deserialize<T>(json);
                }
                else
                {
                    Exception ex = new FileNotFoundException($"Settings file not found for {typeof(T).Name}", settingsFilePath);
                    _appOutputService.LogExceptionWithDialog(ex, "Settings file not found", LogEventLevel.Error, false, true, 5000, false);
                    return await GetNewSettingsAsync<T>(typeof(T).Name);
                }
            }
            catch (Exception ex)
            {
                _appOutputService.LogExceptionWithDialog(new Exception($"Failed to load settings for {typeof(T).Name}: {ex.Message}\n{ex.StackTrace}", ex), "Error loading settings", LogEventLevel.Error, false, true, 5000, false);
                return await GetNewSettingsAsync<T>(typeof(T).Name);
            }
        }

        private async Task<T> GetNewSettingsAsync<T>(string moduleName) where T : ISettings
        {
            try
            {
                var settings = (T)Activator.CreateInstance(typeof(T));
                await SaveSettingsAsync(settings);
                return settings;
            }
            catch (Exception ex)
            {
                _appOutputService.LogExceptionWithDialog(ex, "Error creating new settings", LogEventLevel.Error, false, true, 5000, false);
                return default;
            }
        }



        private string GetSettingsFilePath(string moduleName)
        {
            return Path.Combine(settingsDirectory, $"{moduleName}.json");
        }
    }
}
