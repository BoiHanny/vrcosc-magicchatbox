using System;
using System.Threading.Tasks;
using System.Windows;

namespace vrcosc_magicchatbox.Core.State;

/// <summary>
/// WPF implementation of IUiDispatcher, delegates to Application.Current.Dispatcher.
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }
        Application.Current.Dispatcher.Invoke(action);
    }

    public T Invoke<T>(Func<T> func)
    {
        if (Application.Current?.Dispatcher == null)
            return func();
        return Application.Current.Dispatcher.Invoke(func);
    }

    public void BeginInvoke(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }
        Application.Current.Dispatcher.BeginInvoke(action);
    }

    public async Task InvokeAsync(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }
        await Application.Current.Dispatcher.InvokeAsync(action);
    }

    public async Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (Application.Current?.Dispatcher == null)
            return func();
        return await Application.Current.Dispatcher.InvokeAsync(func);
    }

    public bool CheckAccess()
    {
        return Application.Current?.Dispatcher?.CheckAccess() ?? true;
    }

    public void Shutdown()
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
            Application.Current.Shutdown();
        else
            Application.Current?.Dispatcher?.Invoke(() => Application.Current.Shutdown());
    }
}
