using System;
using vrcosc_magicchatbox.Core.Toast;

namespace vrcosc_magicchatbox.Services;

public interface ITrayIconService : IDisposable
{
    bool IsInitialized { get; }

    void Initialize(MainWindow mainWindow);

    void Notify(string text, ToastAction? action = null, bool showMainWindowOnClick = true);

    void OpenContextMenu();
}
