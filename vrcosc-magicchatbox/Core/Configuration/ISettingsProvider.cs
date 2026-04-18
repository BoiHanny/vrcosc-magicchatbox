using System;

namespace vrcosc_magicchatbox.Core.Configuration;

/// <summary>
/// Generic contract for loading, saving, and observing typed settings.
/// Each module gets its own ISettingsProvider&lt;T&gt; resolved from DI.
/// </summary>
public interface ISettingsProvider<T> where T : class, new()
{
    /// <summary>The current settings instance (never null).</summary>
    T Value { get; }

    /// <summary>Persist current settings to disk.</summary>
    void Save();

    /// <summary>Reload settings from disk (or create defaults if missing/corrupt).</summary>
    void Reload();

    /// <summary>Raised after settings are loaded or saved.</summary>
    event EventHandler SettingsChanged;
}
