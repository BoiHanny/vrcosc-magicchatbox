using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using vrcosc_magicchatbox.Classes.DataAndSecurity;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// JSON file-backed settings provider with auto-save.
/// - Resolves file path to %APPDATA%\Vrcosc-MagicChatbox\{TypeName}.json
/// - Thread-safe, corruption-resistant (ignores NUL-filled files)
/// - Atomic writes (temp file → rename) to prevent corruption on crash
/// - Debounced auto-save: subscribes to INotifyPropertyChanged on the settings
///   object and saves 2 seconds after the last property change
/// </summary>
public sealed class JsonSettingsProvider<T> : ISettingsProvider<T>, IDisposable where T : class, new()
{
    private T _settings;
    private readonly string _filePath;
    private readonly object _lock = new();
    private Timer _debounceTimer;
    private const int DebounceDelayMs = 2000;
    private volatile bool _loaded;
    private bool _disposed;

    public event EventHandler SettingsChanged;

    public JsonSettingsProvider()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "Vrcosc-MagicChatbox", $"{typeof(T).Name}.json");
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

            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(json) && !json.All(c => c == '\0'))
                    {
                        _settings = JsonConvert.DeserializeObject<T>(json) ?? new T();
                        ApplyVersionResets();
                        SubscribeAutoSave();
                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error loading settings for {typeof(T).Name}: {ex.Message}");
            }

            _settings = new T();
            SubscribeAutoSave();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// After deserialization, check whether the loaded file is stale enough to warrant
    /// resetting individual properties or the entire module to defaults.
    /// Silently logs any resets that fire.
    /// </summary>
    private void ApplyVersionResets()
    {
        if (_settings is not VersionedSettings vs)
            return;

        var type = typeof(T);
        string loadedAppVersion = vs.AppVersion ?? string.Empty;
        int loadedSchema = vs.SchemaVersion;

        // Module-level reset: if the file's schema is older than the declared minimum
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
            return;
        }

        // Property-level reset: reset individual properties that are stale
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
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                StampVersion();

                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);

                // Atomic write: write to temp file, then rename
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                Logging.WriteInfo($"Error saving settings for {typeof(T).Name}: {ex.Message}");
            }
        }
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

            // Reuse existing timer if possible to avoid dispose/recreate race
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

    /// <summary>
    /// Writes the current app version and schema version into the settings object
    /// so they are persisted to the JSON file on the next save.
    /// No-op for settings classes that don't inherit VersionedSettings.
    /// </summary>
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
        }

        UnsubscribeAutoSave();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        // Final save to flush any pending changes
        Save();
    }
}
