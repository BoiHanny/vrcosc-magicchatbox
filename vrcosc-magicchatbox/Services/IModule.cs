using System;
using System.Threading;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Services;

public interface IModule : IDisposable
{
    string Name { get; }

    bool IsEnabled { get; set; }

    bool IsRunning { get; }

    Task InitializeAsync(CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);

    Task StopAsync(CancellationToken ct = default);

    void SaveSettings();
}
