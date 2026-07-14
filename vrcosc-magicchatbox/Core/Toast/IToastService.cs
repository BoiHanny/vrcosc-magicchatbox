using System.Collections.ObjectModel;

namespace vrcosc_magicchatbox.Core.Toast;

public interface IToastService
{
    ObservableCollection<ToastItemViewModel> Toasts { get; }

    void Show(
        string title,
        string message,
        ToastType type = ToastType.Info,
        ToastAction? action = null,
        int durationMs = 5000,
        string? key = null);

    void Dismiss(ToastItemViewModel item);
}
