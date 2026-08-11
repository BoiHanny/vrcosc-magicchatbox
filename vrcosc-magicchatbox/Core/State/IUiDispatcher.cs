using System;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.State;

public interface IUiDispatcher
{
    void Invoke(Action action);
    T Invoke<T>(Func<T> func);
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> func);
    bool CheckAccess();

    void BeginInvoke(Action action);

    void Shutdown();
}
