using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;
using vrcosc_magicchatbox.Core.Toast;
using vrcosc_magicchatbox.Services;

namespace vrcosc_magicchatbox.Core.Configuration;

public static class JsonSettingsSerialization
{
    // Collections pre-populated with defaults (e.g. IntegrationSettings.SavedSortOrder) must be
    // replaced by the saved values on load, not appended to - Json.NET's default is to append.
    public static readonly JsonSerializerSettings DeserializerSettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
}

public sealed class JsonSettingsProvider<T> : ISettingsProvider<T>, IDisposable where T : class, new()
{
    private T _settings;
    private readonly string _filePath;
    private readonly object _lock = new();
    private Timer _debounceTimer;
    private const int DebounceDelayMs = 2000;
    private volatile bool _loaded;
    private bool _disposed;
    private bool _loadFailed;
    private bool _saveFailureLogged;

    public event EventHandler SettingsChanged;

    public JsonSettingsProvider(IEnvironmentService environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _filePath = Path.Combine(environment.DataPath, $"{typeof(T).Name}.json");
        _settings = null!;
    }

    public T Value
    {
        get
        {
            if (!_loaded)
            {
                lock (_lock)
                {
                    if (!_loaded)
                    {
                        Reload();
                        _loaded = true;
                    }
                }
            }
            return _settings;
        }
    }

    public void Reload()
    {
        lock (_lock)
        {
            UnsubscribeAutoSave();
            _loadFailed = false;

            bool loadedFromFile = false;
            bool resetApplied = false;

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = ReadFileWithRetry(_filePath);
                    if (!string.IsNullOrWhiteSpace(json) && !json.All(c => c == '\0'))
                    {
                        var loaded = JsonConvert.DeserializeObject<T>(json, JsonSettingsSerialization.DeserializerSettings);
                        if (loaded is null)
                            Logging.WriteInfo($"Settings file for {typeof(T).Name} deserialized to null (damaged content?); falling back to defaults.");
                        _settings = loaded ?? new T();
                        resetApplied = ApplyVersionResets();
                        loadedFromFile = true;
                    }
                }
            }
            catch (JsonException ex)
            {
                Logging.WriteInfo($"Error loading settings for {typeof(T).Name}: {ex.Message}");
                BackupCorruptSettingsFile(ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _loadFailed = true;
                Logging.WriteException(
                    new IOException(
                        $"Settings file for {typeof(T).Name} could not be read; using in-memory defaults for this session (changes will NOT be saved).",
                        ex),
                    MSGBox: false);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error loading settings for {typeof(T).Name}: {ex.Message}");
                BackupCorruptSettingsFile(ex);
            }

            if (!loadedFromFile)
                _settings = new T();

            if (!_loadFailed)
                SubscribeAutoSave();

            SettingsChanged?.Invoke(this, EventArgs.Empty);
            if (resetApplied)
                Save();
        }
    }

    private static string ReadFileWithRetry(string path)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < maxAttempts)
            {
                Thread.Sleep(150);
            }
        }
    }

    private bool ApplyVersionResets()
    {
        if (_settings is not VersionedSettings vs)
            return false;

        var type = typeof(T);
        string loadedAppVersion = vs.AppVersion ?? string.Empty;
        int loadedSchema = vs.SchemaVersion;

        var moduleReset = type.GetCustomAttribute<ResetModuleAfterSchemaAttribute>();
        if (moduleReset != null && loadedSchema < moduleReset.SchemaVersion)
        {
            Logging.WriteInfo(
                $"[VersionReset] {type.Name}: schema {loadedSchema} < {moduleReset.SchemaVersion}, resetting module to defaults.");
            var migratedAt = vs.MigratedAt;
            _settings = new T();
            if (_settings is VersionedSettings fresh)
                fresh.MigratedAt = migratedAt;
            StampVersion();
            return true;
        }

        bool anyReset = false;
        T defaults = new();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var attr = prop.GetCustomAttribute<ResetAfterVersionAttribute>();
            if (attr == null) continue;

            if (AppVersion.IsOlderThan(loadedAppVersion, attr.MinVersion))
            {
                try
                {
                    object defaultVal = prop.GetValue(defaults);
                    prop.SetValue(_settings, defaultVal);
                    anyReset = true;
                    Logging.WriteInfo(
                        $"[VersionReset] {type.Name}.{prop.Name}: reset (loaded version '{loadedAppVersion}' < '{attr.MinVersion}').");
                }
                catch (Exception ex)
                {
                    Logging.WriteInfo($"[VersionReset] Failed to reset {type.Name}.{prop.Name}: {ex.Message}");
                }
            }
        }

        if (anyReset)
            StampVersion();

        return anyReset;
    }

    public void Save()
    {
        lock (_lock)
        {
            if (_settings is null)
                return;

            if (_loadFailed)
                return;

            bool saved = false;
            try
            {
                StampVersion();

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);

                saved = AtomicFileWriter.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error saving settings for {typeof(T).Name}: {ex.Message}");
            }

            if (!saved && !_saveFailureLogged)
            {
                _saveFailureLogged = true;
                Logging.WriteException(
                    new IOException($"Settings for {typeof(T).Name} could not be saved to '{_filePath}'; changes will not persist across restarts."),
                    MSGBox: false);
                NotifySaveFailed();
            }
        }
    }

    private static void NotifySaveFailed()
    {
        try
        {
            App.Services?.GetService<IToastService>()?.Show(
                "Settings not saved",
                $"Changes to {typeof(T).Name} could not be written to disk and will not persist across restarts. Check disk space or antivirus.",
                ToastType.Error,
                durationMs: 10000,
                key: $"settings-save-{typeof(T).Name}");
        }
        catch
        {
        }
    }

    private void BackupCorruptSettingsFile(Exception loadException)
    {
        try
        {
            if (!File.Exists(_filePath))
                return;

            string backupPath = $"{_filePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(_filePath, backupPath, overwrite: false);
            Logging.WriteInfo($"Backed up corrupt settings for {typeof(T).Name} to {backupPath}: {loadException.Message}");
        }
        catch (Exception backupException)
        {
            Logging.WriteInfo($"Could not back up corrupt settings for {typeof(T).Name}: {backupException.Message}");
        }
    }

    public void FlushPendingSave()
    {
        lock (_lock)
        {
            if (_disposed) return;

            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        Save();
    }

    private void SubscribeAutoSave()
    {
        if (_settings is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void UnsubscribeAutoSave()
    {
        if (_settings is INotifyPropertyChanged npc)
            npc.PropertyChanged -= OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_debounceTimer != null)
            {
                _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
            }
            else
            {
                _debounceTimer = new Timer(_ => Save(), null, DebounceDelayMs, Timeout.Infinite);
            }
        }
    }

    private void StampVersion()
    {
        if (_settings is not VersionedSettings vs) return;
        vs.AppVersion = AppVersion.Current;
        vs.SchemaVersion = typeof(T).GetCustomAttribute<CurrentSchemaAttribute>()?.Version ?? 1;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        UnsubscribeAutoSave();
        Save();
    }
}
