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
                        var loaded = JsonConvert.DeserializeObject<T>(json);
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
                // The file content itself is unparseable: quarantine it so the defaults
                // saved later don't silently overwrite the evidence.
                Logging.WriteInfo($"Error loading settings for {typeof(T).Name}: {ex.Message}");
                BackupCorruptSettingsFile(ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The file could not be read (AV/sync lock, ACL denial) but may be perfectly
                // valid — do not quarantine it. Run on in-memory defaults for this session
                // and refuse all saves so the intact file on disk is never overwritten.
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

            // Outside the try: a throwing event subscriber must not condemn a good file as corrupt.
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            if (resetApplied)
                Save();
        }
    }

    /// <summary>
    /// Reads the settings file, retrying briefly on sharing violations and access
    /// denials (AV scanners, backup/sync tools) so a transiently locked file is
    /// not misread as corrupt.
    /// </summary>
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

    /// <summary>
    /// After deserialization, check whether the loaded file is stale enough to warrant
    /// resetting individual properties or the entire module to defaults.
    /// Silently logs any resets that fire.
    /// </summary>
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
            // A provider can be constructed (e.g. injected for on-demand use) without its
            // Value ever being read. Serializing the never-materialized null would replace
            // the file on disk with the literal text "null", wiping the user's settings.
            if (_settings is null)
                return;

            // After a failed (locked/denied) load the provider holds in-memory defaults;
            // persisting them would overwrite a potentially intact file on disk.
            if (_loadFailed)
                return;

            bool saved = false;
            try
            {
                StampVersion();

                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);

                // Atomic write (temp file → rename) with bounded retry/backoff.
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

    /// <summary>
    /// Surfaces a once-per-session toast when settings cannot be persisted. Best-effort:
    /// the DI container may be mid-disposal during a shutdown Save(), so all faults are swallowed.
    /// </summary>
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
            // Container disposed or toast host unavailable — the error was already logged.
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

            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        UnsubscribeAutoSave();
        Save();
    }
}
