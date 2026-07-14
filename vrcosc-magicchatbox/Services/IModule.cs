using System;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

/// <summary>
/// Common lifecycle interface for all MagicChatbox modules.
/// Modules can be initialized, started, stopped, and have settings persisted.
/// Not all modules need every lifecycle method — default to no-op where not applicable.
/// </summary>
public interface IModule : IDisposable
{
    /// <summary>Human-readable module name for logs and UI.</summary>
    string Name { get; }

    /// <summary>Whether the module is currently enabled by the user.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Whether the module is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// One-time initialization (load config, validate prerequisites).
    /// Called before the first Start.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin active operation (connect, start timers, subscribe events).
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Gracefully stop operation (disconnect, stop timers, unsubscribe).
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Persist current settings to disk.
    /// </summary>
    void SaveSettings();
}
