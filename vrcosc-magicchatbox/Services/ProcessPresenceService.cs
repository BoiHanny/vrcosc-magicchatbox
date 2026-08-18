using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace vrcosc_magicchatbox.Services;

public sealed class ProcessPresenceService : IProcessPresenceService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(processName, out var existing) && now < existing.ExpiresAtUtc)
            return existing.IsRunning;

        bool isRunning = QueryIsRunning(processName);
        _cache[processName] = new CacheEntry(isRunning, now + CacheTtl);
        return isRunning;
    }

    public void Invalidate(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return;

        _cache.TryRemove(processName, out _);
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }

    private static bool QueryIsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private readonly record struct CacheEntry(bool IsRunning, DateTime ExpiresAtUtc);
}
