using System;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.State;

/// <summary>
/// Abstraction for UI thread dispatching.
/// Services use this instead of touching WPF Dispatcher directly,
/// enabling testability and decoupling from the UI framework.
/// </summary>
public interface IUiDispatcher
{
    void Invoke(Action action);
    T Invoke<T>(Func<T> func);
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> func);
    bool CheckAccess();

    /// <summary>
    /// Fire-and-forget dispatch to the UI thread.
    /// Does NOT block the calling thread — use instead of Invoke
    /// when the caller doesn't need to wait for the result.
    /// </summary>
    void BeginInvoke(Action action);

    /// <summary>
    /// Shuts down the application gracefully.
    /// </summary>
    void Shutdown();
}
